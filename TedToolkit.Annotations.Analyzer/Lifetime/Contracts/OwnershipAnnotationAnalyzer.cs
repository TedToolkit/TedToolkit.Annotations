// -----------------------------------------------------------------------
// <copyright file="OwnershipAnnotationAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Validates that ownership annotations are applied only to disposable values.
/// </summary>
internal static class OwnershipAnnotationAnalyzer
{
    /// <summary>
    /// Registers ownership annotation validation.
    /// </summary>
    /// <param name="context">The compilation analysis context.</param>
    /// <param name="contract">The available disposal contracts.</param>
    internal static void Register(CompilationStartAnalysisContext context, DisposableContract contract)
    {
        context.RegisterSymbolAction(
            symbolContext => Analyze(symbolContext.Symbol, contract, symbolContext.ReportDiagnostic),
            SymbolKind.Field,
            SymbolKind.Method,
            SymbolKind.Parameter,
            SymbolKind.Property);
    }

    private static void Analyze(ISymbol symbol, DisposableContract contract, Action<Diagnostic> reportDiagnostic)
    {
        var type = symbol switch
        {
            IFieldSymbol field => field.Type,
            IMethodSymbol method => method.ReturnType,
            IParameterSymbol parameter => parameter.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        var ownershipAttributes = LifetimeOwnershipSemantics.GetOwnershipAnnotations(symbol)
            .Where(IsOwnershipAttribute)
            .ToList();
        if (ownershipAttributes.Count == 0 || type is null)
        {
            return;
        }

        if (!contract.IsDisposable(type))
        {
            reportDiagnostic(Diagnostic.Create(
                DisposableLifetimeDiagnostics.OwnershipTargetMustBeDisposable,
                GetLocation(ownershipAttributes[0], symbol),
                type.ToDisplayString()));
        }

        if (!HasInvalidContract(symbol, ownershipAttributes))
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.InvalidOwnershipContract,
            GetLocation(ownershipAttributes[0], symbol),
            symbol.Name));
    }

    private static bool HasInvalidContract(ISymbol symbol, List<AttributeData> attributes)
    {
        var ownershipByFlow = new Dictionary<int, int>();
        foreach (var attribute in attributes)
        {
            if (attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not int ownership
                || ownership is < 0 or > 1)
            {
                return true;
            }

            var declaredFlow = attribute.ConstructorArguments.Length > 1
                && attribute.ConstructorArguments[1].Value is int value
                    ? value
                    : 0;
            if (!TryGetEffectiveFlow(symbol, declaredFlow, out var effectiveFlow))
            {
                return true;
            }

            if (ownershipByFlow.TryGetValue(effectiveFlow, out var existingOwnership)
                && existingOwnership != ownership)
            {
                return true;
            }

            ownershipByFlow[effectiveFlow] = ownership;
        }

        return false;
    }

    private static bool TryGetEffectiveFlow(ISymbol symbol, int declaredFlow, out int effectiveFlow)
    {
        if (declaredFlow is < 0 or > 2)
        {
            effectiveFlow = default;
            return false;
        }

        switch (symbol)
        {
            case IMethodSymbol:
                effectiveFlow = declaredFlow == 0 ? 2 : declaredFlow;
                return effectiveFlow == 2;

            case IFieldSymbol:
                effectiveFlow = declaredFlow;
                return declaredFlow == 0;

            case IPropertySymbol:
                effectiveFlow = declaredFlow == 0 ? 2 : declaredFlow;
                return true;

            case IParameterSymbol { RefKind: RefKind.Ref, }:
                effectiveFlow = declaredFlow;
                return declaredFlow is 1 or 2;

            case IParameterSymbol { RefKind: RefKind.Out, }:
                effectiveFlow = declaredFlow == 0 ? 2 : declaredFlow;
                return effectiveFlow == 2;

            case IParameterSymbol:
                effectiveFlow = declaredFlow == 0 ? 1 : declaredFlow;
                return effectiveFlow == 1;

            default:
                effectiveFlow = default;
                return false;
        }
    }

    private static bool IsOwnershipAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass?.ToDisplayString()
            == "TedToolkit.Annotations.Documentations.OwnershipAttribute";
    }

    private static Location GetLocation(AttributeData attribute, ISymbol symbol)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? symbol.Locations[0];
    }
}