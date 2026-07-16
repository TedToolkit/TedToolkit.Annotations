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
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public class ThreadSafetyAttribute : OperationalAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafetyAttribute"/> class without a standardized category.
    /// </summary>
    /// <param name="description">The thread-safety guarantee or synchronization requirement.</param>
    public ThreadSafetyAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafetyAttribute"/> class with a standardized category.
    /// </summary>
    /// <param name="kind">The concurrency guarantee category.</param>
    /// <param name="description">The conditions, synchronization mechanism, or unsupported scenario.</param>
    public ThreadSafetyAttribute(ThreadSafetyKind kind, string description)
    {
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// Gets the standardized concurrency guarantee category, if specified.
    /// </summary>
    public ThreadSafetyKind? Kind { get; }

    /// <summary>
    /// Gets the thread-safety guarantee or synchronization requirement.
    /// </summary>
    public string Description { get; }
}