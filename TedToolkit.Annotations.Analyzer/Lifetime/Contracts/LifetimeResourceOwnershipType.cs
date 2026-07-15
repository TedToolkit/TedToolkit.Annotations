// -----------------------------------------------------------------------
// <copyright file="LifetimeResourceOwnershipType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Identifies who must release a resource.
/// </summary>
internal enum LifetimeResourceOwnershipType
{
    /// <summary>
    /// The current scope owns disposal.
    /// </summary>
    OWNED = 0,

    /// <summary>
    /// Another scope owns disposal.
    /// </summary>
    BORROWED = 1,
}