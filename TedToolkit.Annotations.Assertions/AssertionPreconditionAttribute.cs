// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Assertions;

/// <summary>
/// Provides the common base type for generated preconditions backed by an assertion item.
/// </summary>
/// <param name="description">The documented condition.</param>
/// <param name="reason">The reason the condition is required, if specified.</param>
/// <param name="exceptionType">The documented exception type, if any.</param>
public abstract class AssertionPreconditionAttribute(string description, string? reason = null, Type? exceptionType = null)
    : PreconditionAttribute(reason ?? description, exceptionType);