// -----------------------------------------------------------------------
// <copyright file="MaintenanceUsageAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TedToolkit.Annotations.Analyzer;
using TedToolkit.Annotations.Maintenances;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class MaintenanceUsageAnalyzerTests
{
    /// <summary>
    /// 验证分析器公开完整的维护调用诊断目录及预期严重级别。
    /// </summary>
    [Test]
    public async Task Should_expose_complete_maintenance_usage_diagnostic_catalog()
    {
        var diagnostics = new MaintenanceUsageAnalyzer()
            .SupportedDiagnostics
            .Where(diagnostic => diagnostic.Category == "Maintenance")
            .Select(diagnostic => $"{diagnostic.Id}:{diagnostic.DefaultSeverity}");

        await Assert.That(diagnostics).IsEquivalentTo(
        [
            "TTA100:Info",
            "TTA101:Info",
            "TTA102:Info",
            "TTA103:Info",
        ]);
    }

    /// <summary>
    /// 验证调用带维护标注的方法和构造函数会报告对应警告及维护上下文。
    /// </summary>
    [Test]
    public async Task Should_report_maintenance_context_when_annotated_members_are_invoked()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Maintenances;

            sealed class Sample
            {
                [Workaround("Serializer bug", RemoveWhen = "Serializer 4.3 is supported")]
                public Sample() { }

                [TemporaryImplementation("OAuth is not ready")]
                public void Connect() { }

                [TechnicalDebt(TechnicalDebtKind.PERFORMANCE, "Avoid allocation until profiling is complete")]
                public void Process() { }

                [CleanupRequired("Merge duplicate validation", RemoveWhen = "Legacy format is removed")]
                public void Validate() { }
            }

            sealed class Consumer
            {
                void Execute()
                {
                    var sample = new Sample();
                    sample.Connect();
                    sample.Process();
                    sample.Validate();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            "TTA100",
            "TTA101",
            "TTA102",
            "TTA103",
        ]);
        var messages = string.Join("\n", diagnostics.Select(diagnostic => diagnostic.GetMessage()));
        await Assert.That(messages).Contains("Reason: Serializer bug.");
        await Assert.That(messages).Contains("Reason: OAuth is not ready.");
        await Assert.That(messages).Contains("PERFORMANCE technical-debt API");
        await Assert.That(messages).Contains("Remove when: Legacy format is removed");
        await Assert.That(messages).Contains("No removal condition is specified");

        var chineseMessages = string.Join("\n", diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.GetCultureInfo("zh-CN"))));
        await Assert.That(chineseMessages).Contains("调用了性能技术债 API“Sample.Process()”。原因：Avoid allocation until profiling is complete。未指定移除条件");
    }

    /// <summary>
    /// 验证未带维护标注的成员调用不会报告维护警告。
    /// </summary>
    [Test]
    public async Task Should_not_report_maintenance_warning_when_member_is_not_annotated()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Sample
            {
                public void Execute() { }
            }

            sealed class Consumer
            {
                void Execute() => new Sample().Execute();
            }
            """);

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id is "TTA100" or "TTA101" or "TTA102" or "TTA103"))
            .IsEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["ANNOTATIONS_MAINTENANCE"])),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        await Assert.That(compilerDiagnostics).IsEmpty();

        var analyzer = new MaintenanceUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(MaintenanceAttribute).Assembly.Location))
            .ToImmutableArray();
    }
}
