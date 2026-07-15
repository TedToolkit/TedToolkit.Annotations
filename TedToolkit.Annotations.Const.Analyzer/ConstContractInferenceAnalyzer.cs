// -----------------------------------------------------------------------
// <copyright file="ConstContractInferenceAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer.Const;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Offers generation of const contracts inferred from annotated invocations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstContractInferenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier for an inferable const contract.
    /// </summary>
    public const string DIAGNOSTIC_ID = "TAC303";

    private static readonly DiagnosticDescriptor _contractCanBeGenerated = new(
        DIAGNOSTIC_ID,
        "Generate Const contract",
        "Const contract can be generated from annotated invocations",
        "Const",
        DiagnosticSeverity.Hidden,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(_contractCanBeGenerated);
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
        context.RegisterCompilationStartAction(RegisterConstContractInference);
    }

    private static void RegisterConstContractInference(CompilationStartAnalysisContext context)
    {
        if (!ConstAnalysisOptions.IsEnabled(context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (!ConstContractInference.TryInfer(declaration, context.SemanticModel, context.CancellationToken, out _, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_contractCanBeGenerated, declaration.Identifier.GetLocation()));
    }
}
