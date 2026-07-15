// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeControlFlowTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TedToolkit.Annotations.Analyzer.Lifetime.Analysis.Local;
using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Local;

internal sealed class DisposableLifetimeControlFlowTests
{
    /// <summary>
    /// 验证复合增量循环的最小执行次数能够被正确计算。
    /// </summary>
    [Test]
    public async Task Should_calculate_minimum_iterations_for_compound_increment_loop()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("for (var index = 0; index < 2; index += 1) { resource.Dispose(); }");
        var invocation = syntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        await Assert.That(LocalDisposableLifetimeWalker.GetMinimumLoopIterations(invocation)).IsEqualTo(2);
    }

    /// <summary>
    /// 验证所有分支都释放资源时不会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_all_branches_release_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                resource.Dispose();
            }
            else
            {
                resource.Dispose();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证只有部分分支释放资源时会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_only_one_branch_releases_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                resource.Dispose();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证条件释放后的汇合点不会报告确定的释放后使用。
    /// </summary>
    [Test]
    public async Task Should_not_report_definite_use_after_dispose_when_release_is_conditional()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                resource.Dispose();
            }

            resource.Use();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证所有到达路径都已释放资源时会报告释放后使用。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_all_reaching_branches_dispose()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                resource.Dispose();
            }
            else
            {
                resource.Dispose();
            }

            resource.Use();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    /// <summary>
    /// 验证 finally 释放资源时所有正常退出路径都完成生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_finally_releases_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            try
            {
                if (condition)
                {
                    return;
                }
            }
            finally
            {
                resource.Dispose();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证释放前提前返回的路径会报告资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_early_return_keeps_resource_owned()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                return;
            }

            resource.Dispose();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证只在可能不执行的循环中释放资源仍会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_release_only_occurs_inside_optional_loop()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            while (condition)
            {
                resource.Dispose();
                break;
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证 switch 的所有分支都释放资源时不会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_all_switch_arms_release_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            switch (condition)
            {
                case true:
                    resource.Dispose();
                    break;
                default:
                    resource.Dispose();
                    break;
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 catch 路径与正常路径都释放资源时不会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_try_and_catch_paths_release_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            try
            {
                if (condition)
                {
                    throw new InvalidOperationException();
                }

                resource.Dispose();
            }
            catch (InvalidOperationException)
            {
                resource.Dispose();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证循环可能只执行一次时不会报告确定的重复释放。
    /// </summary>
    [Test]
    public async Task Should_not_report_definite_double_dispose_when_loop_may_execute_once()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            do
            {
                resource.Dispose();
            }
            while (condition);
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证调用本地释放函数会完成捕获资源的生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_local_function_releases_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            void Release() => resource.Dispose();

            Release();
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证固定执行两次的循环会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_loop_has_two_iterations()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            for (var index = 0; index < 2; index++)
            {
                resource.Dispose();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证使用复合增量的确定双次循环同样会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_guaranteed_loop_uses_compound_increment()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            void Execute()
            {
                var resource = new Resource();
                for (var index = 0; index < 2; index += 1)
                {
                    resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }

                public void Use() { }
            }

            sealed class Sample
            {
                void Execute(bool condition)
                {
                    var resource = new Resource();
                    {{statements}}
                }
            }
            """;
    }
}