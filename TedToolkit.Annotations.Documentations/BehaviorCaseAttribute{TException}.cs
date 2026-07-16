// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseAttribute{TException}.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an input condition, its expected behavior, and the exception expected when that behavior fails.
/// </summary>
/// <typeparam name="TException">The exception expected when this behavior fails.</typeparam>
/// <param name="condition">The condition under which this behavior applies.</param>
/// <param name="expected">The expected behavior.</param>
/// <param name="hasUnitTest">Whether a unit test covers this behavior.</param>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public sealed class BehaviorCaseAttribute<TException>(
    string condition,
    string expected,
    bool hasUnitTest = false) : DocumentationAttribute
    where TException : Exception
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

    /// <summary>
    /// Gets the exception expected when this behavior fails.
    /// </summary>
    public Type ExceptionType { get; } = typeof(TException);
}