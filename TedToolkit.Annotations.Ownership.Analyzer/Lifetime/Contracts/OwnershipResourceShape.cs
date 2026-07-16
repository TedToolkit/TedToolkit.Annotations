// -----------------------------------------------------------------------
// <copyright file="OwnershipResourceShape.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Recognizes values that directly release resources or structurally carry resources.
/// </summary>
internal static class OwnershipResourceShape
{
    /// <summary>
    /// Determines whether a value can carry a disposable resource as itself or through its source-visible structure.
    /// </summary>
    /// <param name="type">The value type to inspect.</param>
    /// <param name="contract">The disposal contracts available to the compilation.</param>
    /// <returns><see langword="true"/> when the value contains a disposable resource.</returns>
    internal static bool CanCarryDisposableResource(ITypeSymbol type, DisposableContract contract)
    {
        return CanCarryDisposableResource(type, contract, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    private static bool CanCarryDisposableResource(
        ITypeSymbol type,
        DisposableContract contract,
        HashSet<ITypeSymbol> visitedTypes)
    {
        if (contract.IsDisposable(type))
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return CanCarryDisposableResource(array.ElementType, contract, visitedTypes);
        }

        if (type is not INamedTypeSymbol namedType || !visitedTypes.Add(namedType))
        {
            return false;
        }

        try
        {
            if (namedType.TypeArguments.Any(argument => CanCarryDisposableResource(argument, contract, visitedTypes)))
            {
                return true;
            }

            // Metadata fields are implementation details of another assembly. Only source-visible fields form
            // an ownership graph edge; generic arguments remain available across assembly boundaries.
            if (!namedType.Locations.Any(location => location.IsInSource))
            {
                return false;
            }

            return namedType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(field => !field.IsStatic
                    && !field.IsImplicitlyDeclared
                    && !LifetimeOwnershipSemantics.IsExplicitlyBorrowed(field)
                    && CanCarryDisposableResource(field.Type, contract, visitedTypes));
        }
        finally
        {
            visitedTypes.Remove(namedType);
        }
    }
}