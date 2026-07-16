// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseUnitTestAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports behavior cases that are not covered by a unit test.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BehaviorCaseUnitTestAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier for behavior cases without unit-test coverage.
    /// </summary>
    public const string DIAGNOSTIC_ID = "TAD202";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(DocumentationDiagnostics.UnitTestRequired);
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
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        foreach (var attribute in method.GetAttributes())
        {
            if (!IsBehaviorCase(attribute.AttributeClass)
                || attribute.ConstructorArguments.Length < 3
                || attribute.ConstructorArguments[2].Value is not false)
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? method.Locations.FirstOrDefault();
            if (location is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationDiagnostics.UnitTestRequired, location));
            }
        }
    }

    private static bool IsBehaviorCase(INamedTypeSymbol? attributeClass)
    {
        for (var type = attributeClass; type is not null; type = type.BaseType)
        {
            if (type.Name == "BehaviorCaseAttribute"
                && type.ContainingNamespace.ToDisplayString() == "TedToolkit.Annotations.Documentations")
            {
                return true;
            }
        }

        return false;
    }
}