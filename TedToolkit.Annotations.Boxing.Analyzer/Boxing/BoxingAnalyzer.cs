// -----------------------------------------------------------------------
// <copyright file="BoxingAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Boxing;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports boxing conversions that are not made explicit through <c>Boxer.Box</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoxingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier for boxing conversions.
    /// </summary>
    public const string DIAGNOSTIC_ID = "TAB201";

    private const string EXPLICIT_TYPE_NAME = "TedToolkit.Annotations.Boxing.Boxer";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(BoxingDiagnostics.BoxingMustBeExplicit);
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
        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
    }

    private static void AnalyzeConversion(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (!IsBoxingConversion(context.Compilation, conversion) || IsExplicitBoxArgument(conversion))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            BoxingDiagnostics.BoxingMustBeExplicit,
            conversion.Syntax.GetLocation(),
            conversion.Operand.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "value",
            conversion.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "object"));
    }

    private static bool IsBoxingConversion(Compilation compilation, IConversionOperation conversion)
    {
        return compilation is CSharpCompilation csharpCompilation
               && conversion.Operand.Type is { } sourceType
               && conversion.Type is { } targetType
               && IsDefinitelyValueType(sourceType)
               && csharpCompilation.ClassifyConversion(sourceType, targetType).IsBoxing;
    }

    private static bool IsDefinitelyValueType(ITypeSymbol sourceType)
    {
        return sourceType is ITypeParameterSymbol typeParameter
            ? typeParameter.HasValueTypeConstraint
            : sourceType.IsValueType;
    }

    private static bool IsExplicitBoxArgument(IOperation operation)
    {
        // Roslyn may insert one or more conversions around the argument before it reaches Boxer.Box.
        while (operation.Parent is IConversionOperation parentConversion)
        {
            operation = parentConversion;
        }

        return operation.Parent is IArgumentOperation
        {
            Parent: IInvocationOperation invocation,
        }

               && invocation.TargetMethod.Name == "Box"
               && invocation.TargetMethod.ContainingType.ToDisplayString() == EXPLICIT_TYPE_NAME;
    }
}