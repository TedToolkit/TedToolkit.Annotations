// -----------------------------------------------------------------------
// <copyright file="NotNullIfNotNullAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

#if NET472 || NET48 || NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Specifies that an output is non-null when the named input is non-null.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
internal sealed class NotNullIfNotNullAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotNullIfNotNullAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">The parameter whose null state determines the output null state.</param>
    public NotNullIfNotNullAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets the parameter whose null state determines the output null state.
    /// </summary>
    public string ParameterName { get; }
}
#endif