// -----------------------------------------------------------------------
// <copyright file="OwnershipAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Ownership;

/// <summary>
/// Documents whether ownership changes while the annotated value crosses an API boundary or is stored in a field.
/// The <paramref name="kind"/> argument is required; when the attribute is absent, the analyzer applies boundary-specific defaults.
/// Method returns and <see langword="out"/> parameters transfer ownership by default, while ordinary parameters and property getters are borrowed by default.
/// Specify <paramref name="flow"/> for <see langword="ref"/> parameters and properties whose input and output ownership differ.
/// For a property, <see cref="OwnershipFlow.INPUT"/> applies to its setter and <see cref="OwnershipFlow.OUTPUT"/> applies to its getter.
/// </summary>
/// <param name="kind">Whether ownership changes in the selected flow. This argument is required.</param>
/// <param name="flow">The value flow; inferred when omitted.</param>
[AttributeUsage(
    AttributeTargets.ReturnValue | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false)]
public sealed class OwnershipAttribute(
    OwnershipKind kind,
    OwnershipFlow flow = OwnershipFlow.DEFAULT) : Attribute
{
    /// <summary>
    /// Gets whether ownership changes.
    /// </summary>
    public OwnershipKind Kind { get; } = kind;

    /// <summary>
    /// Gets the value flow.
    /// </summary>
    public OwnershipFlow Flow { get; } = flow;
}