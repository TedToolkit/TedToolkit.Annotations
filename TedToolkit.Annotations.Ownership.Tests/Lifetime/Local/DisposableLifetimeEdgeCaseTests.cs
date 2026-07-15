// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeEdgeCaseTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Local;

internal sealed class DisposableLifetimeEdgeCaseTests
{
    /// <summary>
    /// 验证 using 不能释放从属性借用的资源。
    /// </summary>
    [Test]
    public async Task Should_report_borrowed_disposal_when_borrowed_resource_enters_using()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Holder
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                void Execute(Holder holder)
                {
                    using var resource = holder.Resource;
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证 await using 不能释放从属性借用的异步资源。
    /// </summary>
    [Test]
    public async Task Should_report_borrowed_disposal_when_borrowed_resource_enters_await_using()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateAsyncSource("""
            sealed class Holder
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                async Task Execute(Holder holder)
                {
                    await using var resource = holder.Resource;
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证 out 参数覆盖旧变量时不会把旧值读取为一次非法使用。
    /// </summary>
    [Test]
    public async Task Should_not_report_use_when_out_parameter_overwrites_disposed_local()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Create(out Resource resource) => resource = new Resource();

                void Execute()
                {
                    var resource = new Resource();
                    resource.Dispose();
                    Create(out resource);
                    resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证异步释放结果通过局部别名等待后会被视为已观察。
    /// </summary>
    [Test]
    public async Task Should_observe_async_release_through_result_alias()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateAsyncSource("""
            sealed class Sample
            {
                async Task Execute()
                {
                    var resource = new Resource();
                    var first = resource.DisposeAsync();
                    var second = first;
                    await second;
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证组合等待多个异步释放结果时所有结果都会被视为已观察。
    /// </summary>
    [Test]
    public async Task Should_observe_async_releases_inside_when_all()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateAsyncSource("""
            sealed class Sample
            {
                async Task Execute()
                {
                    var first = new Resource();
                    var second = new Resource();
                    await Task.WhenAll(first.DisposeAsync().AsTask(), second.DisposeAsync().AsTask());
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证同步等待异步释放结果时会被视为已观察。
    /// </summary>
    [Test]
    public async Task Should_observe_async_release_through_get_result()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateAsyncSource("""
            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    resource.DisposeAsync().GetAwaiter().GetResult();
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string members)
    {
        return $$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{members}}
            """;
    }

    private static string CreateAsyncSource(string members)
    {
        return $$"""
            using System;
            using System.Threading.Tasks;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            {{members}}
            """;
    }
}