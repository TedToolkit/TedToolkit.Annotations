// -----------------------------------------------------------------------
// <copyright file="DiagnosticResources.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Resources;

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Provides culture-aware strings for analyzer diagnostics.
/// </summary>
internal static class DiagnosticResources
{
    private static readonly ResourceManager _resourceManager = new(
        "TedToolkit.Annotations.Analyzer.Resources.DiagnosticResources",
        typeof(DiagnosticResources).Assembly);

    /// <summary>
    /// Gets a localized diagnostic resource string.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="formatArguments">Arguments for resource formatting.</param>
    /// <returns>A localized resource string.</returns>
    internal static LocalizableResourceString Get(string name, params string[] formatArguments)
    {
        return new(name, _resourceManager, typeof(DiagnosticResources), formatArguments);
    }
}