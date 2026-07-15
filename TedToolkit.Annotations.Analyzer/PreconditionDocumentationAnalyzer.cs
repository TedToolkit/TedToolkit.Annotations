// -----------------------------------------------------------------------
// <copyright file="PreconditionDocumentationAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Offers generation of XML exception documentation from <c>PreconditionAttribute</c> annotations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreconditionDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier for available precondition exception documentation.
    /// </summary>
    public const string DIAGNOSTIC_ID = "TTA200";

    /// <summary>
    /// Describes available precondition exception documentation.
    /// </summary>
    public static readonly DiagnosticDescriptor DocumentationCanBeGenerated = new(
        "TTA200",
        DiagnosticResources.Get("PreconditionDocumentationTitle"),
        DiagnosticResources.Get("PreconditionDocumentationMessage"),
        "Documentation",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(DocumentationCanBeGenerated);
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
        context.RegisterSyntaxNodeAction(AnalyzeMember, SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeMember(SyntaxNodeAnalysisContext context)
    {
        var member = (BaseMethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not IMethodSymbol method
            || !PreconditionDocumentation.GetMissingEntries(method, member.GetLeadingTrivia().ToFullString()).Any())
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationCanBeGenerated, member.GetLocation()));
    }
}