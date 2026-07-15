// -----------------------------------------------------------------------
// <copyright file="OwnedMemberDisposalPatternTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Members;

internal sealed class OwnedMemberDisposalPatternTests
{
    /// <summary>
    /// 验证 Dispose 委托给 DisposeCore 时会识别辅助方法释放的字段。
    /// </summary>
    [Test]
    public async Task Should_recognize_owned_field_release_in_dispose_helper()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource? _resource = new Resource();

                public void Dispose() => DisposeCore();

                private void DisposeCore() => _resource?.Dispose();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 DisposeAsync 委托给 DisposeAsyncCore 时会识别辅助方法释放的字段。
    /// </summary>
    [Test]
    public async Task Should_recognize_owned_field_release_in_dispose_async_helper()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class AsyncResource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            sealed class Owner : IAsyncDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly AsyncResource _resource = new AsyncResource();

                public ValueTask DisposeAsync() => DisposeAsyncCore();

                private ValueTask DisposeAsyncCore() => _resource.DisposeAsync();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证空值条件释放拥有属性时不会报告未释放。
    /// </summary>
    [Test]
    public async Task Should_recognize_null_conditional_release_of_owned_property()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource? Resource { get; set; } = new Resource();

                public void Dispose() => Resource?.Dispose();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string owner)
    {
        return $$"""
            #nullable enable
            using System;
            using System.Threading.Tasks;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{owner}}
            """;
    }
}