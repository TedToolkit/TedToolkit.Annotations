using System.Globalization;
using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Const.Tests;

internal sealed class DiagnosticLocalizationTests
{
    [Test]
    [Arguments("TAC300", "违反了 Const 契约")]
    [Arguments("TAC301", "Const 不能标注 out 参数")]
    [Arguments("TAC302", "Const.Local 用法无效")]
    [Arguments("TAC304", "源码方法需要兼容的 Const 契约")]
    [Arguments("TAC305", "外部方法没有兼容的 Const 契约")]
    public async Task Should_localize_titles(string id, string expected)
    {
        var diagnostic = new ConstMutationAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);
        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expected);
    }
}
