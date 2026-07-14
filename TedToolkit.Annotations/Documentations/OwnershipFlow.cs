// -----------------------------------------------------------------------
// <copyright file="OwnershipFlow.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Identifies the direction in which a value crosses an API boundary.
/// </summary>
public enum OwnershipFlow
{
    /// <summary>
    /// Infers the default direction from the annotated API boundary.
    /// Return values and properties default to <see cref="OUTPUT"/>; non-<see langword="ref"/> parameters default to <see cref="INPUT"/>.
    /// </summary>
    DEFAULT = 0,

    /// <summary>
    /// The value flows from the caller to the receiving member or property setter.
    /// </summary>
    INPUT = 1,

    /// <summary>
    /// The value flows from the receiving member or property getter to the caller.
    /// </summary>
    OUTPUT = 2,
}