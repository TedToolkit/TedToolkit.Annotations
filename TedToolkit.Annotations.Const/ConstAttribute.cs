// -----------------------------------------------------------------------
// <copyright file="ConstAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Const;

/// <summary>
/// Declares that the annotated parameter, method, or property does not mutate selected depths of an object graph.
/// </summary>
/// <param name="depths">The depths protected from mutation. Defaults to all 32 depths.</param>
/// <remarks>
/// <para>
/// Applied to a parameter, <see cref="ConstDepth.DEPTH0"/> prevents reassignment of the parameter variable itself.
/// Each following depth protects one further field or property access: <see cref="ConstDepth.DEPTH1"/> protects a
/// direct member, and <see cref="ConstDepth.DEPTH2"/> protects a direct member's member.
/// </para>
/// <para>
/// Applied to an instance method, depths are relative to the current instance. <see cref="ConstDepth.DEPTH0"/>
/// protects direct fields and properties of <see langword="this"/>; <see cref="ConstDepth.DEPTH1"/> protects their
/// fields and properties; and each following depth adds one member access.
/// </para>
/// <para>
/// Applied to a static method or property, depths are relative to the static state of its declaring type.
/// <see cref="ConstDepth.DEPTH0"/> protects its direct static fields and properties, and each following depth
/// protects one further member access.
/// </para>
/// <para>
/// Applied to a type, this contract supplies the default for its static members. A static member with an explicit
/// <see cref="ConstAttribute"/> overrides the type-level default.
/// </para>
/// <para>
/// Applied to a property, this contract applies to both accessors unless an accessor has its own
/// <see cref="ConstAttribute"/>. Without an explicit contract, a getter protects all depths and a setter protects
/// <see cref="ConstDepth.DEPTH1_OR_GREATER"/>. Therefore, a setter can write any direct field or property of the
/// current instance, but cannot mutate a member of those direct members.
/// </para>
/// <para>
/// Use a single <see cref="ConstDepth.DEPTH0_OR_GREATER"/> through <see cref="ConstDepth.DEPTH31_OR_GREATER"/> value
/// to protect a depth and every deeper depth. The bundled analyzer reports writes to protected depths and requires
/// compatible const contracts when protected receivers or values are passed to methods. Incompatible source calls
/// are errors; unverifiable external calls are informational diagnostics. Do not apply this attribute to an
/// <see langword="out"/> parameter because its value originates in the method rather than with the caller. Static
/// members may also be annotated. Overrides and interface implementations combine const contracts
/// declared by their base or interface members and parameters.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Const(ConstDepth.DEPTH0_OR_GREATER)]
/// public void Inspect([Const] Node node)
/// {
///     // node.Value = 1; // TAC300: parameter depth 1 is protected.
///     // Value = 1;      // TAC300: method depth 0 is protected.
/// }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter,
    Inherited = false)]
public sealed class ConstAttribute(ConstDepth depths = ConstDepth.ALL) : Attribute
{
    /// <summary>
    /// Gets the object-graph depths protected from mutation.
    /// </summary>
    public ConstDepth Depths { get; } = depths;
}