// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an input condition and its expected behavior.
/// </summary>
/// <param name="condition">The condition under which this behavior applies.</param>
/// <param name="expected">The expected behavior.</param>
/// <param name="hasUnitTest">Whether a unit test covers this behavior.</param>
/// <param name="exceptionType">The exception expected when this behavior fails, if any.</param>
[AttributeUsage(
    AttributeTargets.Constructor
    | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
[Conditional("ANNOTATIONS_BEHAVIOR_CASE")]
public class BehaviorCaseAttribute(
    string condition,
    string expected,
    bool hasUnitTest = false,
    Type? exceptionType = null) : DocumentationAttribute
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
    /// Gets the exception expected when this behavior fails, if specified.
    /// </summary>
    public Type? ExceptionType { get; } = ValidateExceptionType(exceptionType);

    private static Type? ValidateExceptionType(Type? exceptionType)
    {
        if (exceptionType is not null && !typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException("The type must derive from Exception.", nameof(exceptionType));
        }

        return exceptionType;
    }
}