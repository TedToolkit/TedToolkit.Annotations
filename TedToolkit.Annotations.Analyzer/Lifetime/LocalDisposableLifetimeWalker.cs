// -----------------------------------------------------------------------
// <copyright file="LocalDisposableLifetimeWalker.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer.Lifetime;

#pragma warning disable SA1600

internal sealed class LocalDisposableLifetimeWalker(
    INamedTypeSymbol disposableType,
    Action<Diagnostic> reportDiagnostic) : OperationWalker
{
    private const string OWNERSHIP_ATTRIBUTE_NAME =
        "TedToolkit.Annotations.Documentations.OwnershipAttribute";

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
        {
            return;
        }

        var ownership = GetResourceOwnership(operation.Initializer?.Value);
        if (ownership is null)
        {
            return;
        }

        _resources[operation.Symbol] = new(
            operation.Symbol,
            operation.Syntax.GetLocation(),
            IsUsingDeclaration(operation.Symbol),
            isBorrowed: ownership == ResourceOwnership.BORROWED);
    }

    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
    {
        base.VisitSimpleAssignment(operation);

        CheckPropertyOwnershipTransfer(operation);

        var target = GetReferencedLocal(operation.Target);
        if (target is null)
        {
            return;
        }

        var aliasedLocal = GetReferencedLocal(operation.Value);
        if (aliasedLocal is not null && _resources.TryGetValue(aliasedLocal, out var aliasedResource))
        {
            if (_resources.TryGetValue(target, out var replacedResource)
                && !ReferenceEquals(replacedResource, aliasedResource))
            {
                ReportUndisposedResource(replacedResource);
            }

            _resources[target] = aliasedResource;
            return;
        }

        if (_resources.TryGetValue(target, out var resource))
        {
            ReportUndisposedResource(resource);
        }

        if (!IsDisposable(target.Type))
        {
            return;
        }

        var ownership = GetResourceOwnership(operation.Value);
        if (ownership is null)
        {
            return;
        }

        _resources[target] = new(
            target,
            operation.Value.Syntax.GetLocation(),
            isUsing: false,
            isBorrowed: ownership == ResourceOwnership.BORROWED);
    }

    private void CheckPropertyOwnershipTransfer(ISimpleAssignmentOperation operation)
    {
        if (GetPropertyReference(operation.Target) is not { } property
            || !IsDisposable(property.Property.Type)
            || !HasOwnershipKind(
                property.Property,
                OwnershipFlow.INPUT,
                allowDefaultFlow: false,
                out var kind)
            || kind != OwnershipKind.TRANSFERRED)
        {
            return;
        }

        var local = GetReferencedLocal(operation.Value);
        if (local is null || !_resources.TryGetValue(local, out var resource))
        {
            return;
        }

        if (resource.IsUsing || resource.Status == ResourceStatus.TRANSFERRED)
        {
            reportDiagnostic(
                Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, operation.Value.Syntax.GetLocation(), local.Name));
            return;
        }

        if (resource.Status == ResourceStatus.DISPOSED)
        {
            reportDiagnostic(
                Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterDispose, operation.Value.Syntax.GetLocation(), local.Name));
            return;
        }

        resource.Status = ResourceStatus.TRANSFERRED;
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
        CheckOwnershipOutput(operation);
        CheckCallbackCapture(operation);
    }

    public override void VisitReturn(IReturnOperation operation)
    {
        base.VisitReturn(operation);

        var local = GetReferencedLocal(operation.ReturnedValue);
        if (local is null || !_resources.TryGetValue(local, out var resource) || resource.IsBorrowed)
        {
            return;
        }

        if (resource.IsUsing || resource.Status == ResourceStatus.DISPOSED)
        {
            reportDiagnostic(
                Diagnostic.Create(DisposableLifetimeDiagnostics.DisposedResourceReturned, operation.Syntax.GetLocation(), local.Name));
            return;
        }

        if (resource.Status == ResourceStatus.TRANSFERRED)
        {
            reportDiagnostic(
                Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, operation.Syntax.GetLocation(), local.Name));
            return;
        }

        resource.Status = ResourceStatus.TRANSFERRED;
    }

    public void ReportUndisposedResources()
    {
        foreach (var resource in new HashSet<ResourceState>(_resources.Values))
        {
            ReportUndisposedResource(resource);
        }
    }

    private void CheckInstanceLifetime(IInvocationOperation operation)
    {
        if (IsDisposeInvocation(operation)
            && GetResourceOwnership(operation.Instance) == ResourceOwnership.BORROWED)
        {
            reportDiagnostic(Diagnostic.Create(
                DisposableLifetimeDiagnostics.DisposeBorrowedProperty,
                operation.Syntax.GetLocation(),
                GetResourceName(operation.Instance)));
            return;
        }

        var local = GetReferencedLocal(operation.Instance);
        if (local is null || !_resources.TryGetValue(local, out var resource))
        {
            return;
        }

        if (IsDisposeInvocation(operation))
        {
            if (resource.IsBorrowed)
            {
                reportDiagnostic(
                    Diagnostic.Create(
                        DisposableLifetimeDiagnostics.DisposeBorrowedProperty,
                        operation.Syntax.GetLocation(),
                        local.Name));
                return;
            }

            if (resource.IsUsing || resource.Status == ResourceStatus.DISPOSED)
            {
                reportDiagnostic(
                    Diagnostic.Create(DisposableLifetimeDiagnostics.DoubleDispose, operation.Syntax.GetLocation(), local.Name));
                return;
            }

            if (resource.Status == ResourceStatus.TRANSFERRED)
            {
                reportDiagnostic(
                    Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, operation.Syntax.GetLocation(), local.Name));
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
            if (argument.Parameter is null
                || !HasOwnershipKind(
                    argument.Parameter,
                    OwnershipFlow.INPUT,
                    allowDefaultFlow: argument.Parameter.RefKind != RefKind.Ref,
                    out var kind)
                || kind != OwnershipKind.TRANSFERRED)
            {
                continue;
            }

            var local = GetReferencedLocal(argument.Value);
            if (local is null || !_resources.TryGetValue(local, out var resource))
            {
                continue;
            }

            if (resource.IsUsing || resource.Status == ResourceStatus.TRANSFERRED)
            {
                reportDiagnostic(
                    Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterTransfer, argument.Syntax.GetLocation(), local.Name));
                continue;
            }

            if (resource.Status == ResourceStatus.DISPOSED)
            {
                reportDiagnostic(
                    Diagnostic.Create(DisposableLifetimeDiagnostics.UseAfterDispose, argument.Syntax.GetLocation(), local.Name));
                continue;
            }

            resource.Status = ResourceStatus.TRANSFERRED;
        }
    }

    private void CheckOwnershipOutput(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            var parameter = argument.Parameter;
            if (parameter is null || !IsDisposable(parameter.Type))
            {
                continue;
            }

            var ownership = GetOutputOwnership(parameter);
            if (ownership is null)
            {
                continue;
            }

            var local = GetReferencedLocal(argument.Value);
            if (local is null)
            {
                continue;
            }

            if (_resources.TryGetValue(local, out var replacedResource))
            {
                ReportUndisposedResource(replacedResource);
            }

            _resources[local] = new(
                local,
                argument.Syntax.GetLocation(),
                isUsing: false,
                isBorrowed: ownership == ResourceOwnership.BORROWED);
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
            {
                continue;
            }

            var capturedLocals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
            new LocalReferenceCollector(capturedLocals).Visit(callback.Body);

            foreach (var capturedLocal in capturedLocals)
            {
                if (!_resources.TryGetValue(capturedLocal, out var resource))
                {
                    continue;
                }

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

        if (rule is null)
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(rule, location, resource.Symbol.Name));
    }

    private void ReportUndisposedResource(ResourceState resource)
    {
        if (resource.IsBorrowed || resource.IsUsing || resource.Status != ResourceStatus.OWNED || resource.LeakReported)
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.UndisposedResource,
            resource.DeclarationLocation,
            resource.Symbol.Name));
        resource.LeakReported = true;
    }

    private void ReportDeferredCallbackLifetime(ResourceState resource, Location location)
    {
        if (resource.CallbackLifetime is not { } callbackLifetime)
        {
            return;
        }

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
            {
                resource.IsUsing = true;
            }
        }
    }

    private bool IsDisposable(ITypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(type, disposableType)
            || type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, disposableType));
    }

    private ResourceOwnership? GetResourceOwnership(IOperation? operation)
    {
        return operation switch
        {
            IObjectCreationOperation => ResourceOwnership.OWNED,
            IInvocationOperation invocation when invocation.Type is not null && IsDisposable(invocation.Type) =>
                GetOwnership(invocation.TargetMethod.GetReturnTypeAttributes(), OwnershipFlow.OUTPUT, OwnershipKind.TRANSFERRED),
            IPropertyReferenceOperation property when IsDisposable(property.Property.Type) =>
                GetOwnership(property.Property, OwnershipFlow.OUTPUT, OwnershipKind.UNCHANGED),
            IFieldReferenceOperation field when IsDisposable(field.Field.Type) =>
                GetFieldOwnership(field.Field),
            IConversionOperation conversion => GetResourceOwnership(conversion.Operand),
            _ => null,
        };
    }

    private static ResourceOwnership? GetFieldOwnership(IFieldSymbol field)
    {
        return HasOwnershipKind(field, OwnershipFlow.DEFAULT, allowDefaultFlow: false, out var kind)
            ? ToResourceOwnership(kind)
            : null;
    }

    private static bool IsDisposeInvocation(IInvocationOperation operation)
    {
        return operation.TargetMethod.Name == nameof(IDisposable.Dispose)
            && operation.Arguments.Length == 0;
    }

    private static ILocalSymbol? GetReferencedLocal(IOperation? operation)
    {
        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IDeclarationExpressionOperation declaration => GetReferencedLocal(declaration.Expression),
            IConversionOperation conversion => GetReferencedLocal(conversion.Operand),
            _ => null,
        };
    }

    private static string GetResourceName(IOperation? operation)
    {
        return GetPropertyReference(operation)?.Property.Name
            ?? GetFieldReference(operation)?.Field.Name
            ?? GetReferencedLocal(operation)?.Name
            ?? "resource";
    }

    private static IAnonymousFunctionOperation? GetAnonymousFunction(IOperation operation)
    {
        return operation switch
        {
            IAnonymousFunctionOperation callback => callback,
            IDelegateCreationOperation delegateCreation => GetAnonymousFunction(delegateCreation.Target),
            IConversionOperation conversion => GetAnonymousFunction(conversion.Operand),
            _ => null,
        };
    }

    private static IPropertyReferenceOperation? GetPropertyReference(IOperation? operation)
    {
        return operation switch
        {
            IPropertyReferenceOperation propertyReference => propertyReference,
            IConversionOperation conversion => GetPropertyReference(conversion.Operand),
            _ => null,
        };
    }

    private static IFieldReferenceOperation? GetFieldReference(IOperation? operation)
    {
        return operation switch
        {
            IFieldReferenceOperation fieldReference => fieldReference,
            IConversionOperation conversion => GetFieldReference(conversion.Operand),
            _ => null,
        };
    }

    private static ResourceOwnership GetOwnership(ISymbol symbol, OwnershipFlow flow, OwnershipKind defaultKind)
    {
        return GetOwnership(symbol.GetAttributes(), flow, defaultKind);
    }

    private static ResourceOwnership GetOwnership(
        in ImmutableArray<AttributeData> attributes,
        OwnershipFlow flow,
        OwnershipKind defaultKind)
    {
        return HasOwnershipKind(attributes, flow, allowDefaultFlow: true, out var kind)
            ? ToResourceOwnership(kind)
            : ToResourceOwnership(defaultKind);
    }

    private static ResourceOwnership? GetOutputOwnership(IParameterSymbol parameter)
    {
        if (parameter.RefKind == RefKind.Out)
        {
            return GetOwnership(parameter, OwnershipFlow.OUTPUT, OwnershipKind.TRANSFERRED);
        }

        if (parameter.RefKind != RefKind.Ref)
        {
            return null;
        }

        return HasOwnershipKind(parameter, OwnershipFlow.OUTPUT, allowDefaultFlow: false, out var kind)
            ? ToResourceOwnership(kind)
            : null;
    }

    private static ResourceOwnership ToResourceOwnership(OwnershipKind kind)
    {
        return kind == OwnershipKind.TRANSFERRED ? ResourceOwnership.OWNED : ResourceOwnership.BORROWED;
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
            if (attribute.AttributeClass?.ToDisplayString() != OWNERSHIP_ATTRIBUTE_NAME
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
        {
            return false;
        }

        return declaration.Parent?.Parent is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0, }
            or UsingStatementSyntax;
    }

    private static LocalizableResourceString GetCallbackLifetimeName(int callbackLifetime)
    {
        return callbackLifetime == 1
            ? DiagnosticResources.Get("CallbackLifetimeDeferred")
            : DiagnosticResources.Get("CallbackLifetimeSubscription");
    }

    private enum ResourceStatus
    {
        OWNED = 0,

        DISPOSED = 1,

        TRANSFERRED = 2,
    }

    private enum ResourceOwnership
    {
        OWNED = 0,

        BORROWED = 1,
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
        public HashSet<ILocalSymbol> Locals { get; } = locals ?? new(SymbolEqualityComparer.Default);

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