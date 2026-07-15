// -----------------------------------------------------------------------
// <copyright file="LifetimeOwnershipTransferType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Describes whether an operation changes ownership of a lifetime object.
/// </summary>
internal enum LifetimeOwnershipTransferType
{
    /// <summary>
    /// The operation leaves ownership with the current owner.
    /// </summary>
    UNCHANGED = 0,

    /// <summary>
    /// The operation transfers ownership to another owner.
    /// </summary>
    TRANSFERRED = 1,
}