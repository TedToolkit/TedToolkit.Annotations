// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer.Lifetime;

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
                DisposableLifetimeDiagnostics.OwnershipTargetMustBeDisposable);
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
        var disposableType = context.Compilation.GetTypeByMetadataName("System.IDisposable");
        if (disposableType is null)
        {
            return;
        }

        context.RegisterOperationBlockAction(blockContext =>
        {
            foreach (var operationBlock in blockContext.OperationBlocks)
            {
                var walker = new LocalDisposableLifetimeWalker(disposableType, blockContext.ReportDiagnostic);
                walker.Visit(operationBlock);
                walker.ReportUndisposedResources();
            }
        });

        OwnershipLifetimeAnalysis.Register(context, disposableType);
    }
}