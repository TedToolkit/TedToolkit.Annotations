// -----------------------------------------------------------------------
// <copyright file="AssumptionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents an external fact or convention the annotated code relies on but does not verify.
/// </summary>
/// <param name="description">The assumption the code relies on.</param>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public class AssumptionAttribute(string description) : DocumentationAttribute
{
    /// <summary>
    /// Gets the assumption the code relies on.
    /// </summary>
    public string Description { get; } = description;
}