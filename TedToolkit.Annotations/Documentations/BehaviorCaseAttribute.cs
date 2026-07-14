// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an input condition and its expected behavior.
/// </summary>
/// <param name="condition">The condition under which this behavior applies.</param>
/// <param name="expected">The expected behavior.</param>
/// <param name="hasUnitTest">Whether a unit test covers this behavior.</param>
[AttributeUsage(
    AttributeTargets.Constructor
    | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class BehaviorCaseAttribute(
    string condition,
    string expected,
    bool hasUnitTest = false) : DocumentationAttribute
{
    /// <summary>
    /// Gets the condition under which this behavior applies.
    /// </summary>
    public string Condition { get; } = condition;

    /// <summary>
    /// Gets the expected behavior.
    /// </summary>
    public string Expected { get; } = expected;

    /// <summary>
    /// Gets a value indicating whether a unit test covers this behavior.
    /// </summary>
    public bool HasUnitTest { get; } = hasUnitTest;
}