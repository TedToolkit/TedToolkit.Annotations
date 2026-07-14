// -----------------------------------------------------------------------
// <copyright file="OwnershipLifetimeAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer.Lifetime;

/// <summary>
/// Registers ownership contract and owned-field lifetime analysis.
/// </summary>
internal static class OwnershipLifetimeAnalysis
{
    /// <summary>
    /// Registers ownership analysis actions.
    /// </summary>
    /// <param name="context">The compilation analysis context.</param>
    /// <param name="disposableType">The disposable interface symbol.</param>
    internal static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol disposableType)
    {
        context.RegisterSymbolStartAction(symbolStartContext =>
        {
            if (symbolStartContext.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct, } type)
            {
                return;
            }

            var state = new OwnedFieldAnalysisState(type, disposableType);
            symbolStartContext.RegisterOperationBlockAction(blockContext => state.Analyze(blockContext));
            symbolStartContext.RegisterSymbolEndAction(symbolEndContext => state.Report(symbolEndContext.ReportDiagnostic));
        }, SymbolKind.NamedType);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeOwnershipTarget(symbolContext.Symbol, disposableType, symbolContext.ReportDiagnostic),
            SymbolKind.Field,
            SymbolKind.Method,
            SymbolKind.Parameter,
            SymbolKind.Property);
    }

    private static void AnalyzeOwnershipTarget(
        ISymbol symbol,
        INamedTypeSymbol disposableType,
        Action<Diagnostic> reportDiagnostic)
    {
        var type = symbol switch
        {
            IFieldSymbol field => field.Type,
            IMethodSymbol ownershipMethod => ownershipMethod.ReturnType,
            IParameterSymbol parameter => parameter.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        if (type is null || IsDisposable(type, disposableType))
        {
            return;
        }

        var attributes = symbol is IMethodSymbol methodForAttributes
            ? methodForAttributes.GetReturnTypeAttributes()
            : symbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != "TedToolkit.Annotations.Documentations.OwnershipAttribute")
            {
                continue;
            }

            reportDiagnostic(Diagnostic.Create(
                DisposableLifetimeDiagnostics.OwnershipTargetMustBeDisposable,
                attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? symbol.Locations[0],
                type.ToDisplayString()));
        }
    }

    private static bool IsDisposable(ITypeSymbol type, INamedTypeSymbol disposableType)
    {
        return SymbolEqualityComparer.Default.Equals(type, disposableType)
            || type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, disposableType));
    }

    private static IFieldSymbol? GetReferencedField(IOperation? operation)
    {
        return operation switch
        {
            IFieldReferenceOperation fieldReference => fieldReference.Field,
            IConversionOperation conversion => GetReferencedField(conversion.Operand),
            _ => null,
        };
    }

    private static IParameterSymbol? GetReferencedParameter(IOperation? operation)
    {
        return operation switch
        {
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            IConversionOperation conversion => GetReferencedParameter(conversion.Operand),
            _ => null,
        };
    }

    private static bool HasOwnershipKind(
        ISymbol symbol,
        OwnershipFlow flow,
        bool allowDefaultFlow,
        out OwnershipKind kind)
    {
        return HasOwnershipKind(symbol.GetAttributes(), flow, allowDefaultFlow, out kind);
    }

    private static bool HasOwnershipKind(
        in ImmutableArray<AttributeData> attributes,
        OwnershipFlow flow,
        bool allowDefaultFlow,
        out OwnershipKind kind)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != "TedToolkit.Annotations.Documentations.OwnershipAttribute"
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not int kindValue)
            {
                continue;
            }

            var attributeFlow = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int flowValue
                ? (OwnershipFlow)flowValue
                : OwnershipFlow.DEFAULT;
            if (attributeFlow != flow && (!allowDefaultFlow || attributeFlow != OwnershipFlow.DEFAULT))
            {
                continue;
            }

            kind = (OwnershipKind)kindValue;
            return true;
        }

        kind = default;
        return false;
    }

    private enum OwnershipKind
    {
        UNCHANGED = 0,

        TRANSFERRED = 1,
    }

    private enum OwnershipFlow
    {
        DEFAULT = 0,

        INPUT = 1,

        OUTPUT = 2,
    }

    private sealed class OwnedFieldAnalysisState
    {
        private readonly INamedTypeSymbol _disposableType;

        private readonly IMethodSymbol? _disposeMethod;

        private readonly HashSet<IFieldSymbol> _borrowedFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _candidateFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _explicitlyOwnedFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _inferredOwnedFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _releasedFields = new(SymbolEqualityComparer.Default);

        private readonly object _gate = new();

        private readonly INamedTypeSymbol _type;

        public OwnedFieldAnalysisState(INamedTypeSymbol type, INamedTypeSymbol disposableType)
        {
            _type = type;
            _disposableType = disposableType;
            _disposeMethod = GetDisposeMethod(type, disposableType);

            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || !IsDisposable(field.Type, disposableType))
                {
                    continue;
                }

                _candidateFields.Add(field);
                if (!HasOwnershipKind(field, OwnershipFlow.DEFAULT, allowDefaultFlow: false, out var kind))
                {
                    continue;
                }

                if (kind == OwnershipKind.TRANSFERRED)
                {
                    _explicitlyOwnedFields.Add(field);
                }
                else
                {
                    _borrowedFields.Add(field);
                }
            }
        }

        public void Analyze(OperationBlockAnalysisContext context)
        {
            if (context.OwningSymbol is not IMethodSymbol method)
            {
                return;
            }

            if (method.MethodKind == MethodKind.Constructor && SymbolEqualityComparer.Default.Equals(method.ContainingType, _type))
            {
                var collector = new OwnedFieldAssignmentCollector(_candidateFields, _disposableType);
                var constructorBlocks = context.OperationBlocks;
                VisitOperationBlocks(in constructorBlocks, collector);
                lock (_gate)
                {
                    _inferredOwnedFields.UnionWith(collector.OwnedFields);
                }

                return;
            }

            if (_disposeMethod is null || !SymbolEqualityComparer.Default.Equals(method, _disposeMethod))
            {
                return;
            }

            var releaseCollector = new ReleasedFieldCollector(_candidateFields);
            var operationBlocks = context.OperationBlocks;
            VisitOperationBlocks(in operationBlocks, releaseCollector);
            lock (_gate)
            {
                _releasedFields.UnionWith(releaseCollector.ReleasedFields);
            }
        }

        public void Report(Action<Diagnostic> reportDiagnostic)
        {
            lock (_gate)
            {
                var ownedFields = new HashSet<IFieldSymbol>(_explicitlyOwnedFields, SymbolEqualityComparer.Default);
                ownedFields.UnionWith(_inferredOwnedFields);
                ownedFields.ExceptWith(_borrowedFields);
                if (ownedFields.Count == 0)
                {
                    return;
                }

                if (!IsDisposable(_type, _disposableType))
                {
                    foreach (var field in ownedFields)
                    {
                        reportDiagnostic(Diagnostic.Create(
                            DisposableLifetimeDiagnostics.OwnedFieldRequiresDisposableType,
                            field.Locations[0],
                            _type.Name,
                            field.Name));
                    }

                    return;
                }

                foreach (var field in ownedFields)
                {
                    if (!_releasedFields.Contains(field))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            DisposableLifetimeDiagnostics.OwnedFieldNotReleased,
                            field.Locations[0],
                            field.Name));
                    }
                }
            }
        }

        private static IMethodSymbol? GetDisposeMethod(INamedTypeSymbol type, INamedTypeSymbol disposableType)
        {
            var disposableMethod = disposableType.GetMembers(nameof(IDisposable.Dispose))
                .OfType<IMethodSymbol>()
                .SingleOrDefault();
            return disposableMethod is null
                ? null
                : type.FindImplementationForInterfaceMember(disposableMethod) as IMethodSymbol;
        }

        private static void VisitOperationBlocks(in ImmutableArray<IOperation> operationBlocks, OperationWalker walker)
        {
            foreach (var operationBlock in operationBlocks)
            {
                walker.Visit(operationBlock);
            }
        }
    }

    private sealed class OwnedFieldAssignmentCollector(
        HashSet<IFieldSymbol> candidateFields,
        INamedTypeSymbol disposableType) : OperationWalker
    {
        public HashSet<IFieldSymbol> OwnedFields { get; } = new(SymbolEqualityComparer.Default);

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);

            var field = GetReferencedField(operation.Target);
            var parameter = GetReferencedParameter(operation.Value);
            if (field is null
                || parameter is null
                || !candidateFields.Contains(field)
                || !IsDisposable(field.Type, disposableType)
                || !HasOwnershipKind(parameter, OwnershipFlow.INPUT, allowDefaultFlow: true, out var kind)
                || kind != OwnershipKind.TRANSFERRED)
            {
                return;
            }

            OwnedFields.Add(field);
        }
    }

    private sealed class ReleasedFieldCollector(HashSet<IFieldSymbol> candidateFields) : OperationWalker
    {
        public HashSet<IFieldSymbol> ReleasedFields { get; } = new(SymbolEqualityComparer.Default);

        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);

            if (operation.TargetMethod.Name == nameof(IDisposable.Dispose) && operation.Arguments.Length == 0)
            {
                AddIfCandidate(GetReferencedField(operation.Instance));
                return;
            }

            foreach (var argument in operation.Arguments)
            {
                if (argument.Parameter is not null
                    && HasOwnershipKind(
                        argument.Parameter,
                        OwnershipFlow.INPUT,
                        allowDefaultFlow: argument.Parameter.RefKind != RefKind.Ref,
                        out var kind)
                    && kind == OwnershipKind.TRANSFERRED)
                {
                    AddIfCandidate(GetReferencedField(argument.Value));
                }
            }
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);

            if (operation.Target is not IPropertyReferenceOperation property
                || !HasOwnershipKind(property.Property, OwnershipFlow.INPUT, allowDefaultFlow: false, out var kind)
                || kind != OwnershipKind.TRANSFERRED)
            {
                return;
            }

            AddIfCandidate(GetReferencedField(operation.Value));
        }

        private void AddIfCandidate(IFieldSymbol? field)
        {
            if (field is null || !candidateFields.Contains(field))
            {
                return;
            }

            ReleasedFields.Add(field);
        }
    }
}