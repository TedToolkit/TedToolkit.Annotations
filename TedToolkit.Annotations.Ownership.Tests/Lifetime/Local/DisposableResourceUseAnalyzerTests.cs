// -----------------------------------------------------------------------
// <copyright file="DisposableResourceUseAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Local;

internal sealed class DisposableResourceUseAnalyzerTests
{
    /// <summary>
    /// 验证已释放资源作为借用参数传递时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_passed_to_borrowing_parameter()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            resource.Dispose();
            Inspect(resource);
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    /// <summary>
    /// 验证已释放资源的属性被读取时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_property_is_read()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            resource.Dispose();
            _ = resource.Value;
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    /// <summary>
    /// 验证写入拥有字段会转移局部资源的所有权。
    /// </summary>
    [Test]
    public async Task Should_transfer_ownership_when_assigned_to_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            _owned = resource;
            resource.Use();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003", "TAO011"]);
    }

    /// <summary>
    /// 验证所有权转移后通过别名读取资源会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_transfer_when_resource_is_accessed_through_alias()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            var alias = resource;
            _owned = resource;
            _ = alias.Value;
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003", "TAO011"]);
    }

    /// <summary>
    /// 验证释放后通过字符串转换使用资源会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_resource_is_converted_to_string()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            resource.Dispose();
            _ = resource.ToString();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public int Value => 0;

                public void Dispose() { }

                public void Use() { }
            }

            sealed class Sample : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource _owned = new Resource();

                public void Dispose() => _owned.Dispose();

                private static void Inspect(Resource resource) { }

                void Execute()
                {
                    var resource = new Resource();
                    {{statements}}
                }
            }
            """;
    }
}