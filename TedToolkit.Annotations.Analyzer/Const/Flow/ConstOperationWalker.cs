// -----------------------------------------------------------------------
// <copyright file="ConstOperationWalker.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Const.Contracts;

namespace TedToolkit.Annotations.Analyzer.Const.Flow;

/// <summary>
/// Applies const mutation checks while advancing the alias state through one CFG block.
/// </summary>
/// <param name="owningSymbol">The method or accessor whose body is being analyzed.</param>
/// <param name="state">The input alias state for the CFG block.</param>
/// <param name="reportDiagnostic">The optional diagnostic callback.</param>
internal sealed class ConstOperationWalker(
    ISymbol owningSymbol,
    ConstAliasState state,
    Action<Diagnostic>? reportDiagnostic) : OperationWalker
{
    /// <summary>
    /// Gets the alias state after the operations visited so far.
    /// </summary>
    internal ConstAliasState State { get; } = state;

    /// <inheritdoc />
    public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
    {
        base.VisitVariableDeclarator(operation);
        State.SetAliases(operation.Symbol, ImmutableArray<ConstMutationTarget>.Empty);

        if (operation.Initializer?.Value is not { } initializer)
        {
            return;
        }

        if (initializer is IInvocationOperation invocation && ConstContractResolver.IsConstLocal(invocation.TargetMethod))
        {
            if (TryGetConstLocalDepths(invocation, out var depths))
            {
                State.SetLocalDepths(operation.Symbol, depths);
                State.SetAliases(operation.Symbol, ResolveAssignmentTargets(operation.Symbol, GetValueArgument(invocation)));
            }

            return;
        }

        State.SetAliases(operation.Symbol, ResolveAssignmentTargets(operation.Symbol, initializer));
    }

    /// <inheritdoc />
    public override void VisitFlowCapture(IFlowCaptureOperation operation)
    {
        base.VisitFlowCapture(operation);
        State.SetCapture(operation.Id, ResolveTargets(operation.Value));
    }

    /// <inheritdoc />
    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
    {
        if (operation.IsRef)
        {
            // A ref assignment changes the storage identity itself; treating it as a value copy loses later writes.
            Visit(operation.Target);
            Visit(operation.Value);
            RebindRefAlias(operation);
            return;
        }

        Visit(operation.Target);
        AnalyzeMutationTarget(operation.Target);
        Visit(operation.Value);
        UpdateLocalAlias(operation);
    }

    /// <inheritdoc />
    public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation)
    {
        base.VisitCompoundAssignment(operation);
        AnalyzeMutationTarget(operation.Target);
    }

    /// <inheritdoc />
    public override void VisitCoalesceAssignment(ICoalesceAssignmentOperation operation)
    {
        base.VisitCoalesceAssignment(operation);
        AnalyzeMutationTarget(operation.Target);
    }

    /// <inheritdoc />
    public override void VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation)
    {
        base.VisitDeconstructionAssignment(operation);
        AnalyzeMutationTarget(operation.Target);
        UpdateDeconstructionAliases(operation.Target, operation.Value);
    }

    /// <inheritdoc />
    public override void VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation)
    {
        base.VisitIncrementOrDecrement(operation);
        AnalyzeMutationTarget(operation.Target);
    }

    /// <inheritdoc />
    public override void VisitEventAssignment(IEventAssignmentOperation operation)
    {
        base.VisitEventAssignment(operation);
        AnalyzeMutationTarget(operation.EventReference);
    }

    /// <inheritdoc />
    public override void VisitArgument(IArgumentOperation operation)
    {
        base.VisitArgument(operation);
        if (!(operation.Parameter?.RefKind is RefKind.Ref or RefKind.Out))
        {
            return;
        }

        AnalyzeMutationTarget(operation.Value);
    }

    /// <inheritdoc />
    public override void VisitInvocation(IInvocationOperation operation)
    {
        base.VisitInvocation(operation);
        if (ConstContractResolver.IsConstLocal(operation.TargetMethod))
        {
            if (IsValidConstLocalInitializer(operation) && TryGetConstLocalDepths(operation, out _))
            {
                return;
            }

            reportDiagnostic?.Invoke(Diagnostic.Create(
                ConstMutationAnalyzer.InvalidLocal,
                operation.Syntax.GetLocation()));
            return;
        }

        if (operation.IsImplicit)
        {
            return;
        }

        AnalyzeInvocationContracts(operation);
    }

    /// <inheritdoc />
    public override void VisitForEachLoop(IForEachLoopOperation operation)
    {
        Visit(operation.Collection);
        var elementTargets = AddDepth(ResolveTargets(operation.Collection), 1);
        SetForEachAliases(operation.LoopControlVariable, elementTargets);
        Visit(operation.Body);
        foreach (var nextVariable in operation.NextVariables)
        {
            Visit(nextVariable);
        }
    }

    private void AnalyzeMutationTarget(IOperation target)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
            {
                AnalyzeMutationTarget(element);
            }

            return;
        }

        var mutationTargets = target is ILocalReferenceOperation { Local.RefKind: RefKind.None, } localReference
            ? ImmutableArray.Create(new ConstMutationTarget(localReference.Local, 0))
            : ResolveTargets(target);
        foreach (var mutationTarget in mutationTargets)
        {
            if (mutationTarget.RequiresReferenceBoundary
                || mutationTarget.Depth >= 32
                || !TryGetProtectedDepths(mutationTarget.Symbol, out var protectedDepths)
                || (protectedDepths & (1U << mutationTarget.Depth)) == 0)
            {
                continue;
            }

            reportDiagnostic?.Invoke(Diagnostic.Create(
                ConstMutationAnalyzer.MutationNotAllowed,
                target.Syntax.GetLocation(),
                mutationTarget.Depth,
                mutationTarget.Symbol.Name));
        }
    }

    private void AnalyzeInvocationContracts(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.MethodKind == MethodKind.DelegateInvoke)
        {
            return;
        }

        if (invocation.Instance is { } instance)
        {
            AnalyzeCallContract(
                invocation.TargetMethod,
                instance,
                invocation.Syntax.GetLocation(),
                isReceiver: true);
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter
                || parameter.RefKind is RefKind.Ref or RefKind.Out)
            {
                continue;
            }

            AnalyzeCallContract(
                parameter,
                argument.Value,
                argument.Syntax.GetLocation(),
                isReceiver: false,
                invocation.TargetMethod);
        }
    }

    private void AnalyzeCallContract(
        ISymbol calledContract,
        IOperation value,
        Location location,
        bool isReceiver,
        IMethodSymbol? calledMethod = null)
    {
        calledMethod ??= (IMethodSymbol)calledContract;
        var availableDepths = ConstContractResolver.TryGetConstDepths(calledContract, out var resolvedDepths)
            ? resolvedDepths
            : 0;

        foreach (var target in ResolveTargets(value))
        {
            if (target.Depth >= 32 || !TryGetProtectedDepths(target.Symbol, out var protectedDepths))
            {
                continue;
            }

            var requiredDepths = GetRequiredCallDepths(protectedDepths, target, isReceiver);
            if (requiredDepths == 0 || (availableDepths & requiredDepths) == requiredDepths)
            {
                continue;
            }

            var descriptor = calledMethod.Locations.Any(candidate => candidate.IsInSource)
                ? ConstMutationAnalyzer.SourceCallRequiresConst
                : ConstMutationAnalyzer.ExternalCallRequiresConst;
            reportDiagnostic?.Invoke(Diagnostic.Create(
                descriptor,
                location,
                calledMethod.Name,
                target.Symbol.Name));
        }
    }

    private static uint GetRequiredCallDepths(
        uint protectedDepths,
        in ConstMutationTarget target,
        bool isReceiver)
    {
        var shift = target.Depth + (isReceiver ? 1 : 0);
        if (shift >= 32)
        {
            return 0;
        }

        var requiredDepths = protectedDepths >> shift;
        if (!isReceiver)
        {
            requiredDepths &= ~1U;
        }

        if (target.RequiresReferenceBoundary)
        {
            requiredDepths &= isReceiver ? ~1U : ~3U;
        }

        return requiredDepths;
    }

    private void UpdateLocalAlias(ISimpleAssignmentOperation operation)
    {
        if (operation.Target is not ILocalReferenceOperation localReference)
        {
            return;
        }

        if (operation.Value is IInvocationOperation invocation
            && ConstContractResolver.IsConstLocal(invocation.TargetMethod))
        {
            if (TryGetConstLocalDepths(invocation, out var depths))
            {
                State.SetLocalDepths(localReference.Local, depths);
                State.SetAliases(
                    localReference.Local,
                    ResolveAssignmentTargets(localReference.Local, GetValueArgument(invocation)));
            }

            return;
        }

        if (localReference.Local.RefKind != RefKind.None)
        {
            return;
        }

        State.SetAliases(localReference.Local, ResolveAssignmentTargets(localReference.Local, operation.Value));
    }

    private void UpdateDeconstructionAliases(IOperation target, IOperation value)
    {
        target = UnwrapConversion(target);
        value = UnwrapConversion(value);

        if (target is IDeclarationExpressionOperation declaration)
        {
            var declaredValue = declaration.ChildOperations.SingleOrDefault();
            if (declaredValue is not null)
            {
                UpdateDeconstructionAliases(declaredValue, value);
            }

            return;
        }

        if (target is ITupleOperation targetTuple && value is ITupleOperation valueTuple)
        {
            for (var index = 0; index < Math.Min(targetTuple.Elements.Length, valueTuple.Elements.Length); index++)
            {
                UpdateDeconstructionAliases(targetTuple.Elements[index], valueTuple.Elements[index]);
            }

            return;
        }

        if (target is not ILocalReferenceOperation localReference || localReference.Local.RefKind != RefKind.None)
        {
            return;
        }

        State.SetAliases(localReference.Local, ResolveAssignmentTargets(localReference.Local, value));
    }

    private ImmutableArray<ConstMutationTarget> ResolveTargets(IOperation? operation, int depth = 0)
    {
        return ResolveTargets(
            operation,
            depth,
            crossedReferenceBoundary: false,
            new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default));
    }

    private ImmutableArray<ConstMutationTarget> ResolveTargets(
        IOperation? operation,
        int depth,
        bool crossedReferenceBoundary,
        HashSet<ILocalSymbol> visitedLocals)
    {
        switch (operation)
        {
            case null:
                return ImmutableArray<ConstMutationTarget>.Empty;

            case IConversionOperation conversion:
                return ResolveTargets(conversion.Operand, depth, crossedReferenceBoundary, visitedLocals);

            case IParenthesizedOperation parenthesized:
                return ResolveTargets(parenthesized.Operand, depth, crossedReferenceBoundary, visitedLocals);

            case IArrayElementReferenceOperation arrayElement:
                return ResolveTargets(
                    arrayElement.ArrayReference,
                    depth + 1,
                    crossedReferenceBoundary || arrayElement.ArrayReference.Type?.IsReferenceType == true,
                    visitedLocals);

            case IPropertyReferenceOperation propertyReference when propertyReference.Instance is not null:
                return ResolveTargets(
                    propertyReference.Instance,
                    depth + 1,
                    crossedReferenceBoundary || propertyReference.Instance.Type?.IsReferenceType == true,
                    visitedLocals);

            case IFieldReferenceOperation fieldReference when fieldReference.Instance is not null:
                return ResolveTargets(
                    fieldReference.Instance,
                    depth + 1,
                    crossedReferenceBoundary || fieldReference.Instance.Type?.IsReferenceType == true,
                    visitedLocals);

            case IEventReferenceOperation eventReference when eventReference.Instance is not null:
                return ResolveTargets(
                    eventReference.Instance,
                    depth + 1,
                    crossedReferenceBoundary || eventReference.Instance.Type?.IsReferenceType == true,
                    visitedLocals);

            case IConditionalOperation conditional:
                return ConstAliasState.Union(
                    ResolveTargets(
                        conditional.WhenTrue,
                        depth,
                        crossedReferenceBoundary,
                        new(visitedLocals, SymbolEqualityComparer.Default)),
                    ResolveTargets(
                        conditional.WhenFalse,
                        depth,
                        crossedReferenceBoundary,
                        new(visitedLocals, SymbolEqualityComparer.Default)));

            case ICoalesceOperation coalesce:
                return ConstAliasState.Union(
                    ResolveTargets(
                        coalesce.Value,
                        depth,
                        crossedReferenceBoundary,
                        new(visitedLocals, SymbolEqualityComparer.Default)),
                    ResolveTargets(
                        coalesce.WhenNull,
                        depth,
                        crossedReferenceBoundary,
                        new(visitedLocals, SymbolEqualityComparer.Default)));

            case IInvocationOperation { IsImplicit: true, TargetMethod.Name: "GetEnumerator", Instance: { } instance, }:
                return ResolveTargets(instance, depth, crossedReferenceBoundary, visitedLocals);

            case IFlowCaptureReferenceOperation captureReference:
                return ApplyPath(State.GetCapture(captureReference.Id), depth, crossedReferenceBoundary);

            case IParameterReferenceOperation parameterReference:
                return ImmutableArray.Create(new ConstMutationTarget(parameterReference.Parameter, depth));

            case ILocalReferenceOperation localReference:
                return ResolveLocal(localReference.Local, depth, crossedReferenceBoundary, visitedLocals);

            case IInstanceReferenceOperation:
                return ImmutableArray.Create(new ConstMutationTarget(owningSymbol, Math.Max(0, depth - 1)));

            default:
                return ImmutableArray<ConstMutationTarget>.Empty;
        }
    }

    private ImmutableArray<ConstMutationTarget> ResolveLocal(
        ILocalSymbol local,
        int depth,
        bool crossedReferenceBoundary,
        HashSet<ILocalSymbol> visitedLocals)
    {
        var aliases = State.GetAliases(local);
        var targets = ApplyPath(aliases, depth, crossedReferenceBoundary);
        if (State.TryGetLocalDepths(local, out _))
        {
            targets = ConstAliasState.Union(
                targets,
                ImmutableArray.Create(new ConstMutationTarget(local, depth)));
        }

        if (!visitedLocals.Add(local))
        {
            return targets;
        }

        return targets.IsEmpty
            ? ImmutableArray.Create(new ConstMutationTarget(local, depth))
            : targets;
    }

    private bool TryGetProtectedDepths(ISymbol symbol, out uint depths)
    {
        if (symbol is ILocalSymbol local && State.TryGetLocalDepths(local, out depths))
        {
            return true;
        }

        if (symbol is IParameterSymbol { RefKind: RefKind.Out, })
        {
            depths = default;
            return false;
        }

        if (TryGetAccessorDepths(symbol, out depths))
        {
            return true;
        }

        return ConstContractResolver.TryGetConstDepths(symbol, out depths);
    }

    private static bool TryGetAccessorDepths(ISymbol symbol, out uint depths)
    {
        if (symbol is not IMethodSymbol accessor
            || accessor.MethodKind is not (MethodKind.PropertyGet or MethodKind.PropertySet)
            || accessor.AssociatedSymbol is not IPropertySymbol)
        {
            depths = default;
            return false;
        }

        if (ConstContractResolver.TryGetAccessorDepths(accessor, out depths))
        {
            return true;
        }

        depths = accessor.MethodKind == MethodKind.PropertyGet
            ? uint.MaxValue
            : (uint.MaxValue << 1);
        return true;
    }

    private static IOperation UnwrapConversion(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsValidConstLocalInitializer(IInvocationOperation invocation)
    {
        return invocation.Parent is IVariableInitializerOperation
        {
            Parent: IVariableDeclaratorOperation,
        }

               || (invocation.Parent is ISimpleAssignmentOperation
               {
                   Target: ILocalReferenceOperation,
               }

               && IsDirectInitializerSyntax(invocation.Syntax));
    }

    private static bool IsDirectInitializerSyntax(SyntaxNode invocationSyntax)
    {
        var valueSyntax = invocationSyntax;
        if (valueSyntax.Parent is RefExpressionSyntax refExpression)
        {
            valueSyntax = refExpression;
        }

        if (valueSyntax.Parent is not EqualsValueClauseSyntax equalsValue
            || equalsValue.Parent is not VariableDeclaratorSyntax)
        {
            return false;
        }

        return equalsValue.Value == valueSyntax;
    }

    private static bool TryGetConstLocalDepths(IInvocationOperation invocation, out uint depths)
    {
        var depthArgument = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1);
        if (depthArgument is null)
        {
            depths = uint.MaxValue;
            return true;
        }

        if (depthArgument.Value.ConstantValue.Value is uint value)
        {
            depths = value;
            return true;
        }

        depths = default;
        return false;
    }

    private static ImmutableArray<ConstMutationTarget> AddDepth(
        in ImmutableArray<ConstMutationTarget> targets,
        int depth)
    {
        if (depth == 0 || targets.IsEmpty)
        {
            return targets;
        }

        return targets
            .Select(target => new ConstMutationTarget(
                target.Symbol,
                target.Depth + depth,
                target.RequiresReferenceBoundary))
            .ToImmutableArray();
    }

    private static ImmutableArray<ConstMutationTarget> ApplyPath(
        in ImmutableArray<ConstMutationTarget> targets,
        int depth,
        bool crossedReferenceBoundary)
    {
        if (!crossedReferenceBoundary)
        {
            return AddDepth(targets, depth);
        }

        return targets
            .Select(target => new ConstMutationTarget(target.Symbol, target.Depth + depth))
            .ToImmutableArray();
    }

    private ImmutableArray<ConstMutationTarget> ResolveAssignmentTargets(ILocalSymbol local, IOperation? value)
    {
        var targets = ResolveTargets(value);
        if (local.RefKind != RefKind.None || !local.Type.IsValueType)
        {
            return targets;
        }

        return targets
            .Select(target => new ConstMutationTarget(target.Symbol, target.Depth, requiresReferenceBoundary: true))
            .ToImmutableArray();
    }

    private static IOperation? GetValueArgument(IInvocationOperation invocation)
    {
        return invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
    }

    private void RebindRefAlias(ISimpleAssignmentOperation operation)
    {
        if (operation.Target is not ILocalReferenceOperation { Local.RefKind: not RefKind.None, } localReference)
        {
            return;
        }

        if (operation.Value is IInvocationOperation invocation
            && ConstContractResolver.IsConstLocal(invocation.TargetMethod)
            && TryGetConstLocalDepths(invocation, out var depths))
        {
            State.SetLocalDepths(localReference.Local, depths);
            State.SetAliases(localReference.Local, ResolveTargets(GetValueArgument(invocation)));
            return;
        }

        State.SetAliases(localReference.Local, ResolveTargets(operation.Value));
    }

    private void SetForEachAliases(IOperation loopControlVariable, in ImmutableArray<ConstMutationTarget> targets)
    {
        switch (loopControlVariable)
        {
            case IVariableDeclaratorOperation declarator
                when declarator.Symbol.RefKind != RefKind.None || declarator.Symbol.Type.IsReferenceType:
                State.SetAliases(declarator.Symbol, targets);
                break;

            case ILocalReferenceOperation localReference when localReference.Local.Type.IsReferenceType:
                State.SetAliases(localReference.Local, targets);
                break;

            case IDeclarationExpressionOperation declaration:
                foreach (var child in declaration.ChildOperations)
                {
                    SetForEachAliases(child, targets);
                }

                break;
        }
    }
}