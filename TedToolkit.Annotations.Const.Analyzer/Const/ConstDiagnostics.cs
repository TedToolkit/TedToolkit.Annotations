// -----------------------------------------------------------------------
// <copyright file="ConstDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Const;

/// <summary>
/// Defines diagnostics for const contracts and mutations.
/// </summary>
internal static class ConstDiagnostics
{
    /// <summary>
    /// Reports a write that reaches a protected object-graph depth.
    /// </summary>
    internal static readonly DiagnosticDescriptor MutationNotAllowed = Create(
        ConstMutationAnalyzer.DIAGNOSTIC_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports a const attribute applied to an out parameter.
    /// </summary>
    internal static readonly DiagnosticDescriptor OutParameterNotAllowed = Create(
        ConstMutationAnalyzer.OUT_PARAMETER_DIAGNOSTIC_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an invalid use of <c>Const.Local</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidLocal = Create(
        ConstMutationAnalyzer.INVALID_LOCAL_DIAGNOSTIC_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an incompatible call to a source method.
    /// </summary>
    internal static readonly DiagnosticDescriptor SourceCallRequiresConst = Create(
        ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an unverifiable call to an external method.
    /// </summary>
    internal static readonly DiagnosticDescriptor ExternalCallRequiresConst = Create(
        ConstMutationAnalyzer.EXTERNAL_CALL_DIAGNOSTIC_ID,
        DiagnosticSeverity.Info);

    private static DiagnosticDescriptor Create(string id, DiagnosticSeverity severity)
    {
        return new(
            id,
            DiagnosticResources.Get($"{id}Title"),
            DiagnosticResources.Get($"{id}Message"),
            "Const",
            severity,
            isEnabledByDefault: true);
    }
}