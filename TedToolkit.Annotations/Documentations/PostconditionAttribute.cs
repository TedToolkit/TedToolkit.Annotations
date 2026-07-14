// -----------------------------------------------------------------------
// <copyright file="PostconditionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a condition guaranteed after the annotated member completes successfully.
/// </summary>
/// <param name="description">The guaranteed condition.</param>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Parameter
    | AttributeTargets.ReturnValue,
    AllowMultiple = true,
    Inherited = false)]
public sealed class PostconditionAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the guaranteed condition.
    /// </summary>
    public string Description { get; } = description;
}