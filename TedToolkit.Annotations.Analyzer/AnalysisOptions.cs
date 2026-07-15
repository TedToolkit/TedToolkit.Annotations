// -----------------------------------------------------------------------
// <copyright file="AnalysisOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reads project-level configuration for optional analyses.
/// </summary>
internal static class AnalysisOptions
{
    /// <summary>
    /// The MSBuild property that enables ownership analysis.
    /// </summary>
    internal const string ENABLE_OWNERSHIP_ANALYSIS_PROPERTY_NAME = "build_property.TedToolkitEnableOwnershipAnalysis";

    /// <summary>
    /// The MSBuild property that enables const analysis.
    /// </summary>
    internal const string ENABLE_CONST_ANALYSIS_PROPERTY_NAME = "build_property.TedToolkitEnableConstAnalysis";

    /// <summary>
    /// Gets whether disposable lifetime and ownership analysis is enabled for the current project.
    /// </summary>
    /// <param name="optionsProvider">The analyzer options supplied by the consuming project.</param>
    /// <returns><see langword="true"/> when the project explicitly enables the analysis; otherwise, <see langword="false"/>.</returns>
    internal static bool IsOwnershipAnalysisEnabled(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return IsEnabled(optionsProvider, ENABLE_OWNERSHIP_ANALYSIS_PROPERTY_NAME);
    }

    /// <summary>
    /// Gets whether const analysis is enabled for the current project.
    /// </summary>
    /// <param name="optionsProvider">The analyzer options supplied by the consuming project.</param>
    /// <returns><see langword="true"/> when the project explicitly enables the analysis; otherwise, <see langword="false"/>.</returns>
    internal static bool IsConstAnalysisEnabled(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return IsEnabled(optionsProvider, ENABLE_CONST_ANALYSIS_PROPERTY_NAME);
    }

    private static bool IsEnabled(AnalyzerConfigOptionsProvider optionsProvider, string propertyName)
    {
        return optionsProvider.GlobalOptions.TryGetValue(propertyName, out var value)
            && bool.TryParse(value, out var isEnabled)
            && isEnabled;
    }
}