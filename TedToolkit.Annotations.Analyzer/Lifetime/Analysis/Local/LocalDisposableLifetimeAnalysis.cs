// -----------------------------------------------------------------------
// <copyright file="LocalDisposableLifetimeAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;
using TedToolkit.Annotations.Analyzer.Lifetime.Model;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Local;

/// <summary>
/// Runs local disposable lifetime analysis over a Roslyn control-flow graph.
/// </summary>
internal static class LocalDisposableLifetimeAnalysis
{
    /// <summary>
    /// Analyzes one operation block.
    /// </summary>
    /// <param name="operationBlock">The method body, initializer, or other operation block to analyze.</param>
    /// <param name="owningMethod">The method that supplies parameter and return ownership contracts.</param>
    /// <param name="contract">The disposal interfaces resolved for the compilation.</param>
    /// <param name="reportDiagnostic">The sink for lifetime diagnostics.</param>
    /// <param name="cancellationToken">A token used to cancel analysis.</param>
    internal static void Analyze(
        IOperation operationBlock,
        IMethodSymbol? owningMethod,
        DisposableContract contract,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        var root = GetRoot(operationBlock);
        var graph = CreateGraph(root, cancellationToken);
        if (graph is null)
        {
            // Roslyn cannot create CFGs for every operation-root kind. A linear walk still catches local violations.
            var fallback = new LocalDisposableLifetimeWalker(contract, reportDiagnostic, owningMethod: owningMethod);
            fallback.Visit(operationBlock);
            fallback.ReportUndisposedResources();
            return;
        }

        var localFunctionReleases = LocalFunctionReleaseAnalysis.Analyze(graph, contract, cancellationToken);
        var guaranteedReleaseSymbols = CollectGuaranteedReleaseSymbols(graph, contract);
        var diagnostics = new List<Diagnostic>();
        AnalyzeGraph(
            graph,
            contract,
            diagnostics.Add,
            CollectUsingLocals(root),
            localFunctionReleases,
            owningMethod,
            guaranteedReleaseSymbols,
            cancellationToken);
        foreach (var localFunction in graph.LocalFunctions)
        {
            var localFunctionGraph = graph.GetLocalFunctionControlFlowGraph(localFunction, cancellationToken);
            AnalyzeGraph(
                localFunctionGraph,
                contract,
                reportDiagnostic,
                [],
                localFunctionReleases,
                localFunction,
                new(SymbolEqualityComparer.Default),
                cancellationToken);
        }

        foreach (var diagnostic in diagnostics.Where(diagnostic =>
                     !IsGuaranteedLoopLeak(diagnostic, guaranteedReleaseSymbols)
                     && !IsSupersededByDefiniteDoubleDispose(diagnostic, diagnostics)))
        {
            reportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeGraph(
        ControlFlowGraph graph,
        DisposableContract contract,
        Action<Diagnostic> reportDiagnostic,
        HashSet<ILocalSymbol> usingLocals,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ILocalSymbol>> localFunctionReleases,
        IMethodSymbol? owningMethod,
        HashSet<ISymbol> guaranteedReleaseSymbols,
        CancellationToken cancellationToken)
    {
        var inputs = ComputeInputStates(
            graph,
            contract,
            usingLocals,
            localFunctionReleases,
            owningMethod,
            guaranteedReleaseSymbols,
            cancellationToken);
        ReportDiagnostics(graph, inputs, contract, reportDiagnostic, localFunctionReleases, owningMethod, cancellationToken);
    }

    /// <summary>
    /// Gets the root operation accepted by ControlFlowGraph.Create.
    /// </summary>
    /// <param name="operation">Any operation nested in the desired root.</param>
    /// <returns>The topmost operation in the parent chain.</returns>
    internal static IOperation GetRoot(IOperation operation)
    {
        while (operation.Parent is { } parent)
        {
            operation = parent;
        }

        return operation;
    }

    /// <summary>
    /// Removes leak diagnostics superseded by a definite double-release diagnostic.
    /// </summary>
    /// <param name="diagnostics">Diagnostics collected across operation blocks.</param>
    /// <returns>Diagnostics with path-dependent leak reports removed when the same declaration is definitely double-disposed.</returns>
    internal static IEnumerable<Diagnostic> FilterRedundantLeaks(IEnumerable<Diagnostic> diagnostics)
    {
        var diagnosticList = diagnostics.ToList();
        return diagnosticList.Where(diagnostic =>
            !IsSupersededByDefiniteDoubleDispose(diagnostic, diagnosticList));
    }

    private static ControlFlowGraph? CreateGraph(IOperation operationBlock, CancellationToken cancellationToken)
    {
        return operationBlock switch
        {
            IBlockOperation block => ControlFlowGraph.Create(block, cancellationToken),
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
            IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
            IFieldInitializerOperation fieldInitializer => ControlFlowGraph.Create(fieldInitializer, cancellationToken),
            IPropertyInitializerOperation propertyInitializer => ControlFlowGraph.Create(propertyInitializer, cancellationToken),
            IParameterInitializerOperation parameterInitializer => ControlFlowGraph.Create(parameterInitializer, cancellationToken),
            _ => null,
        };
    }

    private static LifetimeObjectStore?[] ComputeInputStates(
        ControlFlowGraph graph,
        DisposableContract contract,
        HashSet<ILocalSymbol> usingLocals,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ILocalSymbol>> localFunctionReleases,
        IMethodSymbol? owningMethod,
        HashSet<ISymbol> guaranteedReleaseSymbols,
        CancellationToken cancellationToken)
    {
        // Iterate to a fixed point because back edges can change the abstract state at an earlier block.
        var inputs = new LifetimeObjectStore?[graph.Blocks.Length];
        var successors = BuildSuccessors(graph);
        inputs[0] = CreateInitialState(usingLocals, owningMethod, guaranteedReleaseSymbols);
        var pending = new Queue<BasicBlock>();
        pending.Enqueue(graph.Blocks[0]);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = pending.Dequeue();
            var input = inputs[block.Ordinal];
            if (input is null)
            {
                continue;
            }

            var output = ProcessBlock(
                block,
                input,
                contract,
                _ => { },
                graph,
                localFunctionReleases,
                owningMethod,
                cancellationToken);
            foreach (var successor in successors[block.Ordinal])
            {
                Propagate(graph.Blocks[successor], output, inputs, pending);
            }
        }

        return inputs;
    }

    private static void ReportDiagnostics(
        ControlFlowGraph graph,
        LifetimeObjectStore?[] inputs,
        DisposableContract contract,
        Action<Diagnostic> reportDiagnostic,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ILocalSymbol>> localFunctionReleases,
        IMethodSymbol? owningMethod,
        CancellationToken cancellationToken)
    {
        // Diagnostics are emitted only after the fixed point is stable, preventing reports from transient states.
        foreach (var block in graph.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = inputs[block.Ordinal];
            if (input is null)
            {
                continue;
            }

            if (block.Kind == BasicBlockKind.Exit)
            {
                var exitWalker = new LocalDisposableLifetimeWalker(
                    contract,
                    reportDiagnostic,
                    input.Clone(),
                    owningMethod: owningMethod);
                exitWalker.ReportUndisposedResources();
                continue;
            }

            ProcessBlock(
                block,
                input,
                contract,
                reportDiagnostic,
                graph,
                localFunctionReleases,
                owningMethod,
                cancellationToken);
        }
    }

    private static LifetimeObjectStore ProcessBlock(
        BasicBlock block,
        LifetimeObjectStore input,
        DisposableContract contract,
        Action<Diagnostic> reportDiagnostic,
        ControlFlowGraph graph,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ILocalSymbol>> localFunctionReleases,
        IMethodSymbol? owningMethod,
        CancellationToken cancellationToken)
    {
        var walker = new LocalDisposableLifetimeWalker(
            contract,
            reportDiagnostic,
            input.Clone(),
            graph,
            localFunctionReleases,
            owningMethod,
            cancellationToken);
        foreach (var operation in block.Operations)
        {
            walker.Visit(operation);
        }

        if (block.BranchValue is { } branchValue)
        {
            if (block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return)
            {
                walker.VisitReturnedValue(branchValue);
            }
            else
            {
                walker.Visit(branchValue);
            }
        }

        return walker.GetState();
    }

    private static LifetimeObjectStore CreateInitialState(
        HashSet<ILocalSymbol> usingLocals,
        IMethodSymbol? owningMethod,
        HashSet<ISymbol> guaranteedReleaseSymbols)
    {
        var state = new LifetimeObjectStore(usingLocals, guaranteedReleaseSymbols);
        if (owningMethod is null)
        {
            return state;
        }

        foreach (var parameter in owningMethod.Parameters)
        {
            if (!LifetimeOwnershipSemantics.IsTransferredInput(parameter))
            {
                continue;
            }

            state.Set(parameter, state.Create(
                parameter,
                parameter.Locations.FirstOrDefault() ?? Location.None,
                isUsing: false,
                isBorrowed: false));
        }

        return state;
    }

    private static HashSet<ISymbol> CollectGuaranteedReleaseSymbols(
        ControlFlowGraph graph,
        DisposableContract contract)
    {
        var collector = new GuaranteedReleaseCollector(contract);
        foreach (var block in graph.Blocks)
        {
            foreach (var operation in block.Operations)
            {
                collector.Visit(operation);
            }

            collector.Visit(block.BranchValue);
        }

        return collector.Symbols;
    }

    private static bool IsGuaranteedLoopLeak(
        Diagnostic diagnostic,
        HashSet<ISymbol> guaranteedReleaseSymbols)
    {
        if (diagnostic.Id != DisposableLifetimeDiagnostics.UndisposedResource.Id
            || diagnostic.Location.SourceTree is null)
        {
            return false;
        }

        return guaranteedReleaseSymbols.Any(symbol => symbol.DeclaringSyntaxReferences.Any(reference =>
        {
            var syntax = reference.GetSyntax();
            return syntax.SyntaxTree == diagnostic.Location.SourceTree
                && syntax.Span.IntersectsWith(diagnostic.Location.SourceSpan);
        }));
    }

    private static bool IsSupersededByDefiniteDoubleDispose(
        Diagnostic diagnostic,
        List<Diagnostic> diagnostics)
    {
        if (diagnostic.Id != DisposableLifetimeDiagnostics.UndisposedResource.Id
            || diagnostic.Location.SourceTree is not { } sourceTree
            || sourceTree.GetRoot().FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf()
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault() is not { } declaration)
        {
            return false;
        }

        return diagnostics.Any(candidate => IsDoubleDisposeForDeclaration(candidate, declaration, sourceTree));
    }

    private static bool IsDoubleDisposeForDeclaration(
        Diagnostic candidate,
        VariableDeclaratorSyntax declaration,
        SyntaxTree sourceTree)
    {
        if (candidate.Id != DisposableLifetimeDiagnostics.DoubleDispose.Id
            || candidate.Location.SourceTree != sourceTree)
        {
            return false;
        }

        var invocation = sourceTree.GetRoot()
            .FindNode(candidate.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        return invocation?.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax identifier,
        }

        && identifier.Identifier.ValueText == declaration.Identifier.ValueText;
    }

    private static HashSet<ILocalSymbol> CollectUsingLocals(IOperation root)
    {
        var collector = new UsingLocalCollector();
        collector.Visit(root);
        return collector.Locals;
    }

    private static void Propagate(
        BasicBlock destination,
        LifetimeObjectStore output,
        LifetimeObjectStore?[] inputs,
        Queue<BasicBlock> pending)
    {
        var existing = inputs[destination.Ordinal];
        var merged = existing is null ? output.Clone() : LifetimeObjectStore.Merge(existing, output);
        if (existing?.HasSameState(merged) == true)
        {
            return;
        }

        inputs[destination.Ordinal] = merged;
        pending.Enqueue(destination);
    }

    private static HashSet<int>[] BuildSuccessors(ControlFlowGraph graph)
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

        var sourceOrdinal = branch.Source.Ordinal;
        foreach (var region in branch.FinallyRegions)
        {
            successors[sourceOrdinal].Add(region.FirstBlockOrdinal);
            sourceOrdinal = region.LastBlockOrdinal;
        }

        successors[sourceOrdinal].Add(destination.Ordinal);
    }

    /// <summary>
    /// Collects locals whose release is guaranteed by a using construct.
    /// </summary>
    private sealed class UsingLocalCollector : OperationWalker
    {
        /// <summary>
        /// Gets locals declared or referenced as using resources.
        /// </summary>
        internal HashSet<ILocalSymbol> Locals { get; } = new(SymbolEqualityComparer.Default);

        /// <inheritdoc/>
        public override void VisitUsing(IUsingOperation operation)
        {
            var references = new LocalCollector(Locals);
            references.Visit(operation.Resources);
            base.VisitUsing(operation);
        }
    }

    /// <summary>
    /// Adds every local reference in an operation subtree to a supplied set.
    /// </summary>
    /// <param name="locals">The set that receives referenced locals.</param>
    private sealed class LocalCollector(HashSet<ILocalSymbol> locals) : OperationWalker
    {
        /// <inheritdoc/>
        public override void VisitLocalReference(ILocalReferenceOperation operation)
        {
            locals.Add(operation.Local);
            base.VisitLocalReference(operation);
        }
    }

    /// <summary>
    /// Recognizes releases inside simple loops that are proven to execute at least once.
    /// </summary>
    /// <param name="contract">The known disposal methods.</param>
    private sealed class GuaranteedReleaseCollector(DisposableContract contract) : OperationWalker
    {
        private readonly Dictionary<string, ISymbol> _symbolsByName = [];

        /// <summary>
        /// Gets symbols released by a guaranteed loop iteration.
        /// </summary>
        internal HashSet<ISymbol> Symbols { get; } = new(SymbolEqualityComparer.Default);

        /// <inheritdoc/>
        public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
        {
            _symbolsByName[operation.Symbol.Name] = operation.Symbol;
            base.VisitVariableDeclarator(operation);
        }

        /// <inheritdoc/>
        public override void VisitInvocation(IInvocationOperation operation)
        {
            base.VisitInvocation(operation);
            if (!contract.IsSynchronousRelease(operation.TargetMethod)
                && !contract.IsAsynchronousRelease(operation.TargetMethod))
            {
                return;
            }

            if (LocalDisposableLifetimeWalker.GetMinimumLoopIterations(operation.Syntax) < 1
                || GetReleasedSymbol(operation) is not { } symbol)
            {
                return;
            }

            Symbols.Add(symbol);
        }

        private ISymbol? GetReleasedSymbol(IInvocationOperation operation)
        {
            if (LifetimeOwnershipSemantics.GetReferencedSymbol(operation.Instance) is { } symbol)
            {
                return symbol;
            }

            if (operation.Syntax is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax identifier, },
                }

                || !_symbolsByName.TryGetValue(identifier.Identifier.ValueText, out symbol))
            {
                return null;
            }

            return symbol;
        }
    }

    /// <summary>
    /// Summarizes captured locals that every exit path of a local function releases.
    /// </summary>
    private static class LocalFunctionReleaseAnalysis
    {
        /// <summary>
        /// Builds release summaries for all local functions in a containing graph.
        /// </summary>
        /// <param name="containingGraph">The graph that declares the local functions.</param>
        /// <param name="contract">The known disposal methods.</param>
        /// <param name="cancellationToken">A token used to cancel analysis.</param>
        /// <returns>A map from each local function to locals released on every exit path.</returns>
        internal static Dictionary<IMethodSymbol, HashSet<ILocalSymbol>> Analyze(
            ControlFlowGraph containingGraph,
            DisposableContract contract,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<IMethodSymbol, HashSet<ILocalSymbol>>(SymbolEqualityComparer.Default);
            foreach (var localFunction in containingGraph.LocalFunctions)
            {
                var graph = containingGraph.GetLocalFunctionControlFlowGraph(localFunction, cancellationToken);
                result[localFunction] = AnalyzeGraph(graph, contract, cancellationToken);
            }

            return result;
        }

        private static HashSet<ILocalSymbol> AnalyzeGraph(
            ControlFlowGraph graph,
            DisposableContract contract,
            CancellationToken cancellationToken)
        {
            // Intersection is intentional: a caller may rely only on releases performed along every path.
            var inputs = new HashSet<ILocalSymbol>?[graph.Blocks.Length];
            inputs[0] = new(SymbolEqualityComparer.Default);
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

                var output = new HashSet<ILocalSymbol>(input, SymbolEqualityComparer.Default);
                var collector = new ReleasedLocalCollector(output, contract);
                foreach (var operation in block.Operations)
                {
                    collector.Visit(operation);
                }

                collector.Visit(block.BranchValue);
                foreach (var successor in successors[block.Ordinal])
                {
                    var current = inputs[successor];
                    var merged = current is null
                        ? new HashSet<ILocalSymbol>(output, SymbolEqualityComparer.Default)
                        : new HashSet<ILocalSymbol>(current, SymbolEqualityComparer.Default);
                    if (current is not null)
                    {
                        merged.IntersectWith(output);
                    }

                    if (current?.SetEquals(merged) == true)
                    {
                        continue;
                    }

                    inputs[successor] = merged;
                    pending.Enqueue(graph.Blocks[successor]);
                }
            }

            return inputs[graph.Blocks.Length - 1] ?? new(SymbolEqualityComparer.Default);
        }

        /// <summary>
        /// Collects locals directly released by a local-function block.
        /// </summary>
        /// <param name="releasedLocals">The set that receives released locals.</param>
        /// <param name="contract">The known disposal methods.</param>
        private sealed class ReleasedLocalCollector(
            HashSet<ILocalSymbol> releasedLocals,
            DisposableContract contract) : OperationWalker
        {
            /// <inheritdoc/>
            public override void VisitInvocation(IInvocationOperation operation)
            {
                base.VisitInvocation(operation);
                if (!contract.IsSynchronousRelease(operation.TargetMethod)
                    && !contract.IsAsynchronousRelease(operation.TargetMethod))
                {
                    return;
                }

                var local = LifetimeOwnershipSemantics.GetReferencedLocal(operation.Instance);
                if (local is null)
                {
                    return;
                }

                releasedLocals.Add(local);
            }
        }
    }
}