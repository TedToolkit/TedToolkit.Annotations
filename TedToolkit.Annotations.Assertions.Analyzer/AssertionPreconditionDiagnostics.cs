// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Assertions.Analyzer;

/// <summary>
/// Defines diagnostics emitted by the assertion-precondition analyzer.
/// </summary>
internal static class AssertionPreconditionDiagnostics
{
    /// <summary>
    /// Describes an assertion precondition that is not enforced by its member body.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingAssertion = new(
        "TAA200",
        "Generate assertion precondition",
        "The declared assertion preconditions are not enforced by the method body",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}