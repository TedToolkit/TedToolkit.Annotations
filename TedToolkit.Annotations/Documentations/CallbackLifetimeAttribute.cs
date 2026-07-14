// -----------------------------------------------------------------------
// <copyright file="CallbackLifetimeAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents whether the receiving member invokes or retains the annotated callback parameter.
/// </summary>
/// <param name="kind">The callback lifetime.</param>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class CallbackLifetimeAttribute(CallbackLifetimeKind kind) : DocumentationAttribute
{
    /// <summary>
    /// Gets the callback lifetime.
    /// </summary>
    public CallbackLifetimeKind Kind { get; } = kind;
}