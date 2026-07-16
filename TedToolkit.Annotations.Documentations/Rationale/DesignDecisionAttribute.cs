// -----------------------------------------------------------------------
// <copyright file="DesignDecisionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a durable design decision and the reason for making it.
/// </summary>
/// <param name="id">The stable identifier of the decision, such as an ADR or issue number.</param>
/// <param name="decision">The selected design approach.</param>
/// <param name="rationale">Why the selected approach was chosen.</param>
/// <param name="alternatives">The rejected alternatives and their reasons, if any.</param>
[AttributeUsage(
    AttributeTargets.Assembly
    | AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public class DesignDecisionAttribute(
    string id,
    string decision,
    string rationale,
    string? alternatives = null) : RationaleAttribute
{
    /// <summary>
    /// Gets the stable identifier of the decision.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets the selected design approach.
    /// </summary>
    public string Decision { get; } = decision;

    /// <summary>
    /// Gets why the selected approach was chosen.
    /// </summary>
    public string Rationale { get; } = rationale;

    /// <summary>
    /// Gets the rejected alternatives and their reasons, if specified.
    /// </summary>
    public string? Alternatives { get; } = alternatives;
}