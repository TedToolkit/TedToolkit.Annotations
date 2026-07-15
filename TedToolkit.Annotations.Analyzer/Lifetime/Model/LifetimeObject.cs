// -----------------------------------------------------------------------
// <copyright file="LifetimeObject.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Model;

/// <summary>
/// Tracks the lifetime state of one owned or borrowed disposable symbol.
/// </summary>
/// <param name="symbol">The local variable or field represented by the object.</param>
/// <param name="creationLocation">The source location where ownership was acquired.</param>
/// <param name="isUsing">Whether a using construct owns disposal.</param>
/// <param name="isBorrowed">Whether disposal belongs to another owner.</param>
internal sealed class LifetimeObject(
    ISymbol symbol,
    Location creationLocation,
    bool isUsing,
    bool isBorrowed)
{
    private static int _nextIdentity;

    private LifetimeObjectStateType _state = LifetimeObjectStateType.OWNED;

    private bool _isReleasedByGuaranteedLoop;

    /// <summary>
    /// Gets the symbol represented by this lifetime object.
    /// </summary>
    internal ISymbol Symbol { get; } = symbol;

    /// <summary>
    /// Gets the location where ownership was acquired.
    /// </summary>
    internal Location CreationLocation { get; } = creationLocation;

    /// <summary>
    /// Gets a value indicating whether the using statement owns disposal.
    /// </summary>
    internal bool IsUsing { get; private set; } = isUsing;

    /// <summary>
    /// Gets a value indicating whether another caller owns disposal.
    /// </summary>
    internal bool IsBorrowed { get; } = isBorrowed;

    /// <summary>
    /// Gets the longest callback lifetime registered for this object.
    /// </summary>
    internal int? CallbackLifetime { get; private set; }

    /// <summary>
    /// Gets the location of an asynchronous release result that still requires observation.
    /// </summary>
    internal Location? PendingAsyncDisposeLocation { get; private set; }

    /// <summary>
    /// Gets the stable resource identity shared by branch snapshots.
    /// </summary>
    internal int Identity { get; } = Interlocked.Increment(ref _nextIdentity);

    /// <summary>
    /// Gets a value indicating whether any reaching path has already disposed the resource.
    /// </summary>
    internal bool HasDisposedState
    {
        get
        {
            return (_state & LifetimeObjectStateType.DISPOSED) != 0;
        }
    }

    /// <summary>
    /// Creates a branch snapshot for an existing logical resource.
    /// </summary>
    /// <param name="symbol">The symbol used to name the resource.</param>
    /// <param name="creationLocation">The acquisition location.</param>
    /// <param name="isUsing">Whether a using construct guarantees disposal.</param>
    /// <param name="isBorrowed">Whether disposal belongs to another owner.</param>
    /// <param name="identity">The stable identity shared by every snapshot of this resource.</param>
    internal LifetimeObject(
        ISymbol symbol,
        Location creationLocation,
        bool isUsing,
        bool isBorrowed,
        int identity)
        : this(symbol, creationLocation, isUsing, isBorrowed)
    {
        Identity = identity;
    }

    /// <summary>
    /// Marks the object as managed by a using statement.
    /// </summary>
    internal void MarkUsing()
    {
        IsUsing = true;
    }

    /// <summary>
    /// Records a callback which captures this object.
    /// </summary>
    /// <param name="callbackLifetime">The lifetime category of the callback.</param>
    internal void RegisterCallbackLifetime(int callbackLifetime)
    {
        CallbackLifetime = Math.Max(CallbackLifetime ?? callbackLifetime, callbackLifetime);
    }

    /// <summary>
    /// Records an asynchronous release result that must be observed.
    /// </summary>
    /// <param name="location">The `DisposeAsync` invocation whose result must be observed.</param>
    internal void RegisterPendingAsyncDispose(Location location)
    {
        PendingAsyncDisposeLocation = location;
    }

    /// <summary>
    /// Marks the asynchronous release result as observed.
    /// </summary>
    internal void ObserveAsyncDispose()
    {
        PendingAsyncDisposeLocation = null;
    }

    /// <summary>
    /// Records that a loop which executes at least once releases this resource.
    /// </summary>
    internal void MarkReleasedByGuaranteedLoop()
    {
        _isReleasedByGuaranteedLoop = true;
    }

    /// <summary>
    /// Disposes the object.
    /// </summary>
    /// <returns>The resulting lifetime transition.</returns>
    internal LifetimeTransitionResultType Dispose()
    {
        if (IsBorrowed)
        {
            return LifetimeTransitionResultType.BORROWED;
        }

        if (IsUsing || _state == LifetimeObjectStateType.DISPOSED)
        {
            return LifetimeTransitionResultType.ALREADY_DISPOSED;
        }

        if (_state == LifetimeObjectStateType.TRANSFERRED)
        {
            return LifetimeTransitionResultType.TRANSFERRED;
        }

        _state = LifetimeObjectStateType.DISPOSED;
        return LifetimeTransitionResultType.SUCCEEDED;
    }

    /// <summary>
    /// Transfers ownership of the object to another owner.
    /// </summary>
    /// <returns>The resulting lifetime transition.</returns>
    internal LifetimeTransitionResultType TransferOwnership()
    {
        if (IsUsing || _state == LifetimeObjectStateType.TRANSFERRED)
        {
            return LifetimeTransitionResultType.TRANSFERRED;
        }

        if (_state == LifetimeObjectStateType.DISPOSED)
        {
            return LifetimeTransitionResultType.DISPOSED;
        }

        _state = _state == LifetimeObjectStateType.OWNED
            ? LifetimeObjectStateType.TRANSFERRED
            : (_state & ~LifetimeObjectStateType.OWNED) | LifetimeObjectStateType.TRANSFERRED;
        return LifetimeTransitionResultType.SUCCEEDED;
    }

    /// <summary>
    /// Uses the object.
    /// </summary>
    /// <returns>The resulting lifetime transition.</returns>
    internal LifetimeTransitionResultType Use()
    {
        if ((_state & LifetimeObjectStateType.OWNED) != 0)
        {
            return LifetimeTransitionResultType.SUCCEEDED;
        }

        return (_state & LifetimeObjectStateType.DISPOSED) != 0
            ? LifetimeTransitionResultType.DISPOSED
            : LifetimeTransitionResultType.TRANSFERRED;
    }

    /// <summary>
    /// Reports an ownership loss caused by replacing the object.
    /// </summary>
    /// <returns>The resulting lifetime transition.</returns>
    internal LifetimeTransitionResultType Overwrite()
    {
        return ReportOwnershipLoss();
    }

    /// <summary>
    /// Reports an ownership loss at the end of the enclosing scope.
    /// </summary>
    /// <returns>The resulting lifetime transition.</returns>
    internal LifetimeTransitionResultType CompleteScope()
    {
        return ReportOwnershipLoss();
    }

    /// <summary>
    /// Creates an analysis snapshot while preserving resource identity.
    /// </summary>
    /// <returns>An independent state snapshot with the same logical identity.</returns>
    internal LifetimeObject Clone()
    {
        return new(this);
    }

    /// <summary>
    /// Merges another control-flow state for the same resource.
    /// </summary>
    /// <param name="other">A state reaching the same control-flow join.</param>
    /// <exception cref="ArgumentException">The supplied object represents a different resource.</exception>
    internal void MergeFrom(LifetimeObject other)
    {
        if (Identity != other.Identity)
        {
            throw new ArgumentException("Only states for the same resource can be merged.", nameof(other));
        }

        // The flags form a may-state set: after a join, every state reachable on either branch is retained.
        _state |= other._state;
        IsUsing |= other.IsUsing;
        if (other.CallbackLifetime is { } callbackLifetime)
        {
            RegisterCallbackLifetime(callbackLifetime);
        }

        PendingAsyncDisposeLocation ??= other.PendingAsyncDisposeLocation;
        _isReleasedByGuaranteedLoop |= other._isReleasedByGuaranteedLoop;
    }

    /// <summary>
    /// Determines whether another snapshot has the same abstract state.
    /// </summary>
    /// <param name="other">The snapshot to compare.</param>
    /// <returns><see langword="true"/> when both snapshots represent the same identity and state.</returns>
    internal bool HasSameState(LifetimeObject other)
    {
        return Identity == other.Identity
            && _state == other._state
            && IsUsing == other.IsUsing
            && IsBorrowed == other.IsBorrowed
            && CallbackLifetime == other.CallbackLifetime
            && Equals(PendingAsyncDisposeLocation, other.PendingAsyncDisposeLocation)
            && _isReleasedByGuaranteedLoop == other._isReleasedByGuaranteedLoop;
    }

    private LifetimeTransitionResultType ReportOwnershipLoss()
    {
        if (IsBorrowed || IsUsing || _isReleasedByGuaranteedLoop || (_state & LifetimeObjectStateType.OWNED) == 0)
        {
            return LifetimeTransitionResultType.SUCCEEDED;
        }

        _state = (_state & ~LifetimeObjectStateType.OWNED) | LifetimeObjectStateType.TRANSFERRED;
        return LifetimeTransitionResultType.OWNERSHIP_LOSS;
    }

    private LifetimeObject(LifetimeObject source)
        : this(source.Symbol, source.CreationLocation, source.IsUsing, source.IsBorrowed)
    {
        Identity = source.Identity;
        _state = source._state;
        CallbackLifetime = source.CallbackLifetime;
        PendingAsyncDisposeLocation = source.PendingAsyncDisposeLocation;
        _isReleasedByGuaranteedLoop = source._isReleasedByGuaranteedLoop;
    }

    /// <summary>
    /// Represents all lifetime states that may reach a control-flow point.
    /// </summary>
    [Flags]
    private enum LifetimeObjectStateType
    {
        NONE = 0,

        OWNED = 1,

        DISPOSED = 1 << 1,

        TRANSFERRED = 1 << 2,
    }
}