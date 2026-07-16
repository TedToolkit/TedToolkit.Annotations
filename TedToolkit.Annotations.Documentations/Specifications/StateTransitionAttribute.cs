// -----------------------------------------------------------------------
// <copyright file="StateTransitionAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents a successful transition between two states caused by the annotated API.
/// </summary>
/// <param name="fromState">The state required before the transition.</param>
/// <param name="toState">The state guaranteed after the transition completes successfully.</param>
/// <param name="condition">The condition under which the transition applies, if any.</param>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Interface
    | AttributeTargets.Method
    | AttributeTargets.Constructor
    | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
public class StateTransitionAttribute(string fromState, string toState, string? condition = null)
    : SpecificationAttribute
{
    /// <summary>
    /// Gets the state required before the transition.
    /// </summary>
    public string FromState { get; } = fromState;

    /// <summary>
    /// Gets the state guaranteed after the transition completes successfully.
    /// </summary>
    public string ToState { get; } = toState;

    /// <summary>
    /// Gets the condition under which the transition applies, if specified.
    /// </summary>
    public string? Condition { get; } = condition;
}