// -----------------------------------------------------------------------
// <copyright file="SideEffectAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an observable state change caused by the annotated member.
/// </summary>
/// <param name="description">The observable state change.</param>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public sealed class SideEffectAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the observable state change.
    /// </summary>
    public string Description { get; } = description;
}