// -----------------------------------------------------------------------
// <copyright file="OwnedMemberLifetimeAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Members;

/// <summary>
/// Verifies that disposable members owned by a type are released exactly once.
/// </summary>
internal static class OwnedMemberLifetimeAnalysis
{
    /// <summary>
    /// Registers owned-member lifetime analysis.
    /// </summary>
    /// <param name="context">The compilation analysis context.</param>
    /// <param name="contract">The available disposal contracts.</param>
    internal static void Register(CompilationStartAnalysisContext context, DisposableContract contract)
    {
        context.RegisterSymbolStartAction(symbolStartContext =>
        {
            if (symbolStartContext.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct, } type)
            {
                return;
            }

            var state = new State(type, contract);
            symbolStartContext.RegisterOperationBlockAction(blockContext => state.Analyze(blockContext));
            symbolStartContext.RegisterSymbolEndAction(symbolEndContext => state.Report(symbolEndContext.ReportDiagnostic));
        }, SymbolKind.NamedType);
    }

    /// <summary>
    /// Accumulates thread-safe per-type facts from concurrently analyzed operation blocks.
    /// </summary>
    private sealed class State
    {
        private readonly DisposableContract _contract;

        private readonly HashSet<IMethodSymbol> _releaseMethods = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _borrowedFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _candidateFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _explicitlyOwnedFields = new(SymbolEqualityComparer.Default);

        private readonly HashSet<IFieldSymbol> _inferredOwnedFields = new(SymbolEqualityComparer.Default);

        private readonly Dictionary<IMethodSymbol, ReleasedMemberDataFlow.ReleasedMembers> _releaseSummaries =
            new(SymbolEqualityComparer.Default);

        private readonly List<(IFieldSymbol Field, Location Location)> _overwrittenFields = [];

        private readonly HashSet<IPropertySymbol> _ownedProperties = new(SymbolEqualityComparer.Default);

        private readonly List<(IPropertySymbol Property, Location Location)> _overwrittenProperties = [];

        private readonly List<(ISymbol Member, Location Location)> _doubleReleases = [];

        private readonly object _gate = new();

        private readonly INamedTypeSymbol _type;

        internal State(INamedTypeSymbol type, DisposableContract contract)
        {
            _type = type;
            _contract = contract;
            if (contract.GetSynchronousReleaseMethod(type) is { } disposeMethod)
            {
                _releaseMethods.Add(disposeMethod);
            }

            if (contract.GetAsynchronousReleaseMethod(type) is { } disposeAsyncMethod)
            {
                _releaseMethods.Add(disposeAsyncMethod);
            }

            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic
                    || (!contract.IsDisposable(field.Type)
                        && !OwnershipResourceShape.CanCarryDisposableResource(field.Type, contract)))
                {
                    continue;
                }

                _candidateFields.Add(field);
                if (LifetimeOwnershipSemantics.IsExplicitlyOwned(field))
                {
                    _explicitlyOwnedFields.Add(field);
                }
                else if (LifetimeOwnershipSemantics.IsExplicitlyBorrowed(field))
                {
                    _borrowedFields.Add(field);
                }
            }

            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic
                    && property.SetMethod is not null
                    && contract.IsDisposable(property.Type)
                    && LifetimeOwnershipSemantics.IsExplicitlyOwned(property))
                {
                    _ownedProperties.Add(property);
                }
            }
        }

        internal void Analyze(in OperationBlockAnalysisContext context)
        {
            if (context.OwningSymbol is not IMethodSymbol method)
            {
                var initializerCollector = new OwnedFieldAssignmentCollector(_candidateFields, _contract);
                VisitOperationBlocks(context.OperationBlocks, initializerCollector);
                lock (_gate)
                {
                    _inferredOwnedFields.UnionWith(initializerCollector.OwnedFields);
                }

                return;
            }

            var releaseSummary = ReleasedMemberDataFlow.Analyze(
                context.OperationBlocks,
                _candidateFields,
                _ownedProperties,
                _type,
                _contract,
                context.CancellationToken);
            lock (_gate)
            {
                _releaseSummaries[method] = releaseSummary;
                _doubleReleases.AddRange(releaseSummary.DoubleReleases);
            }

            if (method.MethodKind == MethodKind.Constructor && SymbolEqualityComparer.Default.Equals(method.ContainingType, _type))
            {
                var collector = new OwnedFieldAssignmentCollector(_candidateFields, _contract);
                VisitOperationBlocks(context.OperationBlocks, collector);
                lock (_gate)
                {
                    _inferredOwnedFields.UnionWith(collector.OwnedFields);
                }

                return;
            }

            if (_releaseMethods.Contains(method))
            {
                return;
            }

            var overwrites = OwnedMemberOverwriteDataFlow.Analyze(
                context.OperationBlocks,
                _candidateFields,
                _ownedProperties,
                _type,
                _contract,
                context.CancellationToken);
            lock (_gate)
            {
                _overwrittenFields.AddRange(overwrites.OverwrittenFields);
                _overwrittenProperties.AddRange(overwrites.OverwrittenProperties);
            }
        }

        internal void Report(Action<Diagnostic> reportDiagnostic)
        {
            lock (_gate)
            {
                var ownedFields = new HashSet<IFieldSymbol>(_explicitlyOwnedFields, SymbolEqualityComparer.Default);
                ownedFields.UnionWith(_inferredOwnedFields);
                ownedFields.ExceptWith(_borrowedFields);
                var releasedFields = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
                var releasedProperties = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
                var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                foreach (var releaseMethod in _releaseMethods)
                {
                    CollectReleaseEffects(releaseMethod, visitedMethods, releasedFields, releasedProperties);
                }

                if (ownedFields.Count == 0 && _ownedProperties.Count == 0)
                {
                    return;
                }

                foreach (var field in ownedFields)
                {
                    if (!HasRequiredDisposalContract(field.Type))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            DisposableLifetimeDiagnostics.OwnedFieldRequiresDisposableType,
                            field.Locations[0],
                            _type.Name,
                            field.Name));
                    }
                    else if (!releasedFields.Contains(field))
                    {
                        reportDiagnostic(
                            Diagnostic.Create(DisposableLifetimeDiagnostics.OwnedFieldNotReleased, field.Locations[0], field.Name));
                    }
                }

                foreach (var property in _ownedProperties)
                {
                    if (!HasRequiredDisposalContract(property.Type))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            DisposableLifetimeDiagnostics.OwnedFieldRequiresDisposableType,
                            property.Locations[0],
                            _type.Name,
                            property.Name));
                    }
                    else if (!releasedProperties.Contains(property))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            DisposableLifetimeDiagnostics.OwnedFieldNotReleased,
                            property.Locations[0],
                            property.Name));
                    }
                }

                foreach (var overwrittenField in _overwrittenFields.Where(item => ownedFields.Contains(item.Field)))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DisposableLifetimeDiagnostics.OwnedResourceOverwritten,
                        overwrittenField.Location,
                        overwrittenField.Field.Name));
                }

                foreach (var overwrittenProperty in _overwrittenProperties)
                {
                    reportDiagnostic(Diagnostic.Create(
                        DisposableLifetimeDiagnostics.OwnedPropertyOverwritten,
                        overwrittenProperty.Location,
                        overwrittenProperty.Property.Name));
                }

                foreach (var doubleRelease in _doubleReleases.Where(item =>
                             (item.Member is IFieldSymbol field && ownedFields.Contains(field))
                             || (item.Member is IPropertySymbol property && _ownedProperties.Contains(property))))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DisposableLifetimeDiagnostics.DoubleDispose,
                        doubleRelease.Location,
                        doubleRelease.Member.Name));
                }
            }
        }

        private bool HasRequiredDisposalContract(ITypeSymbol memberType)
        {
            if (!OwnershipResourceShape.CanCarryDisposableResource(memberType, _contract))
            {
                return false;
            }

            if (!_contract.IsDisposable(memberType))
            {
                return _releaseMethods.Count > 0;
            }

            var memberIsSync = _contract.IsSynchronouslyDisposable(memberType);
            var memberIsAsync = _contract.IsAsynchronouslyDisposable(memberType);
            var ownerIsSync = _contract.IsSynchronouslyDisposable(_type);
            var ownerIsAsync = _contract.IsAsynchronouslyDisposable(_type);

            return memberIsSync && memberIsAsync
                ? ownerIsSync || ownerIsAsync
                : (!memberIsSync || ownerIsSync) && (!memberIsAsync || ownerIsAsync);
        }

        private void CollectReleaseEffects(
            IMethodSymbol method,
            HashSet<IMethodSymbol> visitedMethods,
            HashSet<IFieldSymbol> releasedFields,
            HashSet<IPropertySymbol> releasedProperties)
        {
            if (!visitedMethods.Add(method) || !_releaseSummaries.TryGetValue(method, out var summary))
            {
                return;
            }

            releasedFields.UnionWith(summary.ReleasedFields);
            releasedProperties.UnionWith(summary.ReleasedProperties);
            foreach (var calledMethod in summary.CalledMethods)
            {
                CollectReleaseEffects(calledMethod, visitedMethods, releasedFields, releasedProperties);
            }
        }

        private static void VisitOperationBlocks(in ImmutableArray<IOperation> operationBlocks, OperationWalker walker)
        {
            foreach (var operationBlock in operationBlocks)
            {
                walker.Visit(operationBlock);
            }
        }
    }

    private static bool TryGetCurrentInstanceMember(
        IOperation? operation,
        INamedTypeSymbol ownerType,
        out ISymbol? member)
    {
        switch (operation)
        {
            case IFieldReferenceOperation fieldReference when
                IsCurrentInstanceReference(fieldReference.Instance, ownerType):
                member = fieldReference.Field;
                return true;

            case IPropertyReferenceOperation propertyReference when
                IsCurrentInstanceReference(propertyReference.Instance, ownerType):
                member = propertyReference.Property;
                return true;

            case IConditionalAccessInstanceOperation conditionalAccess:
                return TryGetCurrentInstanceMember(
                    LifetimeOwnershipSemantics.GetConditionalAccessReceiver(conditionalAccess),
                    ownerType,
                    out member);

            case IConversionOperation conversion:
                return TryGetCurrentInstanceMember(conversion.Operand, ownerType, out member);

            default:
                member = null;
                return false;
        }
    }

    private static bool IsCurrentInstanceReference(IOperation? operation, INamedTypeSymbol ownerType)
    {
        return operation switch
        {
            null => true,
            IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance, Type: { } type, } =>
                SymbolEqualityComparer.Default.Equals(type, ownerType),
            IConversionOperation conversion => IsCurrentInstanceReference(conversion.Operand, ownerType),
            _ => false,
        };
    }

    /// <summary>
    /// Infers field ownership from constructor assignments whose source transfers ownership.
    /// </summary>
    /// <param name="candidateFields">Disposable fields eligible for inferred ownership.</param>
    /// <param name="contract">The known disposal contracts.</param>
    private sealed class OwnedFieldAssignmentCollector(
        HashSet<IFieldSymbol> candidateFields,
        DisposableContract contract) : OperationWalker
    {
        internal HashSet<IFieldSymbol> OwnedFields { get; } = new(SymbolEqualityComparer.Default);

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);
            var field = LifetimeOwnershipSemantics.GetReferencedField(operation.Target);
            if (field is null
                || !candidateFields.Contains(field)
                || !contract.IsDisposable(field.Type)
                || !IsOwnedValue(operation.Value))
            {
                return;
            }

            OwnedFields.Add(field);
        }

        public override void VisitFieldInitializer(IFieldInitializerOperation operation)
        {
            base.VisitFieldInitializer(operation);
            if (!IsOwnedValue(operation.Value))
            {
                return;
            }

            OwnedFields.UnionWith(operation.InitializedFields.Where(candidateFields.Contains));
        }

        private bool IsOwnedValue(IOperation value)
        {
            var parameter = LifetimeOwnershipSemantics.GetReferencedParameter(value);
            return (parameter is not null && LifetimeOwnershipSemantics.IsTransferredInput(parameter))
                || LifetimeOwnershipSemantics.GetResourceOwnership(value, contract) == LifetimeResourceOwnershipType.OWNED;
        }
    }

    /// <summary>
    /// Collects owned members released or transferred anywhere in an operation block.
    /// </summary>
    /// <param name="candidateFields">Disposable fields eligible for ownership.</param>
    /// <param name="candidateProperties">Disposable properties eligible for ownership.</param>
    /// <param name="ownerType">The type whose members are being analyzed.</param>
    /// <param name="contract">The known disposal contracts.</param>
    private sealed class ReleasedMemberCollector(
        HashSet<IFieldSymbol> candidateFields,
        HashSet<IPropertySymbol> candidateProperties,
        INamedTypeSymbol ownerType,
        DisposableContract contract) : OperationWalker
    {
        private readonly Dictionary<ILocalSymbol, IFieldSymbol> _containerAliases = new(SymbolEqualityComparer.Default);

        internal HashSet<IFieldSymbol> ReleasedFields { get; } = new(SymbolEqualityComparer.Default);

        internal HashSet<IPropertySymbol> ReleasedProperties { get; } = new(SymbolEqualityComparer.Default);

        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);
            foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
            {
                AddIfCandidate(value);
            }
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);
            if (!LifetimeReleaseSemantics.TryGetReleasedValue(operation, out var value))
            {
                return;
            }

            AddIfCandidate(value);
        }

        public override void VisitForEachLoop(IForEachLoopOperation operation)
        {
            var source = ResolveContainerField(operation.Collection);
            if (source is not null)
            {
                foreach (var local in GetIterationLocals(operation))
                {
                    _containerAliases[local] = source;
                }
            }

            base.VisitForEachLoop(operation);

            foreach (var local in GetIterationLocals(operation))
            {
                _containerAliases.Remove(local);
            }
        }

        private void AddIfCandidate(IOperation? value)
        {
            var field = ResolveContainerField(value);
            if (field is not null)
            {
                ReleasedFields.Add(field);
            }

            if (!TryGetCurrentInstanceMember(value, ownerType, out var member)
                || member is not IPropertySymbol property
                || !candidateProperties.Contains(property))
            {
                return;
            }

            ReleasedProperties.Add(property);
        }

        private IFieldSymbol? ResolveContainerField(IOperation? value)
        {
            if (TryGetCurrentInstanceMember(value, ownerType, out var member)
                && member is IFieldSymbol field
                && candidateFields.Contains(field)
                && !contract.IsDisposable(field.Type))
            {
                return field;
            }

            if (LifetimeOwnershipSemantics.GetReferencedLocal(value) is { } referencedLocal
                && _containerAliases.TryGetValue(referencedLocal, out var alias))
            {
                return alias;
            }

            return value switch
            {
                IPropertyReferenceOperation property => ResolveContainerField(property.Instance),
                IInvocationOperation invocation => ResolveContainerField(invocation.Instance),
                IConversionOperation conversion => ResolveContainerField(conversion.Operand),
                IConditionalAccessInstanceOperation conditionalAccess => ResolveContainerField(
                    LifetimeOwnershipSemantics.GetConditionalAccessReceiver(conditionalAccess)),
                _ => null,
            };
        }

        private static IEnumerable<ILocalSymbol> GetIterationLocals(IForEachLoopOperation operation)
        {
            foreach (var iterationLocal in operation.Locals)
            {
                yield return iterationLocal;
            }

            var loopControlLocal = GetIterationLocal(operation.LoopControlVariable);
            if (loopControlLocal is null
                || operation.Locals.Contains(loopControlLocal, SymbolEqualityComparer.Default))
            {
                yield break;
            }

            yield return loopControlLocal;
        }

        private static ILocalSymbol? GetIterationLocal(IOperation operation)
        {
            return operation is IVariableDeclaratorOperation { Symbol: ILocalSymbol local, }
                ? local
                : LifetimeOwnershipSemantics.GetReferencedLocal(operation);
        }
    }

    /// <summary>
    /// Computes members released on every exit path and detects definite repeated releases.
    /// </summary>
    private static class ReleasedMemberDataFlow
    {
        internal static ReleasedMembers Analyze(
            in ImmutableArray<IOperation> operationBlocks,
            HashSet<IFieldSymbol> candidateFields,
            HashSet<IPropertySymbol> candidateProperties,
            INamedTypeSymbol ownerType,
            DisposableContract contract,
            CancellationToken cancellationToken)
        {
            if (operationBlocks.FirstOrDefault() is not { } operationBlock)
            {
                return new();
            }

            while (operationBlock.Parent is { } parent)
            {
                operationBlock = parent;
            }

            var graph = operationBlock switch
            {
                IBlockOperation block => ControlFlowGraph.Create(block, cancellationToken),
                IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
                IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
                _ => null,
            };
            if (graph is null)
            {
                return new();
            }

            var inputs = new ReleasedMembers?[graph.Blocks.Length];
            var doubleReleases = new List<(ISymbol Member, Location Location)>();
            inputs[0] = new();
            var successors = BuildSuccessors(graph);
            var pending = new Queue<BasicBlock>();
            pending.Enqueue(graph.Blocks[0]);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var block = pending.Dequeue();
                if (inputs[block.Ordinal] is not { } input)
                {
                    continue;
                }

                var output = input.Clone();
                var collector = new BlockReleaseCollector(
                    output,
                    candidateFields,
                    candidateProperties,
                    ownerType,
                    contract,
                    doubleReleases);
                foreach (var operation in block.Operations)
                {
                    collector.Visit(operation);
                }

                collector.Visit(block.BranchValue);
                foreach (var successor in successors[block.Ordinal])
                {
                    var current = inputs[successor];
                    var merged = current is null ? output.Clone() : ReleasedMembers.Intersect(current, output);
                    if (current?.SetEquals(merged) == true)
                    {
                        continue;
                    }

                    inputs[successor] = merged;
                    pending.Enqueue(graph.Blocks[successor]);
                }
            }

            var result = inputs[graph.Blocks.Length - 1] ?? new();
            var conditionalCollector = new UnconditionalConditionalReleaseCollector(
                candidateFields,
                candidateProperties,
                ownerType,
                contract);
            foreach (var operation in operationBlocks)
            {
                conditionalCollector.Visit(operation);
            }

            result.ReleasedFields.UnionWith(conditionalCollector.ReleasedFields);
            result.ReleasedProperties.UnionWith(conditionalCollector.ReleasedProperties);
            var containerCollector = new ReleasedMemberCollector(candidateFields, candidateProperties, ownerType, contract);
            foreach (var operation in operationBlocks)
            {
                containerCollector.Visit(operation);
            }

            result.ReleasedFields.UnionWith(containerCollector.ReleasedFields);
            result.DoubleReleases.AddRange(doubleReleases);
            return result;
        }

        internal static HashSet<int>[] BuildSuccessors(ControlFlowGraph graph)
        {
            var successors = Enumerable.Range(0, graph.Blocks.Length).Select(_ => new HashSet<int>()).ToArray();
            foreach (var block in graph.Blocks)
            {
                AddBranch(block.FallThroughSuccessor, successors);
                AddBranch(block.ConditionalSuccessor, successors);
            }

            return successors;
        }

        private static void AddBranch(ControlFlowBranch? branch, HashSet<int>[] successors)
        {
            if (branch?.Destination is not { } destination)
            {
                return;
            }

            var source = branch.Source.Ordinal;
            foreach (var region in branch.FinallyRegions)
            {
                successors[source].Add(region.FirstBlockOrdinal);
                source = region.LastBlockOrdinal;
            }

            successors[source].Add(destination.Ordinal);
        }

        /// <summary>
        /// Represents the definitely-released set and aliases at one control-flow point.
        /// </summary>
        internal sealed class ReleasedMembers
        {
            internal HashSet<IFieldSymbol> ReleasedFields { get; } = new(SymbolEqualityComparer.Default);

            internal HashSet<IPropertySymbol> ReleasedProperties { get; } = new(SymbolEqualityComparer.Default);

            internal HashSet<IMethodSymbol> CalledMethods { get; } = new(SymbolEqualityComparer.Default);

            internal Dictionary<ILocalSymbol, ISymbol> MemberAliases { get; } = new(SymbolEqualityComparer.Default);

            internal List<(ISymbol Member, Location Location)> DoubleReleases { get; } = [];

            internal ReleasedMembers Clone()
            {
                var clone = new ReleasedMembers();
                clone.ReleasedFields.UnionWith(ReleasedFields);
                clone.ReleasedProperties.UnionWith(ReleasedProperties);
                clone.CalledMethods.UnionWith(CalledMethods);
                foreach (var pair in MemberAliases)
                {
                    clone.MemberAliases.Add(pair.Key, pair.Value);
                }

                return clone;
            }

            internal bool SetEquals(ReleasedMembers other)
            {
                return ReleasedFields.SetEquals(other.ReleasedFields)
                    && ReleasedProperties.SetEquals(other.ReleasedProperties)
                    && CalledMethods.SetEquals(other.CalledMethods)
                    && MemberAliases.Count == other.MemberAliases.Count
                    && MemberAliases.All(pair =>
                        other.MemberAliases.TryGetValue(pair.Key, out var member)
                        && SymbolEqualityComparer.Default.Equals(pair.Value, member));
            }

            internal static ReleasedMembers Intersect(ReleasedMembers first, ReleasedMembers second)
            {
                // Only releases present on both predecessors are guaranteed after the join.
                var result = first.Clone();
                result.ReleasedFields.IntersectWith(second.ReleasedFields);
                result.ReleasedProperties.IntersectWith(second.ReleasedProperties);
                result.CalledMethods.IntersectWith(second.CalledMethods);
                foreach (var pair in result.MemberAliases.ToList())
                {
                    if (!second.MemberAliases.TryGetValue(pair.Key, out var member)
                        || !SymbolEqualityComparer.Default.Equals(pair.Value, member))
                    {
                        result.MemberAliases.Remove(pair.Key);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Advances definite release and alias state through one control-flow block.
        /// </summary>
        /// <param name="released">The mutable definite-release state.</param>
        /// <param name="candidateFields">Disposable fields eligible for ownership.</param>
        /// <param name="candidateProperties">Disposable properties eligible for ownership.</param>
        /// <param name="ownerType">The type whose members are being analyzed.</param>
        /// <param name="contract">The known disposal contracts.</param>
        /// <param name="doubleReleases">The collection that receives duplicate release sites.</param>
        private sealed class BlockReleaseCollector(
            ReleasedMembers released,
            HashSet<IFieldSymbol> candidateFields,
            HashSet<IPropertySymbol> candidateProperties,
            INamedTypeSymbol ownerType,
            DisposableContract contract,
            List<(ISymbol Member, Location Location)> doubleReleases) : OperationWalker
        {
            public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
            {
                base.VisitVariableDeclarator(operation);
                SetAlias(operation.Symbol, operation.Initializer?.Value);
            }

            public override void VisitForEachLoop(IForEachLoopOperation operation)
            {
                var previousAliases = new Dictionary<ILocalSymbol, ISymbol?>(SymbolEqualityComparer.Default);
                foreach (var local in operation.Locals)
                {
                    previousAliases[local] = released.MemberAliases.TryGetValue(local, out var alias) ? alias : null;
                }

                if (ResolveContainerMember(operation.Collection) is { } member)
                {
                    foreach (var local in operation.Locals)
                    {
                        released.MemberAliases[local] = member;
                    }
                }

                base.VisitForEachLoop(operation);

                foreach (var pair in previousAliases)
                {
                    if (pair.Value is null)
                    {
                        released.MemberAliases.Remove(pair.Key);
                    }
                    else
                    {
                        released.MemberAliases[pair.Key] = pair.Value;
                    }
                }
            }

            public override void VisitInvocation(IInvocationOperation operation)
            {
                base.VisitInvocation(operation);
                if (SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, ownerType)
                    && IsCurrentInstanceReference(operation.Instance, ownerType))
                {
                    released.CalledMethods.Add(operation.TargetMethod);
                }

                foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
                {
                    Add(value, operation.Syntax.GetLocation());
                }
            }

            public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
            {
                base.VisitSimpleAssignment(operation);
                if (LifetimeOwnershipSemantics.GetReferencedLocal(operation.Target) is { } local)
                {
                    SetAlias(local, operation.Value);
                }

                if (!LifetimeReleaseSemantics.TryGetReleasedValue(operation, out var value))
                {
                    return;
                }

                Add(value, operation.Syntax.GetLocation());
            }

            private void Add(IOperation? value, Location location)
            {
                var member = ResolveMember(value);
                if (member is IFieldSymbol field && candidateFields.Contains(field))
                {
                    AddDoubleReleaseIfNeeded(field, released.ReleasedFields.Contains(field), location);
                    released.ReleasedFields.Add(field);
                }

                if (member is not IPropertySymbol property || !candidateProperties.Contains(property))
                {
                    return;
                }

                AddDoubleReleaseIfNeeded(property, released.ReleasedProperties.Contains(property), location);
                released.ReleasedProperties.Add(property);
            }

            private void SetAlias(ILocalSymbol local, IOperation? value)
            {
                if (ResolveMember(value) is { } member)
                {
                    released.MemberAliases[local] = member;
                }
                else
                {
                    released.MemberAliases.Remove(local);
                }
            }

            private ISymbol? ResolveMember(IOperation? value)
            {
                if (TryGetCurrentInstanceMember(value, ownerType, out var member))
                {
                    return member;
                }

                return LifetimeOwnershipSemantics.GetReferencedLocal(value) is { } local
                    && released.MemberAliases.TryGetValue(local, out var alias)
                        ? alias
                        : null;
            }

            private ISymbol? ResolveContainerMember(IOperation? value)
            {
                if (ResolveMember(value) is IFieldSymbol field && candidateFields.Contains(field))
                {
                    return field;
                }

                return value switch
                {
                    IPropertyReferenceOperation property => ResolveContainerMember(property.Instance),
                    IInvocationOperation invocation => ResolveContainerMember(invocation.Instance),
                    IConversionOperation conversion => ResolveContainerMember(conversion.Operand),
                    IConditionalAccessInstanceOperation conditionalAccess => ResolveContainerMember(
                        LifetimeOwnershipSemantics.GetConditionalAccessReceiver(conditionalAccess)),
                    _ => null,
                };
            }

            private void AddDoubleReleaseIfNeeded(ISymbol member, bool alreadyReleased, Location location)
            {
                if (!alreadyReleased || doubleReleases.Any(item =>
                        SymbolEqualityComparer.Default.Equals(item.Member, member)
                        && item.Location.SourceTree == location.SourceTree
                        && item.Location.SourceSpan == location.SourceSpan))
                {
                    return;
                }

                doubleReleases.Add((member, location));
            }
        }

        /// <summary>
        /// Recognizes conditional-access releases that are unconditional with respect to user control flow.
        /// </summary>
        /// <param name="candidateFields">Disposable fields eligible for ownership.</param>
        /// <param name="candidateProperties">Disposable properties eligible for ownership.</param>
        /// <param name="ownerType">The type whose members are being analyzed.</param>
        /// <param name="contract">The known disposal contracts.</param>
        private sealed class UnconditionalConditionalReleaseCollector(
            HashSet<IFieldSymbol> candidateFields,
            HashSet<IPropertySymbol> candidateProperties,
            INamedTypeSymbol ownerType,
            DisposableContract contract) : OperationWalker
        {
            internal HashSet<IFieldSymbol> ReleasedFields { get; } = new(SymbolEqualityComparer.Default);

            internal HashSet<IPropertySymbol> ReleasedProperties { get; } = new(SymbolEqualityComparer.Default);

            public override void VisitInvocation(IInvocationOperation operation)
            {
                base.VisitInvocation(operation);
                if (operation.Instance is not IConditionalAccessInstanceOperation || IsInsideOptionalControlFlow(operation))
                {
                    return;
                }

                foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
                {
                    if (TryGetCurrentInstanceMember(value, ownerType, out var member)
                        && member is IFieldSymbol field
                        && candidateFields.Contains(field))
                    {
                        ReleasedFields.Add(field);
                    }

                    if (member is IPropertySymbol property
                        && candidateProperties.Contains(property))
                    {
                        ReleasedProperties.Add(property);
                    }
                }
            }

            private static bool IsInsideOptionalControlFlow(IOperation operation)
            {
                for (var current = operation.Parent; current is not null; current = current.Parent)
                {
                    if (current is IConditionalOperation or ILoopOperation or ISwitchOperation or ITryOperation)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Detects straight-line field overwrites after accounting for earlier releases.
    /// </summary>
    /// <param name="candidateFields">Disposable fields eligible for ownership.</param>
    /// <param name="contract">The known disposal contracts.</param>
    private sealed class OwnedFieldOverwriteCollector(
        HashSet<IFieldSymbol> candidateFields,
        DisposableContract contract) : OperationWalker
    {
        private readonly HashSet<IFieldSymbol> _releasedFields = new(SymbolEqualityComparer.Default);

        internal List<(IFieldSymbol Field, Location Location)> OverwrittenFields { get; } = [];

        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);
            foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
            {
                AddReleasedField(LifetimeOwnershipSemantics.GetReferencedField(value));
            }
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);
            if (LifetimeReleaseSemantics.TryGetReleasedValue(operation, out var value))
            {
                AddReleasedField(LifetimeOwnershipSemantics.GetReferencedField(value));
            }

            var field = LifetimeOwnershipSemantics.GetReferencedField(operation.Target);
            if (field is null || !candidateFields.Contains(field))
            {
                return;
            }

            if (!_releasedFields.Contains(field))
            {
                OverwrittenFields.Add((field, operation.Target.Syntax.GetLocation()));
            }

            _releasedFields.Remove(field);
        }

        private void AddReleasedField(IFieldSymbol? field)
        {
            if (field is null || !candidateFields.Contains(field))
            {
                return;
            }

            _releasedFields.Add(field);
        }
    }

    /// <summary>
    /// Tracks definite releases before assignments across member control-flow graphs.
    /// </summary>
    private static class OwnedMemberOverwriteDataFlow
    {
        internal static OverwriteResult Analyze(
            in ImmutableArray<IOperation> operationBlocks,
            HashSet<IFieldSymbol> candidateFields,
            HashSet<IPropertySymbol> candidateProperties,
            INamedTypeSymbol ownerType,
            DisposableContract contract,
            CancellationToken cancellationToken)
        {
            if (operationBlocks.FirstOrDefault() is not { } operationBlock)
            {
                return new();
            }

            while (operationBlock.Parent is { } parent)
            {
                operationBlock = parent;
            }

            var graph = operationBlock switch
            {
                IBlockOperation block => ControlFlowGraph.Create(block, cancellationToken),
                IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
                IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
                _ => null,
            };
            if (graph is null)
            {
                return new();
            }

            var result = new OverwriteResult();
            var inputs = new ReleaseState?[graph.Blocks.Length];
            inputs[0] = new();
            var successors = ReleasedMemberDataFlow.BuildSuccessors(graph);
            var pending = new Queue<BasicBlock>();
            pending.Enqueue(graph.Blocks[0]);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var block = pending.Dequeue();
                if (inputs[block.Ordinal] is not { } input)
                {
                    continue;
                }

                var output = input.Clone();
                var collector = new BlockOverwriteCollector(
                    output,
                    result,
                    candidateFields,
                    candidateProperties,
                    ownerType,
                    contract);
                foreach (var operation in block.Operations)
                {
                    collector.Visit(operation);
                }

                collector.Visit(block.BranchValue);
                foreach (var successor in successors[block.Ordinal])
                {
                    var current = inputs[successor];
                    var merged = current is null ? output.Clone() : ReleaseState.Intersect(current, output);
                    if (current?.SetEquals(merged) == true)
                    {
                        continue;
                    }

                    inputs[successor] = merged;
                    pending.Enqueue(graph.Blocks[successor]);
                }
            }

            return result;
        }

        /// <summary>
        /// Collects unique field and property overwrite sites.
        /// </summary>
        internal sealed class OverwriteResult
        {
            internal List<(IFieldSymbol Field, Location Location)> OverwrittenFields { get; } = [];

            internal List<(IPropertySymbol Property, Location Location)> OverwrittenProperties { get; } = [];

            internal void Add(IFieldSymbol field, Location location)
            {
                if (OverwrittenFields.Any(item =>
                        SymbolEqualityComparer.Default.Equals(item.Field, field)
                        && item.Location.SourceTree == location.SourceTree
                        && item.Location.SourceSpan == location.SourceSpan))
                {
                    return;
                }

                OverwrittenFields.Add((field, location));
            }

            internal void Add(IPropertySymbol property, Location location)
            {
                if (OverwrittenProperties.Any(item =>
                        SymbolEqualityComparer.Default.Equals(item.Property, property)
                        && item.Location.SourceTree == location.SourceTree
                        && item.Location.SourceSpan == location.SourceSpan))
                {
                    return;
                }

                OverwrittenProperties.Add((property, location));
            }
        }

        /// <summary>
        /// Stores members definitely released on entry to a control-flow block.
        /// </summary>
        private sealed class ReleaseState
        {
            internal HashSet<IFieldSymbol> Fields { get; } = new(SymbolEqualityComparer.Default);

            internal HashSet<IPropertySymbol> Properties { get; } = new(SymbolEqualityComparer.Default);

            internal ReleaseState Clone()
            {
                var clone = new ReleaseState();
                clone.Fields.UnionWith(Fields);
                clone.Properties.UnionWith(Properties);
                return clone;
            }

            internal bool SetEquals(ReleaseState other)
            {
                return Fields.SetEquals(other.Fields) && Properties.SetEquals(other.Properties);
            }

            internal static ReleaseState Intersect(ReleaseState first, ReleaseState second)
            {
                // An overwrite is safe only when every predecessor has already released the old value.
                var result = first.Clone();
                result.Fields.IntersectWith(second.Fields);
                result.Properties.IntersectWith(second.Properties);
                return result;
            }
        }

        /// <summary>
        /// Reports assignments that replace members absent from the definite-release state.
        /// </summary>
        /// <param name="state">The definite-release state on block entry.</param>
        /// <param name="result">The collection that receives overwrite sites.</param>
        /// <param name="candidateFields">Owned fields that may be overwritten.</param>
        /// <param name="candidateProperties">Owned properties that may be overwritten.</param>
        /// <param name="ownerType">The type whose members are being analyzed.</param>
        /// <param name="contract">The known disposal contracts.</param>
        private sealed class BlockOverwriteCollector(
            ReleaseState state,
            OverwriteResult result,
            HashSet<IFieldSymbol> candidateFields,
            HashSet<IPropertySymbol> candidateProperties,
            INamedTypeSymbol ownerType,
            DisposableContract contract) : OperationWalker
        {
            public override void VisitInvocation(IInvocationOperation operation)
            {
                base.VisitInvocation(operation);
                foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
                {
                    AddReleased(value);
                }
            }

            public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
            {
                base.VisitSimpleAssignment(operation);
                if (LifetimeReleaseSemantics.TryGetReleasedValue(operation, out var releasedValue))
                {
                    AddReleased(releasedValue);
                }

                if (TryGetCurrentInstanceMember(operation.Target, ownerType, out var targetMember)
                    && targetMember is IFieldSymbol field
                    && candidateFields.Contains(field))
                {
                    if (!state.Fields.Contains(field))
                    {
                        result.Add(field, operation.Target.Syntax.GetLocation());
                    }

                    state.Fields.Remove(field);
                }

                if (targetMember is not IPropertySymbol property
                    || !candidateProperties.Contains(property))
                {
                    return;
                }

                if (!state.Properties.Contains(property))
                {
                    result.Add(property, operation.Target.Syntax.GetLocation());
                }

                state.Properties.Remove(property);
            }

            private void AddReleased(IOperation? value)
            {
                if (TryGetCurrentInstanceMember(value, ownerType, out var member)
                    && member is IFieldSymbol field
                    && candidateFields.Contains(field))
                {
                    state.Fields.Add(field);
                }

                if (member is not IPropertySymbol property
                    || !candidateProperties.Contains(property))
                {
                    return;
                }

                state.Properties.Add(property);
            }
        }
    }

    /// <summary>
    /// Provides a linear fallback for owned-property overwrite detection when no CFG is available.
    /// </summary>
    /// <param name="ownedProperties">Owned properties that may be overwritten.</param>
    /// <param name="contract">The known disposal contracts.</param>
    private sealed class OwnedPropertyOverwriteCollector(
        HashSet<IPropertySymbol> ownedProperties,
        DisposableContract contract) : OperationWalker
    {
        private readonly HashSet<IPropertySymbol> _releasedProperties = new(SymbolEqualityComparer.Default);

        internal List<(IPropertySymbol Property, Location Location)> OverwrittenProperties { get; } = [];

        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);
            foreach (var value in LifetimeReleaseSemantics.GetReleasedValues(operation, contract))
            {
                AddReleasedProperty(LifetimeOwnershipSemantics.GetReferencedProperty(value));
            }
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            base.VisitSimpleAssignment(operation);
            if (LifetimeReleaseSemantics.TryGetReleasedValue(operation, out var value))
            {
                AddReleasedProperty(LifetimeOwnershipSemantics.GetReferencedProperty(value));
            }

            var property = LifetimeOwnershipSemantics.GetReferencedProperty(operation.Target);
            if (property is null || !ownedProperties.Contains(property))
            {
                return;
            }

            if (!_releasedProperties.Contains(property))
            {
                OverwrittenProperties.Add((property, operation.Target.Syntax.GetLocation()));
            }

            _releasedProperties.Remove(property);
        }

        private void AddReleasedProperty(IPropertySymbol? property)
        {
            if (property is null || !ownedProperties.Contains(property))
            {
                return;
            }

            _releasedProperties.Add(property);
        }
    }
}