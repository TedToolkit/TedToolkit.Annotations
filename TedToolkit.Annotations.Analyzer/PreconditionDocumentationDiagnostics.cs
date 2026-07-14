// -----------------------------------------------------------------------
// <copyright file="PreconditionDocumentationDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Defines diagnostic descriptors for precondition documentation.
/// </summary>
internal static class PreconditionDocumentationDiagnostics
{
    /// <summary>
    /// Indicates that exception documentation can be generated.
    /// </summary>
    internal static readonly DiagnosticDescriptor DocumentationCanBeGenerated = new(
        PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID,
        DiagnosticResources.Get("PreconditionDocumentationTitle"),
        DiagnosticResources.Get("PreconditionDocumentationMessage"),
        "Documentation",
        DiagnosticSeverity.Hidden,
        isEnabledByDefault: true);
}