// -----------------------------------------------------------------------
// <copyright file="ThreadSafetyAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents the thread-safety guarantees and synchronization requirements of the annotated code.
/// </summary>
/// <param name="description">The thread-safety guarantee or synchronization requirement.</param>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public sealed class ThreadSafetyAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the thread-safety guarantee or synchronization requirement.
    /// </summary>
    public string Description { get; } = description;
}