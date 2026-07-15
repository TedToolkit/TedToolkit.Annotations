using System.Globalization;
using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Ownership.Tests;

internal sealed class DiagnosticLocalizationTests
{
    [Test]
    [Arguments("TAO001", "可释放资源被重复释放")]
    [Arguments("TAO002", "使用了已释放的资源")]
    [Arguments("TAO003", "原所有者使用了已转移的资源")]
    [Arguments("TAO004", "本地拥有的可释放资源未被释放")]
    [Arguments("TAO005", "回调的生命周期可能超过其捕获的资源")]
    [Arguments("TAO006", "可释放资源是借用资源")]
    [Arguments("TAO007", "返回了已释放的资源")]
    [Arguments("TAO011", "拥有的可释放资源被覆盖")]
    [Arguments("TAO012", "拥有的可释放属性被覆盖")]
    public async Task Should_localize_titles(string id, string expected)
    {
        var diagnostic = new DisposableLifetimeAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);
        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expected);
    }
}
