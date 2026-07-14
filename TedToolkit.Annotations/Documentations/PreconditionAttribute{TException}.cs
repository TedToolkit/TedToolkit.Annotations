// -----------------------------------------------------------------------
// <copyright file="PreconditionAttribute{TException}.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a condition that must hold before the annotated member is used and the exception thrown when it is not met.
/// </summary>
/// <typeparam name="TException">The exception thrown when the condition is not met.</typeparam>
/// <param name="description">The required condition.</param>
public sealed class PreconditionAttribute<TException>(string description)
    : PreconditionAttribute(description, typeof(TException))
    where TException : Exception;