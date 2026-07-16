// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TedToolkit.Annotations.Assertions.Analyzer;

/// <summary>
/// Finds assertion preconditions that are not represented by fluent assertions in a member body.
/// </summary>
internal static class AssertionPreconditionAnalysis
{
    private const string AttributeSuffix = "Attribute";

    private const string PreconditionSuffix = "Precondition";

    /// <summary>
    /// Gets assertions declared on parameters but absent from the member body.
    /// </summary>
    /// <param name="semanticModel">The semantic model containing the declaration.</param>
    /// <param name="method">The declared member symbol.</param>
    /// <param name="declaration">The member declaration to inspect.</param>
    /// <returns>The required assertions that are not present.</returns>
    internal static IEnumerable<RequiredAssertion> GetMissingAssertions(
        SemanticModel semanticModel,
        IMethodSymbol method,
        BaseMethodDeclarationSyntax declaration)
    {
        _ = method;
        foreach (var parameter in declaration.ParameterList.Parameters)
        {
            foreach (var attribute in parameter.AttributeLists.SelectMany(static list => list.Attributes))
            {
                if (!TryGetFluentMethod(attribute, out var fluentMethod))
                {
                    continue;
                }

                var arguments = GetArguments(semanticModel, attribute);
                var parameterName = parameter.Identifier.ValueText;
                if (!ContainsAssertion(declaration, parameterName, fluentMethod, arguments))
                {
                    yield return new(parameterName, fluentMethod, arguments);
                }
            }
        }
    }

    private static bool TryGetFluentMethod(AttributeSyntax attribute, out string fluentMethod)
    {
        var name = GetSimpleName(attribute.Name);

        if (name.EndsWith(AttributeSuffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - AttributeSuffix.Length);
        }

        if (!name.EndsWith(PreconditionSuffix, StringComparison.Ordinal)
            || name.Length == PreconditionSuffix.Length)
        {
            fluentMethod = "";
            return false;
        }

        fluentMethod = name.Substring(0, name.Length - PreconditionSuffix.Length);
        return true;
    }

    private static string GetSimpleName(NameSyntax name)
    {
        return name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => name.ToString(),
        };
    }

    private static ImmutableArray<string> GetArguments(SemanticModel semanticModel, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return ImmutableArray<string>.Empty;
        }

        var constructor = semanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
        return attribute.ArgumentList.Arguments
            .Where((_, index) => constructor?.Parameters.ElementAtOrDefault(index)?.Name is not ("reason" or "exceptionType"))
            .Select(static argument => ToArgumentExpression(argument.Expression))
            .ToImmutableArray();
    }

    private static string ToArgumentExpression(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax { Token.ValueText: var value, })
        {
            return value;
        }

        if (expression is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof", },
                ArgumentList.Arguments.Count: 1,
            }

            invocation)
        {
            return invocation.ArgumentList.Arguments[0].Expression.ToString();
        }

        return expression.ToString();
    }

    private static bool ContainsAssertion(
        BaseMethodDeclarationSyntax declaration,
        string parameterName,
        string fluentMethod,
        ImmutableArray<string> arguments)
    {
        return declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name, }
                || !StringComparer.Ordinal.Equals(name, fluentMethod)
                || invocation.ArgumentList.Arguments.Count != arguments.Length)
            {
                return false;
            }

            return invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: var subject, },
                        Name.Identifier.ValueText: "Must",
                    },
                },
            }

            && StringComparer.Ordinal.Equals(subject, parameterName);
        });
    }

    /// <summary>
    /// Describes a fluent assertion required by a parameter annotation.
    /// </summary>
    /// <param name="parameterName">The annotated parameter name.</param>
    /// <param name="fluentMethod">The fluent assertion method name.</param>
    /// <param name="arguments">The arguments supplied to the fluent assertion.</param>
    internal readonly struct RequiredAssertion(string parameterName, string fluentMethod, ImmutableArray<string> arguments)
    {
        /// <summary>
        /// Gets the annotated parameter name.
        /// </summary>
        internal string ParameterName { get; } = parameterName;

        /// <summary>
        /// Gets the fluent assertion method name.
        /// </summary>
        internal string FluentMethod { get; } = fluentMethod;

        /// <summary>
        /// Gets the arguments supplied to the fluent assertion.
        /// </summary>
        internal ImmutableArray<string> Arguments { get; } = arguments;
    }
}