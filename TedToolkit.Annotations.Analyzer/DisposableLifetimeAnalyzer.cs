// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports statically provable lifetime violations for locally owned <see cref="IDisposable"/> resources.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableLifetimeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DisposableLifetimeDiagnostics.DoubleDispose,
        DisposableLifetimeDiagnostics.UseAfterDispose,
        DisposableLifetimeDiagnostics.UseAfterTransfer,
        DisposableLifetimeDiagnostics.UndisposedResource,
        DisposableLifetimeDiagnostics.CallbackOutlivesResource,
        DisposableLifetimeDiagnostics.DisposeBorrowedProperty,
        DisposableLifetimeDiagnostics.DisposedResourceReturned);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(RegisterLifetimeAnalysis);
    }

    private static void RegisterLifetimeAnalysis(CompilationStartAnalysisContext context)
    {
        var disposableType = context.Compilation.GetTypeByMetadataName("System.IDisposable");
        if (disposableType is null)
            return;

        context.RegisterOperationBlockAction(blockContext =>
        {
            foreach (var operationBlock in blockContext.OperationBlocks)
            {
                var walker = new DisposableLifetimeWalker(disposableType, blockContext.ReportDiagnostic);
                walker.Visit(operationBlock);
                walker.ReportUndisposedResources();
            }
        });
    }

    private sealed class DisposableLifetimeWalker(
        INamedTypeSymbol disposableType,
        Action<Diagnostic> reportDiagnostic) : OperationWalker
    {
        private const string TRANSFERS_OWNERSHIP_ATTRIBUTE_NAME =
            "TedToolkit.Annotations.Documentations.TransfersOwnershipAttribute";
        private const string CALLBACK_LIFETIME_ATTRIBUTE_NAME =
            "TedToolkit.Annotations.Documentations.CallbackLifetimeAttribute";

        private readonly Dictionary<ILocalSymbol, ResourceState> _resources =
            new(SymbolEqualityComparer.Default);

        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
        {
            // The callback body is analyzed as a separate operation block. Its captures are checked at the call site.
        }

        public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
        {
            base.VisitVariableDeclarator(operation);

            var aliasedLocal = GetReferencedLocal(operation.Initializer?.Value);
            if (aliasedLocal is not null && _resources.TryGetValue(aliasedLocal, out var aliasedResource))
            {
                _resources[operation.Symbol] = aliasedResource;
                return;
            }

            if (!IsDisposable(operation.Symbol.Type))
                return;

            var isBorrowed = GetPropertyReference(operation.Initializer?.Value) is not null;
            if (!isBorrowed && !IsOwnedResourceCreation(operation.Initializer?.Value))
                return;

            _resources[operation.Symbol] = new ResourceState(
                operation.Symbol,
                operation.Syntax.GetLocation(),
                IsUsingDeclaration(operation.Symbol),
                isBorrowed);
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);

            var target = GetReferencedLocal(operation.Target);
            if (target is null)
                return;

            var aliasedLocal = GetReferencedLocal(operation.Value);
            if (aliasedLocal is not null && _resources.TryGetValue(aliasedLocal, out var aliasedResource))
            {
                if (_resources.TryGetValue(target, out var replacedResource)
                    && !ReferenceEquals(replacedResource, aliasedResource))
                    ReportUndisposedResource(replacedResource);

                _resources[target] = aliasedResource;
                return;
            }

            if (_resources.TryGetValue(target, out var resource))
                ReportUndisposedResource(resource);

            if (!IsDisposable(target.Type))
                return;

            var isBorrowed = GetPropertyReference(operation.Value) is not null;
            if (isBorrowed || IsOwnedResourceCreation(operation.Value))
            {
                _resources[target] = new ResourceState(
                    target,
                    operation.Value.Syntax.GetLocation(),
                    isUsing: false,
                    isBorrowed: isBorrowed);
            }
        }

        public override void VisitUsing(IUsingOperation operation)
        {
            MarkUsingResource(operation.Resources);
            base.VisitUsing(operation);
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);

            CheckInstanceLifetime(operation);
            CheckOwnershipTransfer(operation);
            CheckCallbackCapture(operation);
        }

        public override void VisitReturn(IReturnOperation operation)
        {
            base.VisitReturn(operation);

            var local = GetReferencedLocal(operation.ReturnedValue);
            if (local is null || !_resources.TryGetValue(local, out var resource) || resource.IsBorrowed)
                return;

            if (resource.IsUsing || resource.Status == ResourceStatus.DISPOSED)
            {
                reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DisposedResourceReturned, operation.Syntax.GetLocation(), local.Name));
                return;
            }

            if (resource.Status == ResourceStatus.TRANSFERRED)
            {
                reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, operation.Syntax.GetLocation(), local.Name));
                return;
            }

            resource.Status = ResourceStatus.TRANSFERRED;
        }

        public void ReportUndisposedResources()
        {
            foreach (var resource in new HashSet<ResourceState>(_resources.Values))
                ReportUndisposedResource(resource);
        }

        private void CheckInstanceLifetime(IInvocationOperation operation)
        {
            if (IsDisposeInvocation(operation)
                && GetPropertyReference(operation.Instance) is { } property
                && IsDisposable(property.Property.Type))
            {
                reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DisposeBorrowedProperty, operation.Syntax.GetLocation(), property.Property.Name));
                return;
            }

            var local = GetReferencedLocal(operation.Instance);
            if (local is null || !_resources.TryGetValue(local, out var resource))
                return;

            if (IsDisposeInvocation(operation))
            {
                if (resource.IsBorrowed)
                {
                    reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DisposeBorrowedProperty, operation.Syntax.GetLocation(), local.Name));
                    return;
                }

                if (resource.IsUsing || resource.Status == ResourceStatus.DISPOSED)
                {
                    reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DoubleDispose, operation.Syntax.GetLocation(), local.Name));
                    return;
                }

                if (resource.Status == ResourceStatus.TRANSFERRED)
                {
                    reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, operation.Syntax.GetLocation(), local.Name));
                    return;
                }

                ReportDeferredCallbackLifetime(resource, operation.Syntax.GetLocation());
                resource.Status = ResourceStatus.DISPOSED;
                return;
            }

            ReportInvalidUse(resource, operation.Syntax.GetLocation());
        }

        private void CheckOwnershipTransfer(IInvocationOperation operation)
        {
            foreach (var argument in operation.Arguments)
            {
                if (argument.Parameter is null || !HasAttribute(argument.Parameter, TRANSFERS_OWNERSHIP_ATTRIBUTE_NAME))
                    continue;

                var local = GetReferencedLocal(argument.Value);
                if (local is null || !_resources.TryGetValue(local, out var resource))
                    continue;

                if (resource.IsUsing || resource.Status == ResourceStatus.TRANSFERRED)
                {
                    reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, argument.Syntax.GetLocation(), local.Name));
                    continue;
                }

                if (resource.Status == ResourceStatus.DISPOSED)
                {
                    reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterDispose, argument.Syntax.GetLocation(), local.Name));
                    continue;
                }

                resource.Status = ResourceStatus.TRANSFERRED;
            }
        }

        private void CheckCallbackCapture(IInvocationOperation operation)
        {
            foreach (var argument in operation.Arguments)
            {
                if (argument.Parameter is null
                    || !TryGetCallbackLifetime(argument.Parameter, out var callbackLifetime)
                    || callbackLifetime == 0
                    || GetAnonymousFunction(argument.Value) is not { } callback)
                    continue;

                var capturedLocals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
                new LocalReferenceCollector(capturedLocals).Visit(callback.Body);

                foreach (var capturedLocal in capturedLocals)
                {
                    if (!_resources.TryGetValue(capturedLocal, out var resource))
                        continue;

                    if (!resource.IsUsing)
                    {
                        resource.CallbackLifetime = callbackLifetime;
                        continue;
                    }

                    reportDiagnostic(Diagnostic.Create(
                        DisposableLifetimeDiagnostics.CallbackOutlivesResource,
                        callback.Syntax.GetLocation(),
                        GetCallbackLifetimeName(callbackLifetime),
                        capturedLocal.Name));
                }
            }
        }

        private void ReportInvalidUse(ResourceState resource, Location location)
        {
            var rule = resource.Status switch
            {
                ResourceStatus.DISPOSED => DisposableLifetimeDiagnostics.UseAfterDispose,
                ResourceStatus.TRANSFERRED => DisposableLifetimeDiagnostics.UseAfterTransfer,
                _ => null,
            };

            if (rule is not null)
                reportDiagnostic(Diagnostic.Create(rule, location, resource.Symbol.Name));
        }

        private void ReportUndisposedResource(ResourceState resource)
        {
            if (resource.IsBorrowed || resource.IsUsing || resource.Status != ResourceStatus.OWNED || resource.LeakReported)
                return;

            reportDiagnostic(Diagnostic.Create(
                DisposableLifetimeDiagnostics.UndisposedResource,
                resource.DeclarationLocation,
                resource.Symbol.Name));
            resource.LeakReported = true;
        }

        private void ReportDeferredCallbackLifetime(ResourceState resource, Location location)
        {
            if (resource.CallbackLifetime is not { } callbackLifetime)
                return;

            reportDiagnostic(Diagnostic.Create(
                DisposableLifetimeDiagnostics.CallbackOutlivesResource,
                location,
                GetCallbackLifetimeName(callbackLifetime),
                resource.Symbol.Name));
        }

        private void MarkUsingResource(IOperation resources)
        {
            var collector = new LocalReferenceCollector();
            collector.Visit(resources);

            foreach (var local in collector.Locals)
            {
                if (_resources.TryGetValue(local, out var resource))
                    resource.IsUsing = true;
            }
        }

        private bool IsDisposable(ITypeSymbol type) =>
            SymbolEqualityComparer.Default.Equals(type, disposableType)
            || type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, disposableType));

        private bool IsOwnedResourceCreation(IOperation? operation) =>
            operation switch
            {
                IObjectCreationOperation => true,
                IInvocationOperation invocation when invocation.Type is not null && IsDisposable(invocation.Type) => true,
                IConversionOperation conversion => IsOwnedResourceCreation(conversion.Operand),
                _ => false,
            };

        private static bool IsDisposeInvocation(IInvocationOperation operation) =>
            operation.TargetMethod.Name == nameof(IDisposable.Dispose)
            && operation.Arguments.Length == 0;

        private static ILocalSymbol? GetReferencedLocal(IOperation? operation) =>
            operation switch
            {
                ILocalReferenceOperation localReference => localReference.Local,
                IConversionOperation conversion => GetReferencedLocal(conversion.Operand),
                _ => null,
            };

        private static IAnonymousFunctionOperation? GetAnonymousFunction(IOperation operation) =>
            operation switch
            {
                IAnonymousFunctionOperation callback => callback,
                IDelegateCreationOperation delegateCreation => GetAnonymousFunction(delegateCreation.Target),
                IConversionOperation conversion => GetAnonymousFunction(conversion.Operand),
                _ => null,
            };

        private static IPropertyReferenceOperation? GetPropertyReference(IOperation? operation) =>
            operation switch
            {
                IPropertyReferenceOperation propertyReference => propertyReference,
                IConversionOperation conversion => GetPropertyReference(conversion.Operand),
                _ => null,
            };

        private static bool HasAttribute(ISymbol symbol, string attributeName) =>
            symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == attributeName);

        private static bool TryGetCallbackLifetime(IParameterSymbol parameter, out int callbackLifetime)
        {
            var attribute = parameter.GetAttributes().FirstOrDefault(candidate =>
                candidate.AttributeClass?.ToDisplayString() == CALLBACK_LIFETIME_ATTRIBUTE_NAME);
            if (attribute is null || attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not int value)
            {
                callbackLifetime = default;
                return false;
            }

            callbackLifetime = value;
            return true;
        }

        private static bool IsUsingDeclaration(ILocalSymbol local)
        {
            if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax declaration)
                return false;

            return declaration.Parent?.Parent is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0 }
                or UsingStatementSyntax;
        }

        private static LocalizableString GetCallbackLifetimeName(int callbackLifetime) =>
            callbackLifetime == 1
                ? DiagnosticResources.Get("CallbackLifetimeDeferred")
                : DiagnosticResources.Get("CallbackLifetimeSubscription");

        private enum ResourceStatus
        {
            OWNED,
            DISPOSED,
            TRANSFERRED,
        }

        private sealed class ResourceState(ILocalSymbol symbol, Location declarationLocation, bool isUsing, bool isBorrowed = false)
        {
            public Location DeclarationLocation { get; } = declarationLocation;

            public bool IsUsing { get; set; } = isUsing;

            public bool IsBorrowed { get; } = isBorrowed;

            public int? CallbackLifetime { get; set; }

            public bool LeakReported { get; set; }

            public ResourceStatus Status { get; set; }

            public ILocalSymbol Symbol { get; } = symbol;
        }

        private sealed class LocalReferenceCollector(HashSet<ILocalSymbol>? locals = null) : OperationWalker
        {
            public HashSet<ILocalSymbol> Locals { get; } = locals ?? new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

            public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
            {
                // A nested callback owns its own captures.
            }

            public override void VisitLocalReference(ILocalReferenceOperation operation)
            {
                Locals.Add(operation.Local);
                base.VisitLocalReference(operation);
            }
        }
    }
}
