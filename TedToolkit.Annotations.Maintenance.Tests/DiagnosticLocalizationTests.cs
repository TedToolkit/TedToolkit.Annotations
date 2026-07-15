using System.Globalization;
using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Maintenance.Tests;

internal sealed class DiagnosticLocalizationTests
{
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
