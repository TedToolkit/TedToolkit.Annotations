// -----------------------------------------------------------------------
// <copyright file="MayBlockKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Identifies the source that can block an annotated operation.
/// </summary>
public enum MayBlockKind
{
    /// <summary>
    /// Waits to acquire a lock, monitor, semaphore, mutex, or other synchronization primitive.
    /// </summary>
    SYNCHRONIZATION = 0,

    /// <summary>
    /// Performs synchronous input or output, including streams, files, sockets, consoles, and remote services.
    /// </summary>
    INPUT_OUTPUT = 1,

    /// <summary>
    /// Waits for a task, handle, timer, signal, or other operation to complete.
    /// </summary>
    WAIT = 2,

    /// <summary>
    /// Invokes external code that can block the calling thread.
    /// </summary>
    CALLBACK = 3,

    /// <summary>
    /// Starts, communicates with, or waits for an external process.
    /// </summary>
    EXTERNAL_PROCESS = 4,

    /// <summary>
    /// Describes a blocking source outside the standard categories. The attribute description must state the source and condition.
    /// </summary>
    OTHER = 5,
}