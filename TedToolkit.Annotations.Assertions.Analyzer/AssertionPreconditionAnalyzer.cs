// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Assertions.Analyzer;

/// <summary>
/// Reports methods whose assertion precondition annotations are not enforced in the body.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AssertionPreconditionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(AssertionPreconditionDiagnostics.MissingAssertion);
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
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseMethodDeclarationSyntax)context.Node;
        if (declaration.Body is null
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not IMethodSymbol method
            || !AssertionPreconditionAnalysis.GetMissingAssertions(context.SemanticModel, method, declaration).Any())
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AssertionPreconditionDiagnostics.MissingAssertion, declaration.GetLocation()));
    }
}