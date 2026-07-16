// -----------------------------------------------------------------------
// <copyright file="ThreadSafetyKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Identifies the concurrency guarantee of an annotated API.
/// </summary>
public enum ThreadSafetyKind
{
    /// <summary>
    /// Concurrent calls are safe without caller-provided synchronization.
    /// </summary>
    THREAD_SAFE = 0,

    /// <summary>
    /// Concurrent calls are safe only when the documented conditions hold.
    /// </summary>
    CONDITIONALLY_THREAD_SAFE = 1,

    /// <summary>
    /// Concurrent calls are unsafe and callers must provide synchronization.
    /// </summary>
    EXTERNAL_SYNCHRONIZATION_REQUIRED = 2,

    /// <summary>
    /// Concurrent calls are unsupported, including when caller-provided synchronization cannot make the operation safe.
    /// </summary>
    NOT_THREAD_SAFE = 3,
}