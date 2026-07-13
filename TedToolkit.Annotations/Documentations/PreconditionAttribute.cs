// -----------------------------------------------------------------------
// <copyright file="PreconditionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a condition that must hold before the annotated member is used.
/// </summary>
/// <param name="description">The required condition.</param>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property
    | AttributeTargets.Parameter,
    AllowMultiple = true,
    Inherited = false)]
public sealed class PreconditionAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the required condition.
    /// </summary>
    public string Description { get; } = description;
}