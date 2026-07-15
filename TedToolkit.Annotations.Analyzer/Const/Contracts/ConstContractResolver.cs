// -----------------------------------------------------------------------
// <copyright file="ConstContractResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer.Const.Contracts;

/// <summary>
/// Resolves direct and inherited const contracts and validates their declaration sites.
/// </summary>
internal static class ConstContractResolver
{
    /// <summary>
    /// The metadata name of <c>ConstAttribute</c>.
    /// </summary>
    private const string CONST_ATTRIBUTE_NAME = "TedToolkit.Annotations.Documentations.ConstAttribute";

    /// <summary>
    /// The metadata name of the <c>Explicit</c> helper type.
    /// </summary>
    private const string EXPLICIT_TYPE_NAME = "TedToolkit.Annotations.Documentations.Explicit";

    /// <summary>
    /// Reports const attributes applied to out parameters.
    /// </summary>
    /// <param name="context">The symbol analysis context.</param>
    internal static void AnalyzeParameter(SymbolAnalysisContext context)
    {
        var parameter = (IParameterSymbol)context.Symbol;
        var attribute = GetDirectConstAttribute(parameter);
        if (parameter.RefKind != RefKind.Out || attribute is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConstMutationAnalyzer.OutParameterNotAllowed,
            GetLocation(attribute, parameter, context.CancellationToken),
            parameter.Name));
    }

    /// <summary>
    /// Gets the effective direct and inherited const depth mask for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol whose contract is requested.</param>
    /// <param name="depths">The union of all applicable const depth masks.</param>
    /// <returns><see langword="true"/> when at least one contract exists.</returns>
    internal static bool TryGetConstDepths(ISymbol symbol, out uint depths)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        return TryGetConstDepths(symbol, visited, out depths);
    }

    /// <summary>
    /// Gets the effective contract for a property accessor while preserving accessor-over-property precedence.
    /// </summary>
    /// <param name="accessor">The property accessor.</param>
    /// <param name="depths">The effective protected depths.</param>
    /// <returns><see langword="true"/> when an explicit or inherited contract exists.</returns>
    internal static bool TryGetAccessorDepths(IMethodSymbol accessor, out uint depths)
    {
        if (accessor.AssociatedSymbol is not IPropertySymbol property)
        {
            return TryGetConstDepths(accessor, out depths);
        }

        var found = TryGetDirectConstDepths(accessor, out var declaredDepths)
                    || TryGetDirectConstDepths(property, out declaredDepths)
                    || TryGetStaticTypeConstDepths(property, out declaredDepths);
        depths = found ? declaredDepths : 0;

        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default)
        {
            accessor,
            property,
        };
        found |= UnionContractSources(accessor, visited, ref depths);
        found |= UnionContractSources(property, visited, ref depths);
        return found;
    }

    /// <summary>
    /// Determines whether a method is one of the <c>Explicit.Const</c> overloads.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns><see langword="true"/> when the method is <c>Explicit.Const</c>.</returns>
    internal static bool IsConstLocal(IMethodSymbol method)
    {
        return method.Name == "Const" && method.ContainingType.ToDisplayString() == EXPLICIT_TYPE_NAME;
    }

    private static bool TryGetConstDepths(ISymbol symbol, HashSet<ISymbol> visited, out uint depths)
    {
        if (!visited.Add(symbol))
        {
            depths = 0;
            return false;
        }

        var found = TryGetDirectConstDepths(symbol, out depths)
                    || TryGetStaticTypeConstDepths(symbol, out depths);
        found |= UnionContractSources(symbol, visited, ref depths);
        return found;
    }

    private static bool UnionContractSources(ISymbol symbol, HashSet<ISymbol> visited, ref uint depths)
    {
        // Implementations must honor every base and interface promise, so depth masks compose by union.
        var found = false;
        foreach (var source in GetContractSources(symbol))
        {
            if (TryGetConstDepths(source, visited, out var inheritedDepths))
            {
                depths |= inheritedDepths;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetDirectConstDepths(ISymbol symbol, out uint depths)
    {
        if (GetDirectConstAttribute(symbol) is not { } attribute)
        {
            depths = 0;
            return false;
        }

        depths = attribute.ConstructorArguments.IsEmpty
            ? uint.MaxValue
            : (uint)attribute.ConstructorArguments[0].Value!;
        return true;
    }

    private static bool TryGetStaticTypeConstDepths(ISymbol symbol, out uint depths)
    {
        if (!symbol.IsStatic || symbol.ContainingType is null || GetDirectConstAttribute(symbol) is not null)
        {
            depths = 0;
            return false;
        }

        return TryGetDirectConstDepths(symbol.ContainingType, out depths);
    }

    private static IEnumerable<ISymbol> GetContractSources(ISymbol symbol)
    {
        switch (symbol)
        {
            case IParameterSymbol parameter when parameter.ContainingSymbol is IMethodSymbol method:
                foreach (var source in GetMethodSources(method))
                {
                    if (parameter.Ordinal < source.Parameters.Length)
                    {
                        yield return source.Parameters[parameter.Ordinal];
                    }
                }

                break;

            case IMethodSymbol method:
                foreach (var source in GetMethodSources(method))
                {
                    yield return source;
                }

                break;

            case IPropertySymbol property:
                if (property.OverriddenProperty is not null)
                {
                    yield return property.OverriddenProperty;
                }

                foreach (var source in property.ExplicitInterfaceImplementations)
                {
                    yield return source;
                }

                foreach (var source in GetImplicitInterfaceImplementations(property))
                {
                    yield return source;
                }

                break;
        }
    }

    private static IEnumerable<IMethodSymbol> GetMethodSources(IMethodSymbol method)
    {
        if (method.OverriddenMethod is not null)
        {
            yield return method.OverriddenMethod;
        }

        foreach (var source in method.ExplicitInterfaceImplementations)
        {
            yield return source;
        }

        foreach (var interfaceType in method.ContainingType.AllInterfaces)
        {
            foreach (var member in interfaceType.GetMembers().OfType<IMethodSymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(member), method))
                {
                    yield return member;
                }
            }
        }
    }

    private static IEnumerable<IPropertySymbol> GetImplicitInterfaceImplementations(IPropertySymbol property)
    {
        foreach (var interfaceType in property.ContainingType.AllInterfaces)
        {
            foreach (var member in interfaceType.GetMembers().OfType<IPropertySymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(property.ContainingType.FindImplementationForInterfaceMember(member), property))
                {
                    yield return member;
                }
            }
        }
    }

    private static AttributeData? GetDirectConstAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().FirstOrDefault(candidate => candidate.AttributeClass?.ToDisplayString() == CONST_ATTRIBUTE_NAME);
    }

    private static Location GetLocation(AttributeData attribute, ISymbol symbol, CancellationToken cancellationToken)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? symbol.Locations[0];
    }
}