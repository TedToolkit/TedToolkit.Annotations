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
public class BehaviorCaseAttribute<TException>(
    string condition,
    string expected,
    bool hasUnitTest = false)
    : BehaviorCaseAttribute(condition, expected, hasUnitTest, typeof(TException))
    where TException : Exception;