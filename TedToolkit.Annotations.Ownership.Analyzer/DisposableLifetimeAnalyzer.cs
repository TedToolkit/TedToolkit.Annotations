// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Local;
using TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Members;
using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports statically provable lifetime violations for locally owned <see cref="IDisposable"/> resources.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableLifetimeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(
                DisposableLifetimeDiagnostics.DoubleDispose,
                DisposableLifetimeDiagnostics.UseAfterDispose,
                DisposableLifetimeDiagnostics.UseAfterTransfer,
                DisposableLifetimeDiagnostics.UndisposedResource,
                DisposableLifetimeDiagnostics.CallbackOutlivesResource,
                DisposableLifetimeDiagnostics.DisposeBorrowedProperty,
                DisposableLifetimeDiagnostics.DisposedResourceReturned,
                DisposableLifetimeDiagnostics.OwnedFieldRequiresDisposableType,
                DisposableLifetimeDiagnostics.OwnedFieldNotReleased,
                DisposableLifetimeDiagnostics.OwnershipTargetMustBeDisposable,
                DisposableLifetimeDiagnostics.OwnedResourceOverwritten,
                DisposableLifetimeDiagnostics.OwnedPropertyOverwritten,
                DisposableLifetimeDiagnostics.UnobservedAsyncDispose,
                DisposableLifetimeDiagnostics.InvalidOwnershipContract);
        }
    }

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(RegisterLifetimeAnalysis);
    }

    private static void RegisterLifetimeAnalysis(CompilationStartAnalysisContext context)
    {
        if (!OwnershipAnalysisOptions.IsEnabled(context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        var contract = DisposableContract.Create(context.Compilation);
        if (!contract.IsAvailable)
        {
            return;
        }

        var analyzedRoots = new ConcurrentDictionary<(SyntaxTree Tree, int Start, int Length), byte>();
        var localDiagnostics = new ConcurrentBag<Diagnostic>();

        // Operation blocks can expose the same CFG root more than once. Analyze each syntax root once, then
        // filter cross-block leak diagnostics at compilation end when all competing path evidence is available.
        context.RegisterOperationBlockAction(blockContext =>
        {
            foreach (var operationBlock in blockContext.OperationBlocks)
            {
                var root = LocalDisposableLifetimeAnalysis.GetRoot(operationBlock);
                var rootKey = (root.Syntax.SyntaxTree, root.Syntax.SpanStart, root.Syntax.Span.Length);
                if (!analyzedRoots.TryAdd(rootKey, 0))
                {
                    continue;
                }

                LocalDisposableLifetimeAnalysis.Analyze(
                    root,
                    blockContext.OwningSymbol as IMethodSymbol,
                    contract,
                    localDiagnostics.Add,
                    blockContext.CancellationToken);
            }
        });
        context.RegisterCompilationEndAction(compilationEndContext =>
        {
            foreach (var diagnostic in LocalDisposableLifetimeAnalysis.FilterRedundantLeaks(localDiagnostics))
            {
                compilationEndContext.ReportDiagnostic(diagnostic);
            }
        });

        OwnershipAnnotationAnalyzer.Register(context, contract);
        OwnedMemberLifetimeAnalysis.Register(context, contract);
    }
}
