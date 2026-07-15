// -----------------------------------------------------------------------
// <copyright file="InvariantAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a condition that must remain true for the annotated type.
/// </summary>
/// <param name="description">The invariant.</param>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Property,
    AllowMultiple = true)]
public sealed class InvariantAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the invariant.
    /// </summary>
    public string Description { get; } = description;
}