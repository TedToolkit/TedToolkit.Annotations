// -----------------------------------------------------------------------
// <copyright file="ConstAliasState.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace TedToolkit.Annotations.Analyzer.Const.Flow;

/// <summary>
/// Stores the possible const roots associated with locals and flow captures at one CFG point.
/// </summary>
internal sealed class ConstAliasState
{
    private readonly Dictionary<ILocalSymbol, ImmutableArray<ConstMutationTarget>> _aliases =
        new(SymbolEqualityComparer.Default);

    private readonly Dictionary<ILocalSymbol, uint> _localDepths = new(SymbolEqualityComparer.Default);

    private readonly Dictionary<CaptureId, ImmutableArray<ConstMutationTarget>> _captures = [];

    /// <summary>
    /// Creates an independent copy of this state.
    /// </summary>
    /// <returns>An independent state copy.</returns>
    internal ConstAliasState Clone()
    {
        var clone = new ConstAliasState();
        CopyTo(_aliases, clone._aliases);
        CopyTo(_localDepths, clone._localDepths);
        CopyTo(_captures, clone._captures);
        return clone;
    }

    /// <summary>
    /// Merges the possible aliases from two incoming control-flow paths.
    /// </summary>
    /// <param name="first">The first incoming state.</param>
    /// <param name="second">The second incoming state.</param>
    /// <returns>The merged may-alias state.</returns>
    internal static ConstAliasState Merge(ConstAliasState first, ConstAliasState second)
    {
        // This is a may-alias lattice: anything reachable on either predecessor remains possible after the join.
        var merged = first.Clone();
        foreach (var pair in second._aliases)
        {
            merged._aliases[pair.Key] = Union(merged.GetAliases(pair.Key), pair.Value);
        }

        foreach (var pair in second._localDepths)
        {
            merged._localDepths[pair.Key] = merged._localDepths.TryGetValue(pair.Key, out var depths)
                ? depths | pair.Value
                : pair.Value;
        }

        foreach (var pair in second._captures)
        {
            merged._captures[pair.Key] = Union(merged.GetCapture(pair.Key), pair.Value);
        }

        return merged;
    }

    /// <summary>
    /// Determines whether another state contains the same aliases and depth masks.
    /// </summary>
    /// <param name="other">The state to compare.</param>
    /// <returns><see langword="true"/> when the states are structurally equal.</returns>
    internal bool HasSameState(ConstAliasState other)
    {
        return TargetDictionaryEquals(_aliases, other._aliases)
            && DictionaryEquals(_localDepths, other._localDepths)
            && TargetDictionaryEquals(_captures, other._captures);
    }

    /// <summary>
    /// Gets the possible const roots for a local.
    /// </summary>
    /// <param name="local">The local symbol.</param>
    /// <returns>The possible const mutation roots.</returns>
    internal ImmutableArray<ConstMutationTarget> GetAliases(ILocalSymbol local)
    {
        return _aliases.TryGetValue(local, out var targets) ? targets : ImmutableArray<ConstMutationTarget>.Empty;
    }

    /// <summary>
    /// Gets the possible const roots stored in a flow capture.
    /// </summary>
    /// <param name="captureId">The flow capture identifier.</param>
    /// <returns>The possible const mutation roots.</returns>
    internal ImmutableArray<ConstMutationTarget> GetCapture(CaptureId captureId)
    {
        return _captures.TryGetValue(captureId, out var targets) ? targets : ImmutableArray<ConstMutationTarget>.Empty;
    }

    /// <summary>
    /// Tries to get the protected depth mask declared by <c>Const.Local</c>.
    /// </summary>
    /// <param name="local">The local symbol.</param>
    /// <param name="depths">The declared protected depth mask.</param>
    /// <returns><see langword="true"/> when the local has a contract.</returns>
    internal bool TryGetLocalDepths(ILocalSymbol local, out uint depths)
    {
        return _localDepths.TryGetValue(local, out depths);
    }

    /// <summary>
    /// Replaces the possible const roots for a local.
    /// </summary>
    /// <param name="local">The local symbol.</param>
    /// <param name="targets">The possible const mutation roots.</param>
    internal void SetAliases(ILocalSymbol local, in ImmutableArray<ConstMutationTarget> targets)
    {
        if (targets.IsEmpty)
        {
            _aliases.Remove(local);
            return;
        }

        _aliases[local] = targets;
    }

    /// <summary>
    /// Replaces the possible const roots for a flow capture.
    /// </summary>
    /// <param name="captureId">The flow capture identifier.</param>
    /// <param name="targets">The possible const mutation roots.</param>
    internal void SetCapture(CaptureId captureId, in ImmutableArray<ConstMutationTarget> targets)
    {
        if (targets.IsEmpty)
        {
            _captures.Remove(captureId);
            return;
        }

        _captures[captureId] = targets;
    }

    /// <summary>
    /// Sets the protected depth mask declared by <c>Const.Local</c>.
    /// </summary>
    /// <param name="local">The local symbol.</param>
    /// <param name="depths">The protected depth mask.</param>
    internal void SetLocalDepths(ILocalSymbol local, uint depths)
    {
        _localDepths[local] = depths;
    }

    /// <summary>
    /// Returns the set union of two mutation-target collections.
    /// </summary>
    /// <param name="first">The first target collection.</param>
    /// <param name="second">The second target collection.</param>
    /// <returns>The target union.</returns>
    internal static ImmutableArray<ConstMutationTarget> Union(
        in ImmutableArray<ConstMutationTarget> first,
        in ImmutableArray<ConstMutationTarget> second)
    {
        var builder = first.ToBuilder();
        foreach (var target in second)
        {
            if (!builder.Any(candidate => candidate.Equals(target)))
            {
                builder.Add(target);
            }
        }

        return builder.ToImmutable();
    }

    private static bool DictionaryEquals<TKey, TValue>(Dictionary<TKey, TValue> first, Dictionary<TKey, TValue> second)
        where TKey : notnull
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        return first.All(pair => second.TryGetValue(pair.Key, out var value) && EqualityComparer<TValue>.Default.Equals(pair.Value, value));
    }

    private static bool TargetDictionaryEquals<TKey>(
        Dictionary<TKey, ImmutableArray<ConstMutationTarget>> first,
        Dictionary<TKey, ImmutableArray<ConstMutationTarget>> second)
        where TKey : notnull
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        return first.All(pair =>
            second.TryGetValue(pair.Key, out var value)
            && pair.Value.Length == value.Length
            && pair.Value.All(value.Contains));
    }

    private static void CopyTo<TKey, TValue>(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> destination)
        where TKey : notnull
    {
        foreach (var pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }
}