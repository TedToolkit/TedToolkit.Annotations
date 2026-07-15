using System.Globalization;
using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Documentations.Tests;

internal sealed class DiagnosticLocalizationTests
{
    [Test]
    public async Task Should_fall_back_to_english_when_culture_has_no_resource()
    {
        var titles = new BehaviorCaseUnitTestAnalyzer().SupportedDiagnostics.Select(d => d.Title.ToString(CultureInfo.GetCultureInfo("fr-FR")));
        await Assert.That(titles).Contains("Behavior case requires a unit test");
    }

    [Test]
    [Arguments("TAD200", "生成前置条件异常文档")]
    [Arguments("TAD202", "行为用例需要单元测试")]
    public async Task Should_localize_titles(string id, string expected)
    {
        var diagnostics = new PreconditionDocumentationAnalyzer().SupportedDiagnostics.AddRange(new BehaviorCaseUnitTestAnalyzer().SupportedDiagnostics);
        await Assert.That(diagnostics.Single(d => d.Id == id).Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expected);
    }
}
