// -----------------------------------------------------------------------
// <copyright file="OwnedAsyncMemberLifetimeAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Members;

internal sealed class OwnedAsyncMemberLifetimeAnalyzerTests
{
    /// <summary>
    /// 验证拥有异步资源的类型必须实现 IAsyncDisposable。
    /// </summary>
    [Test]
    public async Task Should_require_async_disposable_when_type_owns_async_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA008"]);
    }

    /// <summary>
    /// 验证 DisposeAsync 释放拥有字段时不会报告诊断。
    /// </summary>
    [Test]
    public async Task Should_not_report_when_dispose_async_releases_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IAsyncDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                public async ValueTask DisposeAsync() => await _resource.DisposeAsync();
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 DisposeAsync 未释放拥有字段时会报告诊断。
    /// </summary>
    [Test]
    public async Task Should_report_when_dispose_async_does_not_release_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IAsyncDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA009"]);
    }

    /// <summary>
    /// 验证只有部分 DisposeAsync 路径释放字段时仍会报告诊断。
    /// </summary>
    [Test]
    public async Task Should_report_when_only_one_dispose_async_path_releases_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IAsyncDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                private readonly bool _release;

                public ValueTask DisposeAsync()
                {
                    if (_release)
                    {
                        return _resource.DisposeAsync();
                    }

                    return ValueTask.CompletedTask;
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA009"]);
    }

    /// <summary>
    /// 验证拥有属性与拥有字段采用相同的释放检查。
    /// </summary>
    [Test]
    [Arguments("public ValueTask DisposeAsync() => Resource.DisposeAsync();", "")]
    [Arguments("public ValueTask DisposeAsync() => ValueTask.CompletedTask;", "TTA009")]
    public async Task Should_validate_owned_property_release(string disposeMember, string expectedDiagnosticId)
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource($$"""
            sealed class Owner : IAsyncDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource Resource { get; set; } = new Resource();

                {{disposeMember}}
            }
            """));

        if (string.IsNullOrEmpty(expectedDiagnosticId))
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    private static string CreateSource(string owner)
    {
        return $$"""
            using System;
            using System.Threading.Tasks;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            {{owner}}
            """;
    }
}