// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionCodeFixProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.Annotations.Assertions.Analyzer;
using TedToolkit.Assertions;

namespace TedToolkit.Annotations.Assertions.Tests;

/// <summary>
/// Contains tests for <see cref="AssertionPreconditionCodeFixProvider"/>.
/// </summary>
internal sealed class AssertionPreconditionCodeFixProviderTests
{
    /// <summary>
    /// 验证单个前置条件会生成对应的 fluent assertion。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_fluent_assertion_when_single_precondition_is_missing()
    {
        var source = await ApplyFixAsync("""
            using TedToolkit.Assertions;

            sealed class Sample
            {
                void Execute([BeGreaterThanPrecondition("10")] int value) { }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("value.Must().BeGreaterThan(10);");
        await Assert.That(source).DoesNotContain("AssertionScope");
    }

    /// <summary>
    /// 验证多个前置条件会在同一个 AssertionScope 中聚合执行。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_assertion_scope_when_multiple_preconditions_are_missing()
    {
        var source = await ApplyFixAsync("""
            using TedToolkit.Assertions;

            sealed class Sample
            {
                void Execute(
                    [BeGreaterThanPrecondition("0")] int first,
                    [BeLessThanPrecondition("100")] int second) { }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("new AssertionScope(\"Validating Execute arguments\").FastPush()");
        await Assert.That(source).Contains("first.Must().BeGreaterThan(0);");
        await Assert.That(source).Contains("second.Must().BeLessThan(100);");
        const string separateLines = "first.Must().BeGreaterThan(0);\r\n"
            + "            second.Must().BeLessThan(100);";
        await Assert.That(source).Contains(separateLines);
    }

    /// <summary>
    /// 验证 nameof 形式的特性参数会还原为方法参数表达式。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_parameter_expression_when_precondition_uses_nameof()
    {
        var source = await ApplyFixAsync("""
            using TedToolkit.Assertions;

            sealed class Sample
            {
                void Execute(int minimum, [BeGreaterThanPrecondition(nameof(minimum))] int value) { }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("value.Must().BeGreaterThan(minimum);");
    }

    /// <summary>
    /// 验证泛型前置条件会生成 fluent assertion。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_fluent_assertion_when_generic_precondition_is_missing()
    {
        var source = await ApplyFixAsync("""
                using TedToolkit.Assertions;

                sealed class Sample
                {
                    void Execute([BeGreaterThanPrecondition<int>(10)] int value) { }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("value.Must().BeGreaterThan(10);");
    }

    /// <summary>
    /// 验证原因和异常类型只用于文档，不会传入 fluent assertion。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_not_pass_explicit_exception_to_fluent_assertion()
    {
        var source = await ApplyFixAsync("""
            using System;
            using TedToolkit.Assertions;

            sealed class Sample
            {
                void Execute([BeGreaterThanPrecondition("10", null, "值必须大于十", typeof(ArgumentOutOfRangeException))] int value) { }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("value.Must().BeGreaterThan(10, null);");
        await Assert.That(source).DoesNotContain("value.Must().BeGreaterThan(10, null, \"值必须大于十\"");
        await Assert.That(source).DoesNotContain("value.Must().BeGreaterThan(10, null, \"值必须大于十\", typeof(ArgumentOutOfRangeException))");
    }

    /// <summary>
    /// 验证异步方法中的多个前置条件会回退到可跨 await 使用的 Push。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_push_when_multiple_preconditions_are_in_async_method()
    {
        var source = await ApplyFixAsync("""
            using System.Threading.Tasks;
            using TedToolkit.Assertions;

            sealed class Sample
            {
                async Task Execute(
                    [BeGreaterThanPrecondition("0")] int first,
                    [BeLessThanPrecondition("100")] int second)
                {
                    await Task.CompletedTask;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(source).Contains("new AssertionScope(\"Validating Execute arguments\").Push()");
        await Assert.That(source).DoesNotContain("FastPush()");
    }

    /// <summary>
    /// 验证已有的 fluent assertion 不会再次报告缺失前置条件。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_not_report_diagnostic_when_fluent_assertion_already_exists()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Assertions;

            sealed class Sample
            {
                void Execute([BeGreaterThanPrecondition("10")] int value)
                {
                    value.Must().BeGreaterThan(10);
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证没有块体的方法不会报告无法生成修复的前置条件诊断。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_not_report_diagnostic_when_method_has_no_body()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Assertions;

            abstract class Sample
            {
                public abstract void Execute([BeGreaterThanPrecondition("10")] int value);
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "CodeFixTests", "CodeFixTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        await AddGeneratedAttributesAsync(workspace, projectId).ConfigureAwait(false);
        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var diagnostics = await AnalyzeAsync(document).ConfigureAwait(false);
        var actions = new List<CodeAction>();
        var provider = new AssertionPreconditionCodeFixProvider();
        var context = new CodeFixContext(
            document,
            diagnostics.Single(),
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        var operations = await actions.Single().GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var updatedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(documentId)!;
        return (await updatedDocument.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "AnalyzerTests", "AnalyzerTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        await AddGeneratedAttributesAsync(workspace, projectId).ConfigureAwait(false);
        return await AnalyzeAsync(workspace.CurrentSolution.GetDocument(documentId)!).ConfigureAwait(false);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false);
        return await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AssertionPreconditionAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static async Task AddGeneratedAttributesAsync(AdhocWorkspace workspace, ProjectId projectId)
    {
        var compilation = await workspace.CurrentSolution.GetProject(projectId)!
            .GetCompilationAsync().ConfigureAwait(false);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());
        driver = driver.RunGenerators(compilation!);
        var solution = workspace.CurrentSolution;
        foreach (var source in driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources))
        {
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), source.HintName, source.SourceText);
        }

        workspace.TryApplyChanges(solution);
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(AssertionPreconditionAttribute).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(TedToolkit.Assertions.AssertionExtensions).Assembly.Location))
            .ToImmutableArray();
    }
}