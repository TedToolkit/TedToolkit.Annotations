// -----------------------------------------------------------------------
// <copyright file="ThreadAffinityAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents the required thread or synchronization context for the annotated code.
/// </summary>
/// <param name="description">The required thread or synchronization context.</param>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public sealed class ThreadAffinityAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the required thread or synchronization context.
    /// </summary>
    public string Description { get; } = description;
}