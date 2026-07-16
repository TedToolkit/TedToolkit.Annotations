// -----------------------------------------------------------------------
// <copyright file="ConstAnalysisOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Provides shared functionality for const analysis options.
/// </summary>
internal static class ConstAnalysisOptions
{
    /// <summary>
    /// Defines the enable const analysis property name value.
    /// </summary>
    internal const string ENABLE_CONST_ANALYSIS_PROPERTY_NAME = "build_property.TedToolkitEnableConstAnalysis";

    /// <summary>
    /// Determines whether const analysis is enabled for the current compilation.
    /// </summary>
    /// <param name="optionsProvider">The analyzer configuration options.</param>
    /// <returns><see langword="true"/> when const analysis is enabled; otherwise, <see langword="false"/>.</returns>
    internal static bool IsEnabled(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue(ENABLE_CONST_ANALYSIS_PROPERTY_NAME, out var value)
               && bool.TryParse(value, out var enabled)
               && enabled;
    }
}