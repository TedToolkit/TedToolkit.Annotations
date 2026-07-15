// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseUnitTestCodeFixProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Composition;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Marks a behavior case as covered by a unit test.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BehaviorCaseUnitTestCodeFixProvider))]
[Shared]
public sealed class BehaviorCaseUnitTestCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            return ImmutableArray.Create(BehaviorCaseUnitTestAnalyzer.DIAGNOSTIC_ID);
        }
    }

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var attribute = root?.FindNode(context.Span).FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                DiagnosticResources.Get("TAD202CodeFixTitle").ToString(CultureInfo.CurrentCulture),
                cancellationToken => ApplyAsync(context.Document, attribute, cancellationToken),
                equivalenceKey: nameof(BehaviorCaseUnitTestCodeFixProvider)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        AttributeSyntax attribute,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var arguments = attribute.ArgumentList?.Arguments ?? default;
        var index = -1;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon?.Name.Identifier.ValueText == "hasUnitTest")
            {
                index = i;
                break;
            }
        }

        if (index < 0 && arguments.Count > 2)
        {
            index = 2;
        }

        var trueExpression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        SeparatedSyntaxList<AttributeArgumentSyntax> updatedArguments;
        if (index >= 0)
        {
            updatedArguments = arguments.Replace(arguments[index], arguments[index].WithExpression(trueExpression));
        }
        else
        {
            updatedArguments = arguments.Add(
                SyntaxFactory.AttributeArgument(trueExpression).WithNameColon(SyntaxFactory.NameColon("hasUnitTest")));
        }

        var updatedAttribute = attribute.WithArgumentList(
            (attribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList()).WithArguments(updatedArguments));
        return document.WithSyntaxRoot(root.ReplaceNode(attribute, updatedAttribute));
    }
}