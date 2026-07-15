// -----------------------------------------------------------------------
// <copyright file="BoxingCodeFixProvider.cs" company="TedToolkit">
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
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Replaces boxing conversions with an <c>Explicit.Box</c> invocation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BoxingCodeFixProvider))]
[Shared]
public sealed class BoxingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            return ImmutableArray.Create(BoxingAnalyzer.DIAGNOSTIC_ID);
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
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var conversion = root is null || semanticModel is null
            ? null
            : FindBoxingConversion(root.FindNode(context.Span, getInnermostNodeForTie: true), semanticModel);
        if (conversion?.Operand.Syntax is not ExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Make boxing explicit with Explicit.Box",
                cancellationToken => ApplyAsync(context.Document, conversion, cancellationToken),
                equivalenceKey: nameof(BoxingCodeFixProvider)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        IConversionOperation conversion,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || conversion.Operand.Syntax is not ExpressionSyntax operand)
        {
            return document;
        }

        var targetType = conversion.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sourceType = GetBoxSourceType(conversion.Operand.Type)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var methodName = conversion.Type?.SpecialType == SpecialType.System_Object
            ? "global::TedToolkit.Annotations.Documentations.Explicit.Box"
            : $"global::TedToolkit.Annotations.Documentations.Explicit.Box<{targetType}, {sourceType}>";
        var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.ParseExpression(methodName),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(operand.WithoutTrivia()))))
            .WithTriviaFrom(conversion.Syntax);

        return document.WithSyntaxRoot(root.ReplaceNode(conversion.Syntax, invocation));
    }

    private static ITypeSymbol? GetBoxSourceType(ITypeSymbol? sourceType)
    {
        if (sourceType is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            }

            nullableType)
        {
            return nullableType.TypeArguments[0];
        }

        return sourceType;
    }

    private static IConversionOperation? FindBoxingConversion(SyntaxNode node, SemanticModel semanticModel)
    {
        foreach (var expression in node.AncestorsAndSelf().OfType<ExpressionSyntax>())
        {
            for (var operation = semanticModel.GetOperation(expression);
                 operation is not null && operation.Syntax == expression;
                 operation = operation.Parent)
            {
                if (operation is IConversionOperation conversion
                    && conversion.Operand.Type is { } sourceType
                    && conversion.Type is { } targetType
                    && semanticModel.Compilation is CSharpCompilation csharpCompilation
                    && csharpCompilation.ClassifyConversion(sourceType, targetType).IsBoxing)
                {
                    return conversion;
                }
            }
        }

        return null;
    }
}