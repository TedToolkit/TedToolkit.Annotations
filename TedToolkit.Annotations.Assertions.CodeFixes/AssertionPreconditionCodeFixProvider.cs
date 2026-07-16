// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionCodeFixProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace TedToolkit.Annotations.Assertions.Analyzer;

/// <summary>
/// Adds fluent assertion calls for declared assertion preconditions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertionPreconditionCodeFixProvider))]
[Shared]
public sealed class AssertionPreconditionCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            return ImmutableArray.Create("TAA200");
        }
    }

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var declaration = root?.FindNode(context.Span).FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (declaration?.Body is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Generate assertion preconditions",
                cancellationToken => ApplyAsync(context.Document, declaration, cancellationToken),
                nameof(AssertionPreconditionCodeFixProvider)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        BaseMethodDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method)
        {
            return document;
        }

        var assertions = AssertionPreconditionAnalysis.GetMissingAssertions(semanticModel, method, declaration).ToArray();
        if (assertions.Length is 0)
        {
            return document;
        }

        var statements = assertions.Select(CreateAssertionStatement).ToArray();
        var useFastPush = !method.IsAsync && SupportsFastPush(semanticModel.Compilation);
        var generated = statements.Length is 1
            ? statements[0]
            : CreateScopeStatement(method.Name, statements, useFastPush);
        var updatedDeclaration = declaration.WithBody(declaration.Body!.WithStatements(declaration.Body.Statements.Insert(0, generated)));
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var updatedRoot = root.ReplaceNode(declaration, updatedDeclaration);
        foreach (var namespaceName in new[] { "TedToolkit.Assertions", "TedToolkit.Scopes", })
        {
            if (updatedRoot.Usings.All(usingDirective => usingDirective.Name?.ToString() != namespaceName))
            {
                updatedRoot = updatedRoot.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName)));
            }
        }

        return document.WithSyntaxRoot(Formatter.Format(
            updatedRoot,
            document.Project.Solution.Workspace,
            cancellationToken: cancellationToken));
    }

    private static StatementSyntax CreateAssertionStatement(AssertionPreconditionAnalysis.RequiredAssertion assertion)
    {
        var arguments = string.Join(", ", assertion.Arguments);
        return SyntaxFactory.ParseStatement($"{assertion.ParameterName}.Must().{assertion.FluentMethod}({arguments});");
    }

    private static UsingStatementSyntax CreateScopeStatement(
        string methodName,
        IEnumerable<StatementSyntax> statements,
        bool useFastPush)
    {
        var pushMethod = useFastPush ? "FastPush" : "Push";
        var conditions = string.Join("\n", statements.Select(static statement => statement.ToFullString()));
        return (UsingStatementSyntax)SyntaxFactory.ParseStatement(
            $"using (new AssertionScope(\"Validating {methodName} arguments\").{pushMethod}())\n{{\n{conditions}\n}}");
    }

    private static bool SupportsFastPush(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("TedToolkit.Scopes.ValueScopeExtensions")
            ?.GetMembers("FastPush")
            .OfType<IMethodSymbol>()
            .Any(static method => method.IsExtensionMethod) is true;
    }
}