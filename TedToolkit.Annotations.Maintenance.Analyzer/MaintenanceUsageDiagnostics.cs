// -----------------------------------------------------------------------
// <copyright file="MaintenanceUsageDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Defines diagnostic descriptors for maintenance annotations.
/// </summary>
internal static class MaintenanceUsageDiagnostics
{
    /// <summary>
    /// Reports invocation of a workaround.
    /// </summary>
    internal static readonly DiagnosticDescriptor WorkaroundInvoked = CreateDiagnostic("TAM100");

    /// <summary>
    /// Reports invocation of a temporary implementation.
    /// </summary>
    internal static readonly DiagnosticDescriptor TemporaryImplementationInvoked = CreateDiagnostic("TAM101");

    /// <summary>
    /// Reports invocation of technical debt.
    /// </summary>
    internal static readonly DiagnosticDescriptor TechnicalDebtInvoked = CreateDiagnostic("TAM102");

    /// <summary>
    /// Reports invocation that requires cleanup.
    /// </summary>
    internal static readonly DiagnosticDescriptor CleanupRequiredInvoked = CreateDiagnostic("TAM103");

    private static DiagnosticDescriptor CreateDiagnostic(string id)
    {
        return new(
            id: id,
            title: DiagnosticResources.Get($"{id}Title"),
            messageFormat: DiagnosticResources.Get("MaintenanceUsageMessage"),
            category: "Maintenance",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);
    }
}