// -----------------------------------------------------------------------
// <copyright file="LocalDisposableLifetimeWalker.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;
using TedToolkit.Annotations.Analyzer.Lifetime.Diagnostics;
using TedToolkit.Annotations.Analyzer.Lifetime.Model;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Local;

/// <summary>
/// Applies disposable state transitions while walking one control-flow block.
/// </summary>
/// <param name="contract">The disposal interfaces and release methods available to the compilation.</param>
/// <param name="reportDiagnostic">The sink for violations discovered during the walk.</param>
/// <param name="initialState">The abstract resource state at the block entry.</param>
/// <param name="controlFlowGraph">The graph that owns flow-capture placeholders.</param>
/// <param name="localFunctionReleases">A summary of captured locals released by each local function.</param>
/// <param name="owningMethod">The method whose return and parameter ownership contracts apply.</param>
/// <param name="cancellationToken">A token used to cancel analysis.</param>
internal sealed class LocalDisposableLifetimeWalker(
    DisposableContract contract,
    Action<Diagnostic> reportDiagnostic,
    LifetimeObjectStore? initialState = null,
    ControlFlowGraph? controlFlowGraph = null,
    IReadOnlyDictionary<IMethodSymbol, HashSet<ILocalSymbol>>? localFunctionReleases = null,
    IMethodSymbol? owningMethod = null,
    CancellationToken cancellationToken = default) : OperationWalker
{
    private readonly LifetimeObjectStore _resources = initialState ?? new();

    private readonly LifetimeDiagnosticReporter _diagnostics = new(reportDiagnostic);

    private bool _isVisitingReturnedValue;

    /// <inheritdoc/>
    public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
    {
        // The callback body is analyzed as a separate operation block. Its captures are checked at the call site.
    }

    /// <inheritdoc/>
    public override void VisitFlowCapture(IFlowCaptureOperation operation)
    {
        base.VisitFlowCapture(operation);
        _resources.SetCaptureOrigin(operation.Id, operation.Value);
    }

    /// <inheritdoc/>
    public override void VisitConditional(IConditionalOperation operation)
    {
        Visit(operation.Condition);
        var branchEntry = _resources.Clone();

        // Each arm starts from the same state. The merged store conservatively retains every possible alias and state.
        Visit(operation.WhenTrue);
        var whenTrue = _resources.Clone();

        _resources.ReplaceWith(branchEntry);
        Visit(operation.WhenFalse);
        var whenFalse = _resources.Clone();

        _resources.ReplaceWith(LifetimeObjectStore.Merge(whenTrue, whenFalse));
    }

    /// <inheritdoc/>
    public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
    {
        base.VisitVariableDeclarator(operation);

        RegisterAsyncDisposeResult(operation.Symbol, operation.Initializer?.Value);

        if (_resources.TryResolveLifetimeObject(operation.Initializer?.Value, out var aliasedResource))
        {
            _resources.Set(operation.Symbol, aliasedResource);
            return;
        }

        if (!TryCreateLifetimeObject(operation, out var lifetimeObject))
        {
            return;
        }

        _resources.Set(operation.Symbol, lifetimeObject!);
    }

    /// <inheritdoc/>
    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
    {
        base.VisitSimpleAssignment(operation);

        CheckPropertyOwnershipTransfer(operation);

        var target = GetReferencedSymbol(operation.Target);
        if (target is null)
        {
            return;
        }

        if (target is ILocalSymbol targetLocal)
        {
            RegisterAsyncDisposeResult(targetLocal, operation.Value);
        }

        if (_resources.TryResolveLifetimeObject(operation.Value, out var aliasedResource))
        {
            if (_resources.TryGet(target, out var replacedResource)
                && !ReferenceEquals(replacedResource, aliasedResource))
            {
                ReportOverwrite(replacedResource, operation.Target.Syntax.GetLocation());
            }

            _resources.Set(target, aliasedResource);
            return;
        }

        ReportOverwrite(
            _resources.TryGet(target, out var resource) ? resource : null,
            operation.Target.Syntax.GetLocation());

        if (!TryCreateLifetimeObject(
                target,
                operation.Value,
                operation.Value.Syntax.GetLocation(),
                isUsing: target is ILocalSymbol local && IsUsingDeclaration(local),
                out var lifetimeObject))
        {
            return;
        }

        _resources.Set(target, lifetimeObject!);
    }

    private void CheckPropertyOwnershipTransfer(ISimpleAssignmentOperation operation)
    {
        var transfersOwnership = GetPropertyReference(operation.Target) is { } property
            && contract.IsDisposable(property.Property.Type)
            && LifetimeOwnershipSemantics.IsTransferredInput(property.Property);
        transfersOwnership |= GetFieldReference(operation.Target) is { } field
            && contract.IsDisposable(field.Field.Type)
            && (LifetimeOwnershipSemantics.IsExplicitlyOwned(field.Field)
                || (owningMethod?.MethodKind == MethodKind.Constructor
                    && !LifetimeOwnershipSemantics.IsExplicitlyBorrowed(field.Field)));
        if (!transfersOwnership)
        {
            return;
        }

        var symbol = GetReferencedSymbol(operation.Value);
        if (symbol is null || !_resources.TryGet(symbol, out var resource))
        {
            return;
        }

        _diagnostics.ReportTransfer(
            resource.TransferOwnership(),
            operation.Value.Syntax.GetLocation(),
            symbol.Name);
    }

    /// <inheritdoc/>
    public override void VisitUsing(IUsingOperation operation)
    {
        MarkUsingResource(operation.Resources);
        base.VisitUsing(operation);
    }

    /// <inheritdoc/>
    public override void VisitInvocation(IInvocationOperation operation)
    {
        base.VisitInvocation(operation);

        CheckInstanceLifetime(operation);
        CheckOwnershipTransfer(operation);
        CheckOwnershipOutput(operation);
        CheckCallbackCapture(operation);
        CheckLocalFunctionRelease(operation);
        if (!(operation.TargetMethod.Name is "GetResult" or "Wait"))
        {
            return;
        }

        ObserveAsyncReleaseInvocations(operation);
    }

    /// <inheritdoc/>
    public override void VisitArgument(IArgumentOperation operation)
    {
        base.VisitArgument(operation);
        if (operation.Parameter is null
            || operation.Parameter.RefKind == RefKind.Out
            || LifetimeOwnershipSemantics.IsTransferredInput(operation.Parameter))
        {
            return;
        }

        ReportReferencedLocalUse(operation.Value, operation.Syntax.GetLocation());
    }

    /// <inheritdoc/>
    public override void VisitPropertyReference(IPropertyReferenceOperation operation)
    {
        base.VisitPropertyReference(operation);
        ReportReferencedLocalUse(operation.Instance, operation.Syntax.GetLocation());
    }

    /// <inheritdoc/>
    public override void VisitFieldReference(IFieldReferenceOperation operation)
    {
        base.VisitFieldReference(operation);
        ReportReferencedLocalUse(operation.Instance, operation.Syntax.GetLocation());
    }

    /// <inheritdoc/>
    public override void VisitReturn(IReturnOperation operation)
    {
        base.VisitReturn(operation);

        _resources.ObserveAsyncDisposeResult(operation.ReturnedValue);

        if (!_resources.TryResolveLifetimeObject(operation.ReturnedValue, out var resource))
        {
            ValidateUntrackedReturnOwnership(operation.ReturnedValue, operation.Syntax.GetLocation());
            return;
        }

        if (!ValidateReturnOwnership(resource, operation.Syntax.GetLocation()))
        {
            return;
        }

        var resourceName = resource.Symbol.Name;

        if (resource.IsUsing)
        {
            _diagnostics.ReportDisposedReturn(operation.Syntax.GetLocation(), resourceName);
            return;
        }

        switch (resource.TransferOwnership())
        {
            case LifetimeTransitionResultType.DISPOSED:
                _diagnostics.ReportDisposedReturn(operation.Syntax.GetLocation(), resourceName);
                return;

            case LifetimeTransitionResultType.TRANSFERRED:
                _diagnostics.ReportTransfer(
                    LifetimeTransitionResultType.TRANSFERRED,
                    operation.Syntax.GetLocation(),
                    resourceName);
                return;
        }
    }

    /// <summary>
    /// Reports owned resources whose obligations remain live at the current scope exit.
    /// </summary>
    public void ReportUndisposedResources()
    {
        CompleteOutputParameters();
        foreach (var resource in _resources.GetUniqueObjects())
        {
            ReportUndisposedResource(resource);
            if (resource.PendingAsyncDisposeLocation is { } location)
            {
                _diagnostics.ReportUnobservedAsyncDispose(location, resource.Symbol.Name);
            }
        }
    }

    /// <inheritdoc/>
    public override void VisitAwait(IAwaitOperation operation)
    {
        base.VisitAwait(operation);
        _resources.ObserveAsyncDisposeResult(operation.Operation);
        ObserveAsyncReleaseInvocations(operation.Operation);
    }

    /// <summary>
    /// Gets the resource state after all visited operations.
    /// </summary>
    /// <returns>The mutable store owned by this walker.</returns>
    internal LifetimeObjectStore GetState()
    {
        return _resources;
    }

    /// <summary>
    /// Applies the method's output ownership contract to a returned value.
    /// </summary>
    /// <param name="value">The returned expression.</param>
    /// <param name="location">The return expression location used for diagnostics.</param>
    internal void TransferReturnedValue(IOperation? value, Location location)
    {
        if (!_resources.TryResolveLifetimeObject(value, out var resource))
        {
            ValidateUntrackedReturnOwnership(value, location);
            return;
        }

        if (!ValidateReturnOwnership(resource, location))
        {
            return;
        }

        var resourceName = resource.Symbol.Name;

        if (resource.IsUsing)
        {
            _diagnostics.ReportDisposedReturn(location, resourceName);
            return;
        }

        switch (resource.TransferOwnership())
        {
            case LifetimeTransitionResultType.DISPOSED:
                _diagnostics.ReportDisposedReturn(location, resourceName);
                break;

            case LifetimeTransitionResultType.TRANSFERRED:
                _diagnostics.ReportTransfer(LifetimeTransitionResultType.TRANSFERRED, location, resourceName);
                break;
        }
    }

    /// <summary>
    /// Visits a return expression as a value use and then transfers it to the caller.
    /// </summary>
    /// <param name="value">The returned expression.</param>
    internal void VisitReturnedValue(IOperation value)
    {
        _isVisitingReturnedValue = true;
        Visit(value);
        _isVisitingReturnedValue = false;
        TransferReturnedValue(value, value.Syntax.GetLocation());
    }

    private void CheckInstanceLifetime(IInvocationOperation operation)
    {
        var ownership = LifetimeOwnershipSemantics.GetResourceOwnership(_resources.Resolve(operation.Instance), contract);
        if (IsReleaseInvocation(operation) && ownership == LifetimeResourceOwnershipType.BORROWED)
        {
            _diagnostics.ReportBorrowedDisposal(operation.Syntax.GetLocation(), GetResourceName(operation.Instance));
            return;
        }

        var symbol = GetReferencedSymbol(operation.Instance);
        if (symbol is null || !_resources.TryGet(symbol, out var resource))
        {
            return;
        }

        if (IsReleaseInvocation(operation))
        {
            if (operation.IsImplicit && resource.IsUsing && !resource.IsBorrowed)
            {
                return;
            }

            var hadDisposedState = resource.HasDisposedState;
            var result = resource.Dispose();
            var minimumLoopIterations = GetMinimumLoopIterations(operation.Syntax);
            if (minimumLoopIterations >= 1)
            {
                resource.MarkReleasedByGuaranteedLoop();
                _resources.MarkGuaranteedLoopRelease(resource);
            }

            if (result == LifetimeTransitionResultType.SUCCEEDED
                && hadDisposedState
                && minimumLoopIterations >= 2)
            {
                result = LifetimeTransitionResultType.ALREADY_DISPOSED;
            }

            if (result == LifetimeTransitionResultType.ALREADY_DISPOSED)
            {
                _resources.MarkGuaranteedLoopRelease(resource);
            }

            _diagnostics.ReportDispose(result, operation.Syntax.GetLocation(), symbol.Name);
            if (contract.IsAsynchronousRelease(operation.TargetMethod)
                && !_isVisitingReturnedValue
                && !IsAsyncReleaseObserved(operation))
            {
                resource.RegisterPendingAsyncDispose(operation.Syntax.GetLocation());
            }

            if (result == LifetimeTransitionResultType.SUCCEEDED)
            {
                ReportDeferredCallbackLifetime(resource, operation.Syntax.GetLocation());
            }

            return;
        }

        ReportInvalidUse(resource, operation.Syntax.GetLocation());
    }

    private void CheckOwnershipTransfer(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is null || !LifetimeOwnershipSemantics.IsTransferredInput(argument.Parameter))
            {
                continue;
            }

            var symbol = GetReferencedSymbol(argument.Value);
            if (symbol is null || !_resources.TryGet(symbol, out var resource))
            {
                continue;
            }

            _diagnostics.ReportTransfer(
                resource.TransferOwnership(),
                argument.Syntax.GetLocation(),
                symbol.Name);
        }
    }

    private void CheckOwnershipOutput(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            var parameter = argument.Parameter;
            if (parameter is null || !contract.IsDisposable(parameter.Type))
            {
                continue;
            }

            var ownership = LifetimeOwnershipSemantics.GetOutputOwnership(parameter);
            if (ownership is null)
            {
                continue;
            }

            var local = GetReferencedLocal(argument.Value);
            if (local is null)
            {
                continue;
            }

            ReportUndisposedResource(_resources.TryGet(local, out var resource) ? resource : null);

            _resources.Set(local, _resources.Create(
                local,
                argument.Syntax.GetLocation(),
                isUsing: false,
                isBorrowed: ownership == LifetimeResourceOwnershipType.BORROWED));
        }
    }

    private void CheckCallbackCapture(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is null
                || !LifetimeOwnershipSemantics.TryGetCallbackLifetime(argument.Parameter, out var callbackLifetime)
                || callbackLifetime == 0
                || GetCallbackLocals(argument.Value) is not { } callback)
            {
                continue;
            }

            foreach (var capturedLocal in callback.Locals)
            {
                if (!_resources.TryGet(capturedLocal, out var resource))
                {
                    continue;
                }

                if (!resource.IsUsing)
                {
                    resource.RegisterCallbackLifetime(callbackLifetime);
                    continue;
                }

                reportDiagnostic(Diagnostic.Create(
                    DisposableLifetimeDiagnostics.CallbackOutlivesResource,
                    callback.Location,
                    GetCallbackLifetimeName(callbackLifetime),
                    capturedLocal.Name));
            }
        }
    }

    private void CheckLocalFunctionRelease(IInvocationOperation operation)
    {
        if (localFunctionReleases is null
            || !localFunctionReleases.TryGetValue(operation.TargetMethod, out var releasedLocals))
        {
            return;
        }

        foreach (var local in releasedLocals)
        {
            if (!_resources.TryGet(local, out var resource))
            {
                continue;
            }

            _diagnostics.ReportDispose(resource.Dispose(), operation.Syntax.GetLocation(), local.Name);
        }
    }

    private void ReportInvalidUse(LifetimeObject resource, Location location)
    {
        _diagnostics.ReportUse(resource.Use(), location, resource.Symbol.Name);
    }

    private bool ValidateReturnOwnership(LifetimeObject resource, Location location)
    {
        if (owningMethod is null)
        {
            return !resource.IsBorrowed;
        }

        var expectedOwnership = LifetimeOwnershipSemantics.GetReturnOwnership(owningMethod);
        var isValid = resource.IsBorrowed
            ? expectedOwnership == LifetimeResourceOwnershipType.BORROWED
            : expectedOwnership == LifetimeResourceOwnershipType.OWNED;
        if (isValid)
        {
            return !resource.IsBorrowed;
        }

        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.InvalidOwnershipContract,
            location,
            owningMethod.AssociatedSymbol?.Name ?? owningMethod.Name));
        if (resource.IsBorrowed)
        {
            return false;
        }

        resource.TransferOwnership();

        return false;
    }

    private void ValidateUntrackedReturnOwnership(IOperation? value, Location location)
    {
        if (owningMethod is null
            || LifetimeOwnershipSemantics.GetResourceOwnership(_resources.Resolve(value), contract) is not { } actualOwnership
            || actualOwnership == LifetimeOwnershipSemantics.GetReturnOwnership(owningMethod))
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.InvalidOwnershipContract,
            location,
            owningMethod.AssociatedSymbol?.Name ?? owningMethod.Name));
    }

    private void ReportReferencedLocalUse(IOperation? operation, Location location)
    {
        var symbol = GetReferencedSymbol(operation);
        if (symbol is null || !_resources.TryGet(symbol, out var resource))
        {
            return;
        }

        ReportInvalidUse(resource, location);
    }

    private void ReportUndisposedResource(LifetimeObject? resource)
    {
        if (resource is null
            || _resources.IsGuaranteedReleased(resource)
            || resource.CompleteScope() != LifetimeTransitionResultType.OWNERSHIP_LOSS)
        {
            return;
        }

        _diagnostics.ReportUndisposed(resource);
    }

    private void ReportOverwrite(LifetimeObject? resource, Location location)
    {
        if (resource is null || resource.Overwrite() != LifetimeTransitionResultType.OWNERSHIP_LOSS)
        {
            return;
        }

        _diagnostics.ReportOverwrite(resource, location);
    }

    private void ReportDeferredCallbackLifetime(LifetimeObject resource, Location location)
    {
        if (resource.CallbackLifetime is not { } callbackLifetime)
        {
            return;
        }

        _diagnostics.ReportCallbackOutlivesResource(location, GetCallbackLifetimeName(callbackLifetime), resource.Symbol.Name);
    }

    private void MarkUsingResource(IOperation resources)
    {
        var collector = new LocalReferenceCollector();
        collector.Visit(resources);
        foreach (var local in collector.Locals)
        {
            if (_resources.TryGet(local, out var resource))
            {
                resource.MarkUsing();
            }
        }
    }

    private bool TryCreateLifetimeObject(IVariableDeclaratorOperation declaration, out LifetimeObject? lifetimeObject)
    {
        return TryCreateLifetimeObject(
            declaration.Symbol,
            declaration.Initializer?.Value,
            declaration.Syntax.GetLocation(),
            IsUsingDeclaration(declaration.Symbol),
            out lifetimeObject);
    }

    private bool TryCreateLifetimeObject(
        ISymbol symbol,
        IOperation? value,
        Location creationLocation,
        bool isUsing,
        out LifetimeObject? lifetimeObject)
    {
        var type = symbol switch
        {
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
        if (type is null
            || !contract.IsDisposable(type)
            || LifetimeOwnershipSemantics.GetResourceOwnership(_resources.Resolve(value), contract) is not { } ownership)
        {
            lifetimeObject = null;
            return false;
        }

        lifetimeObject = _resources.Create(
            symbol,
            creationLocation,
            isUsing || (symbol is ILocalSymbol usingLocal && _resources.IsUsingLocal(usingLocal)),
            isBorrowed: ownership == LifetimeResourceOwnershipType.BORROWED);
        return true;
    }

    private void CompleteOutputParameters()
    {
        if (owningMethod is null)
        {
            return;
        }

        foreach (var parameter in owningMethod.Parameters)
        {
            if (LifetimeOwnershipSemantics.GetOutputOwnership(parameter) is not { } expectedOwnership
                || !_resources.TryGet(parameter, out var resource))
            {
                continue;
            }

            var isValid = resource.IsBorrowed
                ? expectedOwnership == LifetimeResourceOwnershipType.BORROWED
                : expectedOwnership == LifetimeResourceOwnershipType.OWNED;
            if (!isValid)
            {
                reportDiagnostic(Diagnostic.Create(
                    DisposableLifetimeDiagnostics.InvalidOwnershipContract,
                    parameter.Locations.FirstOrDefault() ?? Location.None,
                    parameter.Name));
            }

            if (!resource.IsBorrowed)
            {
                resource.TransferOwnership();
            }
        }
    }

    private static bool IsUsingDeclaration(ILocalSymbol local)
    {
        if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax declaration)
        {
            return false;
        }

        return declaration.AncestorsAndSelf().Any(node =>
            node is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0, }
                or UsingStatementSyntax);
    }

    private bool IsReleaseInvocation(IInvocationOperation operation)
    {
        return contract.IsSynchronousRelease(operation.TargetMethod)
            || contract.IsAsynchronousRelease(operation.TargetMethod);
    }

    private static bool IsAsyncReleaseObserved(IInvocationOperation operation)
    {
        IOperation current = operation;
        while (current.Parent is IConversionOperation conversion)
        {
            current = conversion;
        }

        if (current.Parent is IAwaitOperation or IReturnOperation)
        {
            return true;
        }

        while (current.Parent is IInvocationOperation { TargetMethod.Name: "ConfigureAwait" or "AsTask" or "Preserve", }
               forwardingInvocation)
        {
            current = forwardingInvocation;
            while (current.Parent is IConversionOperation conversion)
            {
                current = conversion;
            }
        }

        return current.Parent is IAwaitOperation or IReturnOperation;
    }

    /// <summary>
    /// Computes a conservative lower bound for a simple counted <see langword="for"/> loop.
    /// </summary>
    /// <param name="syntax">An operation inside the loop body.</param>
    /// <returns>The proven iteration count, or zero when the loop shape is not recognized.</returns>
    internal static int GetMinimumLoopIterations(SyntaxNode syntax)
    {
        var loop = syntax.Ancestors().OfType<ForStatementSyntax>().FirstOrDefault();
        if (loop is null
            || syntax.Ancestors().TakeWhile(node => node != loop).Any(node =>
                node is IfStatementSyntax
                    or SwitchStatementSyntax
                    or ConditionalExpressionSyntax
                    or TryStatementSyntax)
            || loop.Declaration is not { Variables.Count: 1, } declaration
            || declaration.Variables[0] is not { Initializer.Value: LiteralExpressionSyntax startLiteral, } variable
            || !int.TryParse(startLiteral.Token.ValueText, out var start)
            || loop.Condition is not BinaryExpressionSyntax condition
            || condition.Left is not IdentifierNameSyntax conditionVariable
            || condition.Right is not LiteralExpressionSyntax limitLiteral
            || !int.TryParse(limitLiteral.Token.ValueText, out var limit)
            || conditionVariable.Identifier.ValueText != variable.Identifier.ValueText
            || loop.Incrementors.Count != 1
            || !IsUnitIncrement(loop.Incrementors[0], variable.Identifier.ValueText))
        {
            return 0;
        }

        return condition.OperatorToken.ValueText switch
        {
            "<" => Math.Max(0, limit - start),
            "<=" => Math.Max(0, limit - start + 1),
            _ => 0,
        };
    }

    private static bool IsUnitIncrement(ExpressionSyntax expression, string variableName)
    {
        if (expression is PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax postfixIdentifier, } postfix
            && postfix.IsKind(SyntaxKind.PostIncrementExpression))
        {
            return postfixIdentifier.Identifier.ValueText == variableName;
        }

        if (expression is PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax prefixIdentifier, } prefix
            && prefix.IsKind(SyntaxKind.PreIncrementExpression))
        {
            return prefixIdentifier.Identifier.ValueText == variableName;
        }

        if (expression is not AssignmentExpressionSyntax assignment
            || assignment.Left is not IdentifierNameSyntax identifier
            || assignment.Right is not LiteralExpressionSyntax literal)
        {
            return false;
        }

        return assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            && identifier.Identifier.ValueText == variableName
            && literal.Token.Value is 1;
    }

    private void RegisterAsyncDisposeResult(ILocalSymbol resultLocal, IOperation? value)
    {
        if (_resources.TryGetAsyncDisposeResult(value, out var existingResource))
        {
            _resources.SetAsyncDisposeResult(resultLocal, existingResource);
            return;
        }

        if (GetAsyncReleaseInvocation(value) is not { } release
            || GetReferencedSymbol(release.Instance) is not { } resourceSymbol
            || !_resources.TryGet(resourceSymbol, out var resource))
        {
            return;
        }

        _resources.SetAsyncDisposeResult(resultLocal, resource);
    }

    private void ObserveAsyncReleaseInvocations(IOperation operation)
    {
        var collector = new InvocationCollector();
        collector.Visit(operation);
        foreach (var invocation in collector.Invocations)
        {
            if (!contract.IsAsynchronousRelease(invocation.TargetMethod)
                || GetReferencedSymbol(invocation.Instance) is not { } resourceSymbol
                || !_resources.TryGet(resourceSymbol, out var resource))
            {
                continue;
            }

            resource.ObserveAsyncDispose();
        }
    }

    private IInvocationOperation? GetAsyncReleaseInvocation(IOperation? operation)
    {
        operation = _resources.Resolve(operation);
        return operation switch
        {
            IInvocationOperation invocation when contract.IsAsynchronousRelease(invocation.TargetMethod) => invocation,
            IInvocationOperation { TargetMethod.Name: "ConfigureAwait" or "AsTask" or "Preserve", Instance: { } instance, } =>
                GetAsyncReleaseInvocation(instance),
            IConversionOperation conversion => GetAsyncReleaseInvocation(conversion.Operand),
            _ => null,
        };
    }

    private string GetResourceName(IOperation? operation)
    {
        return GetPropertyReference(operation)?.Property.Name
            ?? GetFieldReference(operation)?.Field.Name
            ?? GetReferencedSymbol(operation)?.Name
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

    private CallbackCapture? GetCallbackLocals(IOperation operation)
    {
        operation = _resources.Resolve(operation) ?? operation;
        if (GetAnonymousFunction(operation) is { } callback)
        {
            var locals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
            new LocalReferenceCollector(locals).Visit(callback.Body);
            return new(locals, callback.Syntax.GetLocation());
        }

        if (controlFlowGraph is null || GetFlowAnonymousFunction(operation) is not { } flowCallback)
        {
            return null;
        }

        var capturedLocals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        var collector = new LocalReferenceCollector(capturedLocals);
        var callbackGraph = controlFlowGraph.GetAnonymousFunctionControlFlowGraph(flowCallback, cancellationToken);
        foreach (var block in callbackGraph.Blocks)
        {
            foreach (var blockOperation in block.Operations)
            {
                collector.Visit(blockOperation);
            }

            collector.Visit(block.BranchValue);
        }

        return new(capturedLocals, flowCallback.Syntax.GetLocation());
    }

    private static IFlowAnonymousFunctionOperation? GetFlowAnonymousFunction(IOperation operation)
    {
        return operation switch
        {
            IFlowAnonymousFunctionOperation callback => callback,
            IDelegateCreationOperation delegateCreation => GetFlowAnonymousFunction(delegateCreation.Target),
            IConversionOperation conversion => GetFlowAnonymousFunction(conversion.Operand),
            _ => null,
        };
    }

    private ILocalSymbol? GetReferencedLocal(IOperation? operation)
    {
        return LifetimeOwnershipSemantics.GetReferencedLocal(_resources.Resolve(operation));
    }

    private ISymbol? GetReferencedSymbol(IOperation? operation)
    {
        return LifetimeOwnershipSemantics.GetReferencedSymbol(_resources.Resolve(operation));
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

    private static LocalizableResourceString GetCallbackLifetimeName(int callbackLifetime)
    {
        return callbackLifetime == 1
            ? DiagnosticResources.Get("CallbackLifetimeDeferred")
            : DiagnosticResources.Get("CallbackLifetimeSubscription");
    }

    /// <summary>
    /// Collects local references without descending into nested callbacks.
    /// </summary>
    /// <param name="locals">An optional existing result set.</param>
    private sealed class LocalReferenceCollector(HashSet<ILocalSymbol>? locals = null) : OperationWalker
    {
        /// <summary>
        /// Gets the referenced locals.
        /// </summary>
        internal HashSet<ILocalSymbol> Locals { get; } = locals ?? new(SymbolEqualityComparer.Default);

        /// <inheritdoc/>
        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
        {
            // A nested callback owns its own captures.
        }

        /// <inheritdoc/>
        public override void VisitLocalReference(ILocalReferenceOperation operation)
        {
            Locals.Add(operation.Local);
            base.VisitLocalReference(operation);
        }
    }

    /// <summary>
    /// Collects invocations nested in an expression.
    /// </summary>
    private sealed class InvocationCollector : OperationWalker
    {
        /// <summary>
        /// Gets the collected invocations.
        /// </summary>
        internal List<IInvocationOperation> Invocations { get; } = [];

        /// <inheritdoc/>
        public override void VisitInvocation(IInvocationOperation operation)
        {
            Invocations.Add(operation);
            base.VisitInvocation(operation);
        }
    }

    /// <summary>
    /// Pairs a callback's captured locals with its argument location.
    /// </summary>
    /// <param name="locals">The locals captured by the callback.</param>
    /// <param name="location">The callback argument location.</param>
    private sealed class CallbackCapture(HashSet<ILocalSymbol> locals, Location location)
    {
        internal HashSet<ILocalSymbol> Locals { get; } = locals;

        internal Location Location { get; } = location;
    }
}