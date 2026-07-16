// -----------------------------------------------------------------------
// <copyright file="PreconditionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a condition that must hold before the annotated member is used.
/// </summary>
/// <param name="description">The required condition.</param>
/// <param name="exceptionType">The exception thrown when the condition is not met, if any.</param>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Parameter,
    AllowMultiple = true,
    Inherited = false)]
public class PreconditionAttribute(string description, Type? exceptionType = null) : ContractAttribute
{
    /// <summary>
    /// Gets the required condition.
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// Gets the exception thrown when the condition is not met, if specified.
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