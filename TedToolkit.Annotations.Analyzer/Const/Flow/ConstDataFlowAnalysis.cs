// -----------------------------------------------------------------------
// <copyright file="ConstDataFlowAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer.Const.Flow;

/// <summary>
/// Runs forward may-alias analysis over Roslyn control-flow graphs.
/// </summary>
internal static class ConstDataFlowAnalysis
{
    /// <summary>
    /// Analyzes every operation block owned by a method or accessor.
    /// </summary>
    /// <param name="context">The operation-block analysis context.</param>
    internal static void Analyze(OperationBlockAnalysisContext context)
    {
        foreach (var operationBlock in context.OperationBlocks)
        {
            var graph = CreateGraph(GetRoot(operationBlock), context.CancellationToken);
            if (graph is null)
            {
                continue;
            }

            AnalyzeGraph(graph, context.OwningSymbol, context.ReportDiagnostic, context.CancellationToken);
        }
    }

    private static void AnalyzeGraph(
        ControlFlowGraph graph,
        ISymbol owningSymbol,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        var inputs = ComputeInputStates(graph, owningSymbol, cancellationToken);
        foreach (var block in graph.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inputs[block.Ordinal] is { } input)
            {
                ProcessBlock(block, input, owningSymbol, reportDiagnostic);
            }
        }
    }

    private static ConstAliasState?[] ComputeInputStates(
        ControlFlowGraph graph,
        ISymbol owningSymbol,
        CancellationToken cancellationToken)
    {
        // A work queue computes the least fixed point; loop back-edges can add aliases to blocks already visited.
        var inputs = new ConstAliasState?[graph.Blocks.Length];
        var successors = BuildSuccessors(graph);
        inputs[0] = new();
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

            var output = ProcessBlock(block, input, owningSymbol, null);
            foreach (var successor in successors[block.Ordinal])
            {
                Propagate(graph.Blocks[successor], output, inputs, pending);
            }
        }

        return inputs;
    }

    private static ConstAliasState ProcessBlock(
        BasicBlock block,
        ConstAliasState input,
        ISymbol owningSymbol,
        Action<Diagnostic>? reportDiagnostic)
    {
        var walker = new ConstOperationWalker(owningSymbol, input.Clone(), reportDiagnostic);
        foreach (var operation in block.Operations)
        {
            walker.Visit(operation);
        }

        walker.Visit(block.BranchValue);
        return walker.State;
    }

    private static void Propagate(
        BasicBlock destination,
        ConstAliasState output,
        ConstAliasState?[] inputs,
        Queue<BasicBlock> pending)
    {
        var existing = inputs[destination.Ordinal];
        var merged = existing is null ? output.Clone() : ConstAliasState.Merge(existing, output);
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

        AddExceptionalBranches(graph.Root, successors);

        return successors;
    }

    private static void AddExceptionalBranches(ControlFlowRegion region, HashSet<int>[] successors)
    {
        // Roslyn's ordinary successor edges do not enumerate every operation that can jump into a handler.
        // Connecting every try block to each handler is conservative for a may-alias analysis.
        if (region.Kind is ControlFlowRegionKind.TryAndCatch or ControlFlowRegionKind.TryAndFinally
            && region.NestedRegions.FirstOrDefault(candidate => candidate.Kind == ControlFlowRegionKind.Try) is { } tryRegion)
        {
            foreach (var handler in region.NestedRegions.Where(candidate => candidate.Kind != ControlFlowRegionKind.Try))
            {
                for (var source = tryRegion.FirstBlockOrdinal; source <= tryRegion.LastBlockOrdinal; source++)
                {
                    successors[source].Add(handler.FirstBlockOrdinal);
                }
            }
        }

        foreach (var nestedRegion in region.NestedRegions)
        {
            AddExceptionalBranches(nestedRegion, successors);
        }
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

    private static IOperation GetRoot(IOperation operation)
    {
        while (operation.Parent is { } parent)
        {
            operation = parent;
        }

        return operation;
    }

    private static ControlFlowGraph? CreateGraph(IOperation operation, CancellationToken cancellationToken)
    {
        return operation switch
        {
            IBlockOperation block => ControlFlowGraph.Create(block, cancellationToken),
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
            IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
            IFieldInitializerOperation fieldInitializer => ControlFlowGraph.Create(fieldInitializer, cancellationToken),
            IPropertyInitializerOperation propertyInitializer => ControlFlowGraph.Create(propertyInitializer, cancellationToken),
            _ => null,
        };
    }
}