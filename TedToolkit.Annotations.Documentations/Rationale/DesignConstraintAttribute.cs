// -----------------------------------------------------------------------
// <copyright file="DesignConstraintAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a design boundary that must be preserved when maintaining the annotated code.
/// </summary>
/// <param name="constraint">The design boundary that must be preserved.</param>
/// <param name="rationale">Why violating the boundary would be unsafe or incorrect.</param>
[AttributeUsage(
    AttributeTargets.Assembly
    | AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property
    | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false)]
public class DesignConstraintAttribute(string constraint, string rationale) : RationaleAttribute
{
    /// <summary>
    /// Gets the design boundary that must be preserved.
    /// </summary>
    public string Constraint { get; } = constraint;

    /// <summary>
    /// Gets why violating the boundary would be unsafe or incorrect.
    /// </summary>
    public string Rationale { get; } = rationale;
}