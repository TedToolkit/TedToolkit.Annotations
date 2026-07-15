// -----------------------------------------------------------------------
// <copyright file="ConstMutationTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Const.Flow;

/// <summary>
/// Identifies a const root symbol and a mutation depth relative to that root.
/// </summary>
internal readonly struct ConstMutationTarget : IEquatable<ConstMutationTarget>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstMutationTarget"/> struct.
    /// </summary>
    /// <param name="symbol">The const root symbol.</param>
    /// <param name="depth">The mutation depth relative to the root.</param>
    /// <param name="requiresReferenceBoundary">
    /// Whether a value-type copy must cross a reference member before it can mutate the source graph.
    /// </param>
    internal ConstMutationTarget(ISymbol symbol, int depth, bool requiresReferenceBoundary = false)
    {
        Symbol = symbol;
        Depth = depth;
        RequiresReferenceBoundary = requiresReferenceBoundary;
    }

    /// <summary>
    /// Gets the const root symbol.
    /// </summary>
    internal ISymbol Symbol { get; }

    /// <summary>
    /// Gets the mutation depth relative to <see cref="Symbol"/>.
    /// </summary>
    internal int Depth { get; }

    /// <summary>
    /// Gets a value indicating whether a value-type copy must cross a reference member before it can mutate the source graph.
    /// </summary>
    internal bool RequiresReferenceBoundary { get; }

    /// <inheritdoc />
    public bool Equals(ConstMutationTarget other)
    {
        return Depth == other.Depth
               && RequiresReferenceBoundary == other.RequiresReferenceBoundary
               && SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ConstMutationTarget other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = (SymbolEqualityComparer.Default.GetHashCode(Symbol) * 397) ^ Depth;
        return (hashCode * 397) ^ RequiresReferenceBoundary.GetHashCode();
    }
}