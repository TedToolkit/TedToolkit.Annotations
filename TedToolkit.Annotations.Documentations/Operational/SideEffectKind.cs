// -----------------------------------------------------------------------
// <copyright file="SideEffectKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Identifies the category of an observable effect caused by an annotated operation.
/// </summary>
public enum SideEffectKind
{
    /// <summary>
    /// Mutates the state of the current instance or an object owned by it.
    /// </summary>
    INSTANCE_STATE_MUTATION = 0,

    /// <summary>
    /// Mutates static or process-wide managed state.
    /// </summary>
    STATIC_STATE_MUTATION = 1,

    /// <summary>
    /// Mutates state outside the managed process, such as a database, file system, operating-system setting, or remote service.
    /// </summary>
    EXTERNAL_STATE_MUTATION = 2,

    /// <summary>
    /// Reads from or writes to an input or output boundary, including streams, files, sockets, consoles, and remote services.
    /// </summary>
    INPUT_OUTPUT = 3,

    /// <summary>
    /// Publishes a notification, including a .NET event, message, diagnostic, metric, trace, or log entry.
    /// </summary>
    NOTIFICATION_PUBLICATION = 4,

    /// <summary>
    /// Invokes caller-provided, user-provided, or otherwise externally controlled code.
    /// </summary>
    CALLBACK_INVOCATION = 5,

    /// <summary>
    /// Acquires a resource whose lifetime must subsequently be managed.
    /// </summary>
    RESOURCE_ACQUISITION = 6,

    /// <summary>
    /// Releases, disposes, closes, cancels, or otherwise ends the lifetime of a resource or operation.
    /// </summary>
    RESOURCE_RELEASE = 7,

    /// <summary>
    /// Describes an observable effect outside the standard categories. The attribute description must state its nature and boundary.
    /// </summary>
    OTHER = 8,
}