// -----------------------------------------------------------------------
// <copyright file="LifetimeOwnershipFlowType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Identifies the direction in which an ownership contract applies.
/// </summary>
internal enum LifetimeOwnershipFlowType
{
    /// <summary>
    /// Uses the default ownership flow.
    /// </summary>
    DEFAULT = 0,

    /// <summary>
    /// Uses ownership entering a member.
    /// </summary>
    INPUT = 1,

    /// <summary>
    /// Uses ownership leaving a member.
    /// </summary>
    OUTPUT = 2,
}