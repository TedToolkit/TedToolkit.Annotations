// -----------------------------------------------------------------------
// <copyright file="MaintenanceAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace TedToolkit.Annotations.Maintenance;

/// <summary>
/// Provides shared metadata for source-only maintenance annotations.
/// </summary>
/// <param name="reason">The reason the annotated code needs maintenance.</param>
[AttributeUsage(
    AttributeTargets.Constructor
    | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
[Conditional("ANNOTATIONS_MAINTENANCE")]
public abstract class MaintenanceAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets the reason the annotated code needs maintenance.
    /// </summary>
    public string Reason { get; } = reason;

    /// <summary>
    /// Gets or sets the condition that makes this annotation removable.
    /// </summary>
    public string? RemoveWhen { get; set; }
}