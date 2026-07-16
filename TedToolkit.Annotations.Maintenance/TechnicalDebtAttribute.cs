// -----------------------------------------------------------------------
// <copyright file="TechnicalDebtAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Maintenance;

/// <summary>
/// Marks an intentional, categorized trade-off that should be repaid when its cost outweighs its benefit.
/// </summary>
/// <param name="kind">The area affected by the technical debt.</param>
/// <param name="reason">The trade-off and its maintenance cost.</param>
public sealed class TechnicalDebtAttribute(TechnicalDebtKind kind, string reason)
    : MaintenanceAttribute(reason)
{
    /// <summary>
    /// Gets the area affected by the technical debt.
    /// </summary>
    public TechnicalDebtKind Kind { get; } = kind;
}