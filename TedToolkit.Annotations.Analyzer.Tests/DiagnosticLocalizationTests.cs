// -----------------------------------------------------------------------
// <copyright file="DiagnosticLocalizationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;

using TedToolkit.Annotations.Analyzer;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class DiagnosticLocalizationTests
{
    /// <summary>
    /// 验证所有诊断标题在缺少特定语言资源时回退到默认英语资源。
    /// </summary>
    [Test]
    public async Task Should_fall_back_to_english_when_culture_has_no_resource()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var diagnostics = GetAllDiagnostics();

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Title.ToString(culture))).Contains("Disposed resource is used");
        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Title.ToString(culture))).Contains("Technical-debt API is invoked");
        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Title.ToString(culture))).Contains("Const contract is violated");
        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Title.ToString(culture))).Contains("Behavior case requires a unit test");
    }

    /// <summary>
    /// 验证所有诊断标题可使用简体中文资源显示。
    /// </summary>
    [Test]
    [Arguments("TTA001", "可释放资源被重复释放")]
    [Arguments("TTA002", "使用了已释放的资源")]
    [Arguments("TTA003", "原所有者使用了已转移的资源")]
    [Arguments("TTA004", "本地拥有的可释放资源未被释放")]
    [Arguments("TTA005", "回调的生命周期可能超过其捕获的资源")]
    [Arguments("TTA006", "可释放资源是借用资源")]
    [Arguments("TTA007", "返回了已释放的资源")]
    [Arguments("TTA011", "拥有的可释放资源被覆盖")]
    [Arguments("TTA012", "拥有的可释放属性被覆盖")]
    [Arguments("TTA100", "调用了变通 API")]
    [Arguments("TTA101", "调用了临时实现")]
    [Arguments("TTA102", "调用了技术债 API")]
    [Arguments("TTA103", "调用了需要清理的 API")]
    [Arguments("TTA200", "生成前置条件异常文档")]
    [Arguments("TTA201", "应显式执行装箱转换")]
    [Arguments("TTA202", "行为用例需要单元测试")]
    [Arguments("TTA300", "违反了 Const 契约")]
    [Arguments("TTA301", "Const 不能标注 out 参数")]
    [Arguments("TTA302", "Explicit.Const 用法无效")]
    [Arguments("TTA304", "源码方法需要兼容的 Const 契约")]
    [Arguments("TTA305", "外部方法没有兼容的 Const 契约")]
    public async Task Should_localize_diagnostic_title_when_chinese_is_requested(string diagnosticId, string expectedTitle)
    {
        var diagnostic = GetAllDiagnostics().Single(candidate => candidate.Id == diagnosticId);

        await Assert.That(diagnostic.Title.ToString(CultureInfo.GetCultureInfo("zh-CN"))).IsEqualTo(expectedTitle);
    }

    private static ImmutableArray<DiagnosticDescriptor> GetAllDiagnostics() =>
        new DisposableLifetimeAnalyzer().SupportedDiagnostics
            .AddRange(new MaintenanceUsageAnalyzer().SupportedDiagnostics)
            .AddRange(new PreconditionDocumentationAnalyzer().SupportedDiagnostics)
            .AddRange(new BoxingAnalyzer().SupportedDiagnostics)
            .AddRange(new BehaviorCaseUnitTestAnalyzer().SupportedDiagnostics)
            .AddRange(new ConstMutationAnalyzer().SupportedDiagnostics);
}
