// -----------------------------------------------------------------------
// <copyright file="LifetimeObjectStore.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Model;

/// <summary>
/// Stores aliases and abstract states for one control-flow point.
/// </summary>
internal sealed class LifetimeObjectStore
{
    private readonly Dictionary<ISymbol, LifetimeObject> _objects = new(SymbolEqualityComparer.Default);

    private readonly Dictionary<int, LifetimeObject> _allObjects = [];

    private readonly Dictionary<CaptureId, IOperation> _captureOrigins = [];

    private readonly Dictionary<ILocalSymbol, LifetimeObject> _asyncDisposeResults = new(SymbolEqualityComparer.Default);

    private readonly IdentityProvider _identities;

    private readonly HashSet<ILocalSymbol> _usingLocals;

    private readonly HashSet<ISymbol> _guaranteedReleaseSymbols;

    /// <summary>
    /// Creates an empty state with no using or guaranteed-release information.
    /// </summary>
    internal LifetimeObjectStore()
        : this(new(), new(SymbolEqualityComparer.Default), new(SymbolEqualityComparer.Default))
    {
    }

    /// <summary>
    /// Creates an empty state for a set of using locals.
    /// </summary>
    /// <param name="usingLocals">Locals whose enclosing using construct guarantees disposal.</param>
    internal LifetimeObjectStore(HashSet<ILocalSymbol> usingLocals)
        : this(new(), usingLocals, new(SymbolEqualityComparer.Default))
    {
    }

    /// <summary>
    /// Creates an empty state with precomputed release guarantees.
    /// </summary>
    /// <param name="usingLocals">Locals whose enclosing using construct guarantees disposal.</param>
    /// <param name="guaranteedReleaseSymbols">Symbols released by a loop proven to execute.</param>
    internal LifetimeObjectStore(
        HashSet<ILocalSymbol> usingLocals,
        HashSet<ISymbol> guaranteedReleaseSymbols)
        : this(new(), usingLocals, guaranteedReleaseSymbols)
    {
    }

    private LifetimeObjectStore(
        IdentityProvider identities,
        HashSet<ILocalSymbol> usingLocals,
        HashSet<ISymbol> guaranteedReleaseSymbols)
    {
        _identities = identities;
        _usingLocals = usingLocals;
        _guaranteedReleaseSymbols = guaranteedReleaseSymbols;
    }

    /// <summary>
    /// Determines whether a local is managed by a using construct.
    /// </summary>
    /// <param name="local">The local to inspect.</param>
    /// <returns><see langword="true"/> when disposal is guaranteed by using.</returns>
    internal bool IsUsingLocal(ILocalSymbol local)
    {
        return _usingLocals.Contains(local);
    }

    /// <summary>
    /// Creates a resource using the stable identity assigned to its acquisition location.
    /// </summary>
    /// <param name="symbol">The symbol used to name the resource.</param>
    /// <param name="creationLocation">The acquisition location.</param>
    /// <param name="isUsing">Whether using guarantees disposal.</param>
    /// <param name="isBorrowed">Whether another owner must dispose the resource.</param>
    /// <returns>A tracked resource snapshot.</returns>
    internal LifetimeObject Create(
        ISymbol symbol,
        Location creationLocation,
        bool isUsing,
        bool isBorrowed)
    {
        var resource = new LifetimeObject(
            symbol,
            creationLocation,
            isUsing,
            isBorrowed,
            _identities.GetIdentity(creationLocation));
        if (_guaranteedReleaseSymbols.Contains(symbol))
        {
            _identities.MarkGuaranteedRelease(resource.Identity);
        }

        return resource;
    }

    /// <summary>
    /// Binds a symbol to a resource identity.
    /// </summary>
    /// <param name="symbol">The local or parameter alias.</param>
    /// <param name="lifetimeObject">The resource referenced by the symbol.</param>
    internal void Set(ISymbol symbol, LifetimeObject lifetimeObject)
    {
        _objects[symbol] = lifetimeObject;
        _allObjects[lifetimeObject.Identity] = lifetimeObject;
    }

    /// <summary>
    /// Tries to get the resource bound to a symbol.
    /// </summary>
    /// <param name="symbol">The local or parameter alias.</param>
    /// <param name="lifetimeObject">Receives the referenced resource.</param>
    /// <returns><see langword="true"/> when the symbol is tracked.</returns>
    internal bool TryGet(ISymbol symbol, out LifetimeObject lifetimeObject)
    {
        return _objects.TryGetValue(symbol, out lifetimeObject!);
    }

    /// <summary>
    /// Resolves conversions and flow captures, then finds the referenced resource.
    /// </summary>
    /// <param name="operation">The expression that may refer to a tracked alias.</param>
    /// <param name="lifetimeObject">Receives the referenced resource.</param>
    /// <returns><see langword="true"/> when the expression resolves to a tracked resource.</returns>
    internal bool TryResolveLifetimeObject(IOperation? operation, out LifetimeObject lifetimeObject)
    {
        var symbol = LifetimeOwnershipSemantics.GetReferencedSymbol(Resolve(operation));
        if (symbol is not null && _objects.TryGetValue(symbol, out lifetimeObject!))
        {
            return true;
        }

        lifetimeObject = null!;
        return false;
    }

    /// <summary>
    /// Records the original expression represented by a Roslyn flow capture.
    /// </summary>
    /// <param name="id">The flow-capture identifier.</param>
    /// <param name="operation">The captured expression.</param>
    internal void SetCaptureOrigin(in CaptureId id, IOperation operation)
    {
        _captureOrigins[id] = Resolve(operation) ?? operation;
    }

    /// <summary>
    /// Associates a local awaitable with the resource being asynchronously disposed.
    /// </summary>
    /// <param name="local">The local storing the disposal awaitable.</param>
    /// <param name="resource">The resource whose disposal completes with that awaitable.</param>
    internal void SetAsyncDisposeResult(ILocalSymbol local, LifetimeObject resource)
    {
        _asyncDisposeResults[local] = resource;
    }

    /// <summary>
    /// Resolves an awaitable expression to the resource whose asynchronous disposal it represents.
    /// </summary>
    /// <param name="operation">An awaitable expression or forwarding call.</param>
    /// <param name="resource">Receives the associated resource.</param>
    /// <returns><see langword="true"/> when the expression is a tracked asynchronous-disposal result.</returns>
    internal bool TryGetAsyncDisposeResult(IOperation? operation, out LifetimeObject resource)
    {
        var local = GetAsyncDisposeResultLocal(Resolve(operation));
        if (local is not null && _asyncDisposeResults.TryGetValue(local, out resource!))
        {
            return true;
        }

        resource = null!;
        return false;
    }

    /// <summary>
    /// Marks an asynchronous-disposal result as awaited or returned.
    /// </summary>
    /// <param name="operation">The observed awaitable expression.</param>
    internal void ObserveAsyncDisposeResult(IOperation? operation)
    {
        if (!TryGetAsyncDisposeResult(operation, out var resource))
        {
            return;
        }

        resource.ObserveAsyncDispose();
    }

    /// <summary>
    /// Removes flow-capture and conversion wrappers from an operation.
    /// </summary>
    /// <param name="operation">The operation to unwrap.</param>
    /// <returns>The originating expression.</returns>
    internal IOperation? Resolve(IOperation? operation)
    {
        return operation switch
        {
            IFlowCaptureReferenceOperation captureReference when _captureOrigins.TryGetValue(captureReference.Id, out var origin) =>
                Resolve(origin),
            IConversionOperation conversion => Resolve(conversion.Operand),
            _ => operation,
        };
    }

    /// <summary>
    /// Gets one snapshot for every logical resource known at this point.
    /// </summary>
    /// <returns>A set keyed by resource reference.</returns>
    internal HashSet<LifetimeObject> GetUniqueObjects()
    {
        return new(_allObjects.Values);
    }

    /// <summary>
    /// Marks a resource as released by a loop proven to execute at least once.
    /// </summary>
    /// <param name="resource">The released resource.</param>
    internal void MarkGuaranteedLoopRelease(LifetimeObject resource)
    {
        _identities.MarkGuaranteedRelease(resource.Identity);
    }

    /// <summary>
    /// Determines whether a proven loop discharges the resource obligation.
    /// </summary>
    /// <param name="resource">The resource to inspect.</param>
    /// <returns><see langword="true"/> when a guaranteed loop releases it.</returns>
    internal bool IsGuaranteedReleased(LifetimeObject resource)
    {
        return _identities.IsGuaranteedReleased(resource.Identity);
    }

    /// <summary>
    /// Creates an independent branch snapshot while preserving logical identities.
    /// </summary>
    /// <returns>A deep copy of mutable resource states and alias bindings.</returns>
    internal LifetimeObjectStore Clone()
    {
        var clone = new LifetimeObjectStore(_identities, _usingLocals, _guaranteedReleaseSymbols);
        foreach (var resource in _allObjects.Values)
        {
            clone._allObjects.Add(resource.Identity, resource.Clone());
        }

        foreach (var pair in _objects)
        {
            clone._objects.Add(pair.Key, clone._allObjects[pair.Value.Identity]);
        }

        foreach (var pair in _captureOrigins)
        {
            clone._captureOrigins.Add(pair.Key, pair.Value);
        }

        foreach (var pair in _asyncDisposeResults)
        {
            clone._asyncDisposeResults.Add(pair.Key, clone._allObjects[pair.Value.Identity]);
        }

        return clone;
    }

    /// <summary>
    /// Determines whether two fixed-point states contain identical bindings and resource states.
    /// </summary>
    /// <param name="other">The state to compare.</param>
    /// <returns><see langword="true"/> when another propagation would make no change.</returns>
    internal bool HasSameState(LifetimeObjectStore other)
    {
        if (_objects.Count != other._objects.Count
            || _allObjects.Count != other._allObjects.Count
            || _captureOrigins.Count != other._captureOrigins.Count
            || _asyncDisposeResults.Count != other._asyncDisposeResults.Count)
        {
            return false;
        }

        foreach (var pair in _objects)
        {
            if (!other._objects.TryGetValue(pair.Key, out var otherResource)
                || pair.Value.Identity != otherResource.Identity)
            {
                return false;
            }
        }

        return _allObjects.All(pair =>
            other._allObjects.TryGetValue(pair.Key, out var otherResource)
            && pair.Value.HasSameState(otherResource))
            && _captureOrigins.All(pair =>
                other._captureOrigins.TryGetValue(pair.Key, out var otherOrigin)
                && ReferenceEquals(pair.Value, otherOrigin))
            && _asyncDisposeResults.All(pair =>
                other._asyncDisposeResults.TryGetValue(pair.Key, out var otherResource)
                && pair.Value.Identity == otherResource.Identity);
    }

    /// <summary>
    /// Replaces this mutable store with a previously computed snapshot.
    /// </summary>
    /// <param name="source">The snapshot to adopt.</param>
    internal void ReplaceWith(LifetimeObjectStore source)
    {
        _objects.Clear();
        _allObjects.Clear();
        _captureOrigins.Clear();
        _asyncDisposeResults.Clear();
        foreach (var pair in source._allObjects)
        {
            _allObjects.Add(pair.Key, pair.Value);
        }

        foreach (var pair in source._objects)
        {
            _objects.Add(pair.Key, pair.Value);
        }

        foreach (var pair in source._captureOrigins)
        {
            _captureOrigins.Add(pair.Key, pair.Value);
        }

        foreach (var pair in source._asyncDisposeResults)
        {
            _asyncDisposeResults.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Joins two branch states conservatively.
    /// </summary>
    /// <param name="first">The state reaching from one predecessor.</param>
    /// <param name="second">The state reaching from another predecessor.</param>
    /// <returns>A state containing possible resource states and only aliases valid on both paths.</returns>
    internal static LifetimeObjectStore Merge(LifetimeObjectStore first, LifetimeObjectStore second)
    {
        var merged = first.Clone();

        // Resources are joined by stable identity. Symbol aliases survive only when both paths bind the same identity.
        foreach (var pair in second._allObjects)
        {
            if (merged._allObjects.TryGetValue(pair.Key, out var existing))
            {
                existing.MergeFrom(pair.Value);
            }
            else
            {
                merged._allObjects.Add(pair.Key, pair.Value.Clone());
            }
        }

        foreach (var pair in second._objects)
        {
            if (first._objects.TryGetValue(pair.Key, out var firstResource)
                && firstResource.Identity == pair.Value.Identity)
            {
                merged._objects[pair.Key] = merged._allObjects[pair.Value.Identity];
            }
            else
            {
                merged._objects.Remove(pair.Key);
            }
        }

        foreach (var local in first._objects.Keys.Where(local => !second._objects.ContainsKey(local)).ToList())
        {
            merged._objects.Remove(local);
        }

        foreach (var pair in second._captureOrigins)
        {
            if (first._captureOrigins.TryGetValue(pair.Key, out var firstOrigin)
                && (ReferenceEquals(firstOrigin, pair.Value)
                    || ReferencesSameLifetimeObject(first, firstOrigin, second, pair.Value)))
            {
                merged._captureOrigins[pair.Key] = pair.Value;
            }
            else
            {
                merged._captureOrigins.Remove(pair.Key);
            }
        }

        foreach (var pair in second._asyncDisposeResults)
        {
            if (first._asyncDisposeResults.TryGetValue(pair.Key, out var firstResource)
                && firstResource.Identity == pair.Value.Identity)
            {
                merged._asyncDisposeResults[pair.Key] = merged._allObjects[pair.Value.Identity];
            }
            else
            {
                merged._asyncDisposeResults.Remove(pair.Key);
            }
        }

        foreach (var local in first._asyncDisposeResults.Keys.Where(local => !second._asyncDisposeResults.ContainsKey(local)).ToList())
        {
            merged._asyncDisposeResults.Remove(local);
        }

        return merged;
    }

    private static bool ReferencesSameLifetimeObject(
        LifetimeObjectStore firstStore,
        IOperation firstOperation,
        LifetimeObjectStore secondStore,
        IOperation secondOperation)
    {
        return firstStore.TryResolveLifetimeObject(firstOperation, out var firstResource)
            && secondStore.TryResolveLifetimeObject(secondOperation, out var secondResource)
            && firstResource.Identity == secondResource.Identity;
    }

    private static ILocalSymbol? GetAsyncDisposeResultLocal(IOperation? operation)
    {
        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IConversionOperation conversion => GetAsyncDisposeResultLocal(conversion.Operand),
            IInvocationOperation { TargetMethod.Name: "ConfigureAwait" or "AsTask" or "Preserve", Instance: { } instance, } =>
                GetAsyncDisposeResultLocal(instance),
            _ => null,
        };
    }

    /// <summary>
    /// Assigns deterministic identities by acquisition location across fixed-point iterations.
    /// </summary>
    private sealed class IdentityProvider
    {
        private readonly Dictionary<(SyntaxTree? Tree, TextSpan Span), int> _identities = [];

        private readonly HashSet<int> _guaranteedReleaseIdentities = [];

        /// <summary>
        /// Gets or creates the identity for an acquisition site.
        /// </summary>
        /// <param name="location">The resource acquisition location.</param>
        /// <returns>The stable integer identity.</returns>
        internal int GetIdentity(Location location)
        {
            var key = (location.SourceTree, location.SourceSpan);
            if (_identities.TryGetValue(key, out var identity))
            {
                return identity;
            }

            identity = _identities.Count + 1;
            _identities.Add(key, identity);
            return identity;
        }

        /// <summary>
        /// Records that a resource identity is released by a guaranteed loop.
        /// </summary>
        /// <param name="identity">The resource identity.</param>
        internal void MarkGuaranteedRelease(int identity)
        {
            _guaranteedReleaseIdentities.Add(identity);
        }

        /// <summary>
        /// Determines whether a resource identity has a guaranteed-loop release.
        /// </summary>
        /// <param name="identity">The resource identity.</param>
        /// <returns><see langword="true"/> when the identity is recorded.</returns>
        internal bool IsGuaranteedReleased(int identity)
        {
            return _guaranteedReleaseIdentities.Contains(identity);
        }
    }
}