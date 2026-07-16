// -----------------------------------------------------------------------
// <copyright file="BoxingDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Boxing;

/// <summary>
/// Defines diagnostics for implicit or cast-based boxing.
/// </summary>
internal static class BoxingDiagnostics
{
    /// <summary>
    /// Reports a boxing conversion that should be expressed through <c>Boxer.Box</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor BoxingMustBeExplicit = CreateDiagnostic(BoxingAnalyzer.DIAGNOSTIC_ID);

    private static DiagnosticDescriptor CreateDiagnostic(string id)
    {
        return new(
            id,
            DiagnosticResources.Get("TAB201Title"),
            DiagnosticResources.Get("TAB201Message"),
            "Performance",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);
    }
}