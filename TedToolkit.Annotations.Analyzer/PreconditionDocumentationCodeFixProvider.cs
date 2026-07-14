// -----------------------------------------------------------------------
// <copyright file="PreconditionDocumentationCodeFixProvider.cs" company="TedToolkit">
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

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Adds XML exception documentation for preconditions that declare an exception type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreconditionDocumentationCodeFixProvider))]
[Shared]
public sealed class PreconditionDocumentationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            return ImmutableArray.Create(PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID);
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
        var member = root?.FindNode(context.Span).FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (member is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Generate precondition exception documentation",
                cancellationToken => ApplyAsync(context.Document, member, cancellationToken),
                equivalenceKey: nameof(PreconditionDocumentationCodeFixProvider)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        BaseMethodDeclarationSyntax member,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(member, cancellationToken) is not IMethodSymbol method)
        {
            return document;
        }

        var documentation = member.GetLeadingTrivia().ToFullString();
        var generatedDocumentation = string.Concat(
            PreconditionDocumentation.GetMissingEntries(method, documentation)
                .Select(entry => entry.ToDocumentation()));
        if (generatedDocumentation.Length == 0)
        {
            return document;
        }

        var generatedTrivia = SyntaxFactory.ParseLeadingTrivia(generatedDocumentation);
        var generatedComment = generatedTrivia.First(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var existingComment = member.GetLeadingTrivia()
            .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var updatedMember = existingComment.RawKind == 0
            ? member.WithLeadingTrivia(generatedTrivia.AddRange(member.GetLeadingTrivia()))
            : member.ReplaceTrivia(existingComment, SyntaxFactory.Trivia(
                ((DocumentationCommentTriviaSyntax)existingComment.GetStructure()!).WithContent(
                    ((DocumentationCommentTriviaSyntax)existingComment.GetStructure()!).Content.AddRange(
                        ((DocumentationCommentTriviaSyntax)generatedComment.GetStructure()!).Content))));

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(member, updatedMember));
    }
}