// -----------------------------------------------------------------------
// <copyright file="DisposableExpressionAliasTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Local;

internal sealed class DisposableExpressionAliasTests
{
    /// <summary>
    /// 验证条件表达式的两个分支引用同一资源时会保留别名关系。
    /// </summary>
    [Test]
    public async Task Should_track_alias_through_conditional_expression()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            var alias = condition ? resource : resource;
            alias.Dispose();
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证空值合并表达式的两侧引用同一资源时会保留别名关系。
    /// </summary>
    [Test]
    public async Task Should_track_alias_through_coalesce_expression()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            Resource? optional = resource;
            var alias = optional ?? resource;
            alias.Dispose();
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证通过条件表达式返回同一资源时会转移所有权。
    /// </summary>
    [Test]
    public async Task Should_transfer_returned_resource_through_conditional_expression()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync("""
            #nullable enable
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                Resource Create(bool condition)
                {
                    var resource = new Resource();
                    return condition ? resource : resource;
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            #nullable enable
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
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