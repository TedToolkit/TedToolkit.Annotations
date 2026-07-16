// -----------------------------------------------------------------------
// <copyright file="DiagnosticLocalizationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Maintenance.Tests;

/// <summary>
/// Contains tests for diagnostic localization.
/// </summary>
internal sealed class DiagnosticLocalizationTests
{
    /// <summary>
    /// Verifies that should localize titles.
    /// </summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="expected">The expected localized title.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("TAM100", "调用了变通 API")]
    [Arguments("TAM101", "调用了临时实现")]
    [Arguments("TAM102", "调用了技术债 API")]
    [Arguments("TAM103", "调用了需要清理的 API")]
    public async Task Should_localize_titles(string id, string expected)
    {
        var diagnostic = new MaintenanceUsageAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);
        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expected);
    }
}