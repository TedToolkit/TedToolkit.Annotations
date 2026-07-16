// -----------------------------------------------------------------------
// <copyright file="DocumentationDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Defines diagnostics for documentation annotations.
/// </summary>
internal static class DocumentationDiagnostics
{
    /// <summary>
    /// Describes available precondition exception documentation.
    /// </summary>
    internal static readonly DiagnosticDescriptor PreconditionDocumentation = new(
        PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID,
        DiagnosticResources.Get("PreconditionDocumentationTitle"),
        DiagnosticResources.Get("PreconditionDocumentationMessage"),
        "Documentation",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// Describes a behavior case without unit-test coverage.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnitTestRequired = new(
        BehaviorCaseUnitTestAnalyzer.DIAGNOSTIC_ID,
        DiagnosticResources.Get("TAD202Title"),
        DiagnosticResources.Get("TAD202Message"),
        "Testing",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}