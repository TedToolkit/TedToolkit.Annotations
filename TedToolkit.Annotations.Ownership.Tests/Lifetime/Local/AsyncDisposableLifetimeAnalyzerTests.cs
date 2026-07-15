// -----------------------------------------------------------------------
// <copyright file="AsyncDisposableLifetimeAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Local;

internal sealed class AsyncDisposableLifetimeAnalyzerTests
{
    /// <summary>
    /// 验证异步资源未释放时会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_async_disposable_is_not_released()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource(""));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证等待 DisposeAsync 后不会报告资源泄漏。
    /// </summary>
    [Test]
    [Arguments("await resource.DisposeAsync();")]
    [Arguments("await resource.DisposeAsync().ConfigureAwait(false);")]
    public async Task Should_complete_lifetime_when_async_release_is_awaited(string release)
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource(release));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 await using 会管理异步资源的生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_resource_uses_await_using()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync("""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            sealed class Sample
            {
                async Task ExecuteAsync()
                {
                    await using var resource = new Resource();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证未观察 DisposeAsync 返回值时会报告诊断。
    /// </summary>
    [Test]
    public async Task Should_report_unobserved_release_when_dispose_async_is_not_awaited()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("resource.DisposeAsync();"));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO013"]);
    }

    /// <summary>
    /// 验证等待异步释放后再次使用资源会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_dispose_async_was_awaited()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            await resource.DisposeAsync();
            resource.Use();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    /// <summary>
    /// 验证直接返回 DisposeAsync 的 ValueTask 会转移异步释放责任。
    /// </summary>
    [Test]
    public async Task Should_accept_returned_dispose_async_value_task()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync("""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            sealed class Sample
            {
                ValueTask ReleaseAsync()
                {
                    var resource = new Resource();
                    return resource.DisposeAsync();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证异步资源被等待释放两次时会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_dispose_async_is_awaited_twice()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            await resource.DisposeAsync();
            await resource.DisposeAsync();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证 await using 语句会管理异步资源的生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_resource_uses_await_using_statement()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync("""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;

                public void Use() { }
            }

            sealed class Sample
            {
                async Task ExecuteAsync()
                {
                    await using (var resource = new Resource())
                    {
                        resource.Use();
                    }
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 await using 管理的资源被显式异步释放时会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_await_using_resource_is_released_explicitly()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync("""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            sealed class Sample
            {
                async Task ExecuteAsync()
                {
                    await using var resource = new Resource();
                    await resource.DisposeAsync();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证仅配置 DisposeAsync 而未等待其结果时会报告未观察诊断。
    /// </summary>
    [Test]
    public async Task Should_report_unobserved_release_when_configure_await_result_is_not_awaited()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(
            CreateSource("resource.DisposeAsync().ConfigureAwait(false);"));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO013"]);
    }

    /// <summary>
    /// 验证保存 DisposeAsync 结果并随后等待时不会报告未观察诊断。
    /// </summary>
    [Test]
    public async Task Should_observe_async_release_when_stored_result_is_awaited()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            var disposal = resource.DisposeAsync();
            await disposal;
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证通过 AsTask 等待 DisposeAsync 结果时不会报告未观察诊断。
    /// </summary>
    [Test]
    public async Task Should_observe_async_release_when_as_task_is_awaited()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(
            CreateSource("await resource.DisposeAsync().AsTask();"));

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;

                public void Use() { }
            }

            sealed class Sample
            {
                async Task ExecuteAsync()
                {
                    var resource = new Resource();
                    {{statements}}
                }
            }
            """;
    }
}