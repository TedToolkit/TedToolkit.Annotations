// -----------------------------------------------------------------------
// <copyright file="DiagnosticLocalizationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Documentations.Tests;

/// <summary>
/// Contains tests for diagnostic localization.
/// </summary>
internal sealed class DiagnosticLocalizationTests
{
    /// <summary>
    /// Verifies that should fall back to english when culture has no resource.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_fall_back_to_english_when_culture_has_no_resource()
    {
        var titles = new BehaviorCaseUnitTestAnalyzer().SupportedDiagnostics
            .Select(d => d.Title.ToString(CultureInfo.GetCultureInfo("fr-FR")));
        await Assert.That(titles).Contains("Behavior case requires a unit test");
    }

    /// <summary>
    /// Verifies that should localize titles.
    /// </summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="expected">The expected localized title.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("TAD200", "生成前置条件异常文档")]
    [Arguments("TAD202", "行为用例需要单元测试")]
    public async Task Should_localize_titles(string id, string expected)
    {
        var diagnostics = new PreconditionDocumentationAnalyzer().SupportedDiagnostics
            .AddRange(new BehaviorCaseUnitTestAnalyzer().SupportedDiagnostics);
        await Assert.That(diagnostics.Single(d => d.Id == id).Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expected);
    }
}