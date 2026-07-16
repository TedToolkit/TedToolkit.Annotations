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
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    Inherited = false)]
public class MayBlockAttribute : OperationalAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MayBlockAttribute"/> class without a standardized blocking category.
    /// </summary>
    /// <param name="description">The condition or operation that can block the calling thread.</param>
    public MayBlockAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MayBlockAttribute"/> class with a standardized blocking category.
    /// </summary>
    /// <param name="kind">The source that can block the operation.</param>
    /// <param name="description">The blocking condition and affected boundary.</param>
    public MayBlockAttribute(MayBlockKind kind, string description)
    {
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// Gets the standardized blocking category, if specified.
    /// </summary>
    public MayBlockKind? Kind { get; }

    /// <summary>
    /// Gets the condition or operation that can block the calling thread.
    /// </summary>
    public string Description { get; }
}