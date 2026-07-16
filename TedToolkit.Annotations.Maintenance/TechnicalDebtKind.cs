// -----------------------------------------------------------------------
// <copyright file="TechnicalDebtKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Maintenance;

/// <summary>
/// Identifies the area affected by a technical debt decision.
/// </summary>
public enum TechnicalDebtKind
{
    /// <summary>
    /// A compromise in the design, structure, or extensibility of the code.
    /// </summary>
    DESIGN = 0,

    /// <summary>
    /// A legacy behavior or API retained for compatibility.
    /// </summary>
    COMPATIBILITY = 1,

    /// <summary>
    /// A known trade-off in execution time, memory use, allocation, or throughput.
    /// </summary>
    PERFORMANCE = 2,

    /// <summary>
    /// A known fragility or limitation affecting resilience, error handling, or recovery.
    /// </summary>
    RELIABILITY = 3,
}