// -----------------------------------------------------------------------
// <copyright file="OwnershipKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Ownership;

/// <summary>
/// Specifies whether ownership changes while a value crosses an API boundary.
/// <see cref="UNCHANGED"/> is the CLR default enum value, but unannotated API boundaries use target-specific analyzer defaults.
/// </summary>
public enum OwnershipKind
{
    /// <summary>
    /// Ownership does not change; the receiving side only borrows the value. This is the underlying enum value zero.
    /// </summary>
    UNCHANGED = 0,

    /// <summary>
    /// Ownership transfers to the receiving side.
    /// </summary>
    TRANSFERRED = 1,
}