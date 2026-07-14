// -----------------------------------------------------------------------
// <copyright file="MayBlockAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents that the annotated operation can block the calling thread.
/// </summary>
/// <param name="description">The condition or operation that can block the calling thread.</param>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    Inherited = false)]
public sealed class MayBlockAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the condition or operation that can block the calling thread.
    /// </summary>
    public string Description { get; } = description;
}