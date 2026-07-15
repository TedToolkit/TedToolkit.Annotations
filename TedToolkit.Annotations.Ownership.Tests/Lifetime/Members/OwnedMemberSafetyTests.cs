// -----------------------------------------------------------------------
// <copyright file="OwnedMemberSafetyTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Members;

internal sealed class OwnedMemberSafetyTests
{
    /// <summary>
    /// 验证释放其他实例的字段不能满足当前实例的释放责任。
    /// </summary>
    [Test]
    public async Task Should_not_count_other_instance_field_as_current_instance_release()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                private readonly Owner _other;

                public Owner(Owner other) => _other = other;

                public void Dispose() => _other._resource.Dispose();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO009"]);
    }

    /// <summary>
    /// 验证调用其他实例的释放辅助方法不能满足当前实例的释放责任。
    /// </summary>
    [Test]
    public async Task Should_not_count_other_instance_helper_as_current_instance_release()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                private readonly Owner _other;

                public Owner(Owner other) => _other = other;

                public void Dispose() => _other.Release();

                private void Release() => _resource.Dispose();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO009"]);
    }

    /// <summary>
    /// 验证自有字段被直接释放两次时会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_owned_field_is_released_twice()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                public void Dispose()
                {
                    _resource.Dispose();
                    _resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证通过局部别名释放自有字段会满足成员释放责任。
    /// </summary>
    [Test]
    public async Task Should_recognize_owned_field_release_through_local_alias()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource = new Resource();

                public void Dispose()
                {
                    var resource = _resource;
                    resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证字段初始化器中新建的资源会被推断为当前类型所有。
    /// </summary>
    [Test]
    public async Task Should_infer_owned_field_from_resource_initializer()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner
            {
                private readonly Resource _resource = new Resource();
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO008"]);
    }

    /// <summary>
    /// 验证构造函数中新建并保存的资源会被推断为当前类型所有。
    /// </summary>
    [Test]
    public async Task Should_infer_owned_field_from_constructor_assignment()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Owner : IDisposable
            {
                private readonly Resource _resource;

                public Owner() => _resource = new Resource();

                public void Dispose() { }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO009"]);
    }

    private static string CreateSource(string members)
    {
        return $$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{members}}
            """;
    }
}