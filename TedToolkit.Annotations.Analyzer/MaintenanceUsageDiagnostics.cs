// -----------------------------------------------------------------------
// <copyright file="MaintenanceUsageDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

internal static class MaintenanceUsageDiagnostics
{
    internal static readonly DiagnosticDescriptor WorkaroundInvoked = CreateDiagnostic(
        "TTA100",
        "Workaround API is invoked");

    internal static readonly DiagnosticDescriptor TemporaryImplementationInvoked = CreateDiagnostic(
        "TTA101",
        "Temporary implementation is invoked");

    internal static readonly DiagnosticDescriptor TechnicalDebtInvoked = CreateDiagnostic(
        "TTA102",
        "Technical-debt API is invoked");

    internal static readonly DiagnosticDescriptor CleanupRequiredInvoked = CreateDiagnostic(
        "TTA103",
        "Cleanup-required API is invoked");

    private static DiagnosticDescriptor CreateDiagnostic(string id, string title) =>
        new(
            id: id,
            title: title,
            messageFormat: "The {0} '{1}' is invoked. Reason: {2}. {3}",
            category: "Maintenance",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);
}
