// -----------------------------------------------------------------------
// <copyright file="DiagnosticLocalizationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Boxing.Tests;

/// <summary>
/// Contains tests for diagnostic localization.
/// </summary>
internal sealed class DiagnosticLocalizationTests
{
    /// <summary>
    /// Verifies that should localize title.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_localize_title()
    {
        var diagnostic = new BoxingAnalyzer().SupportedDiagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("TAB201");
        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo("应显式执行装箱转换");
    }
}