// -----------------------------------------------------------------------
// <copyright file="ConstContractCodeFixProvider.cs" company="TedToolkit">
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

using TedToolkit.Annotations.Analyzer.Const;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Generates a const contract from the contracts required by directly invoked members.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConstContractCodeFixProvider))]
[Shared]
public sealed class ConstContractCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            return ImmutableArray.Create(ConstContractInferenceAnalyzer.DIAGNOSTIC_ID);
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
        var declaration = root?.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Generate Const contract",
                cancellationToken => ApplyAsync(context.Document, declaration, cancellationToken),
                equivalenceKey: nameof(ConstContractCodeFixProvider)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        MethodDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        if (!ConstContractInference.TryInfer(
                declaration,
                semanticModel,
                cancellationToken,
                out var methodDepths,
                out var parameterDepths))
        {
            return document;
        }

        var updatedDeclaration = methodDepths == 0
            ? declaration
            : declaration.WithAttributeLists(declaration.AttributeLists.Insert(0, CreateAttributeList(methodDepths)));
        for (var index = 0; index < declaration.ParameterList.Parameters.Count; index++)
        {
            var originalParameter = declaration.ParameterList.Parameters[index];
            if (semanticModel.GetDeclaredSymbol(originalParameter, cancellationToken) is not IParameterSymbol symbol
                || !parameterDepths.TryGetValue(symbol, out var depths))
            {
                continue;
            }

            var parameter = updatedDeclaration.ParameterList.Parameters[index];
            updatedDeclaration = updatedDeclaration.ReplaceNode(
                parameter,
                parameter.WithAttributeLists(parameter.AttributeLists.Insert(0, CreateAttributeList(depths))));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(
            declaration,
            updatedDeclaration.WithAdditionalAnnotations(Formatter.Annotation)));
    }

    private static AttributeListSyntax CreateAttributeList(uint depths)
    {
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Const"));
        if (depths != uint.MaxValue)
        {
            attribute = attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ParseExpression(FormatDepths(depths))))));
        }

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
    }

    private static string FormatDepths(uint depths)
    {
        if (IsSingleBit(depths, out var bit))
        {
            return GetDepthName(bit);
        }

        if (IsGreaterRange(depths, out bit))
        {
            return $"{GetDepthName(bit)}_OR_GREATER";
        }

        if (IsLowerRange(depths, out bit))
        {
            return $"{GetDepthName(bit)}_OR_LOWER";
        }

        return string.Join(
            " | ",
            Enumerable.Range(0, 32)
                .Where(index => (depths & (1U << index)) != 0)
                .Select(GetDepthName));
    }

    private static string GetDepthName(int depth)
    {
        return string.Concat(
            "ConstDepth.DEPTH",
            depth.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool IsSingleBit(uint value, out int bit)
    {
        bit = 0;
        if (value == 0 || (value & (value - 1)) != 0)
        {
            return false;
        }

        while ((value >>= 1) != 0)
        {
            bit++;
        }

        return true;
    }

    private static bool IsGreaterRange(uint value, out int bit)
    {
        for (bit = 1; bit < 32; bit++)
        {
            if (value == uint.MaxValue << bit)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLowerRange(uint value, out int bit)
    {
        for (bit = 1; bit < 31; bit++)
        {
            if (value == (1U << (bit + 1)) - 1U)
            {
                return true;
            }
        }

        return false;
    }
}