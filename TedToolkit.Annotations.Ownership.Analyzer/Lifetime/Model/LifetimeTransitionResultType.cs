// -----------------------------------------------------------------------
// <copyright file="LifetimeTransitionResultType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Lifetime.Model;

/// <summary>
/// Describes the result of one <see cref="LifetimeObject"/> state transition.
/// </summary>
internal enum LifetimeTransitionResultType
{
    /// <summary>
    /// The transition preserved the lifetime contract.
    /// </summary>
    SUCCEEDED = 0,

    /// <summary>
    /// The object belongs to another owner.
    /// </summary>
    BORROWED = 1,

    /// <summary>
    /// The object was already disposed.
    /// </summary>
    ALREADY_DISPOSED = 2,

    /// <summary>
    /// The object was disposed earlier.
    /// </summary>
    DISPOSED = 3,

    /// <summary>
    /// The object was transferred to another owner.
    /// </summary>
    TRANSFERRED = 4,

    /// <summary>
    /// The object would lose an outstanding ownership obligation.
    /// </summary>
    OWNERSHIP_LOSS = 5,
}