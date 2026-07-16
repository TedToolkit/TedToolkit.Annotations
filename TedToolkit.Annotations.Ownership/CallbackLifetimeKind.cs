// -----------------------------------------------------------------------
// <copyright file="CallbackLifetimeKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Ownership;

/// <summary>
/// Identifies how long a receiving member can retain a callback parameter.
/// </summary>
public enum CallbackLifetimeKind
{
    /// <summary>
    /// The callback, if invoked, is invoked before the current call returns and is not retained.
    /// </summary>
    IMMEDIATE = 0,

    /// <summary>
    /// The callback can be retained after the current call returns and invoked later.
    /// </summary>
    DEFERRED = 1,

    /// <summary>
    /// The callback is retained until unsubscription or disposal and can be invoked repeatedly.
    /// </summary>
    SUBSCRIPTION = 2,
}