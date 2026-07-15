using System.Globalization;
using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Boxing.Tests;

internal sealed class DiagnosticLocalizationTests
{
    [Test]
    public async Task Should_localize_title()
    {
        var diagnostic = new BoxingAnalyzer().SupportedDiagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("TAB201");
        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo("应显式执行装箱转换");
    }
}
