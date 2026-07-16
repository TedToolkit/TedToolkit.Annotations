// -----------------------------------------------------------------------
// <copyright file="SideEffectAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an observable effect caused by the annotated member.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public class SideEffectAttribute : OperationalAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SideEffectAttribute"/> class without a standardized category.
    /// </summary>
    /// <param name="description">The observable effect.</param>
    public SideEffectAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SideEffectAttribute"/> class with a standardized category.
    /// </summary>
    /// <param name="kind">The observable effect category.</param>
    /// <param name="description">The affected boundary, state, resource, or recipient.</param>
    public SideEffectAttribute(SideEffectKind kind, string description)
    {
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// Gets the standardized observable effect category, if specified.
    /// </summary>
    public SideEffectKind? Kind { get; }

    /// <summary>
    /// Gets the observable effect.
    /// </summary>
    public string Description { get; }
}