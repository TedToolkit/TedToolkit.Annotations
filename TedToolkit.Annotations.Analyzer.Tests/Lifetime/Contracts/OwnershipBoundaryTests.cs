// -----------------------------------------------------------------------
// <copyright file="OwnershipBoundaryTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Contracts;

internal sealed class OwnershipBoundaryTests
{
    /// <summary>
    /// 验证方法必须释放或继续转移其接收所有权的输入参数。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_transferred_input_parameter_is_abandoned()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Consume([Ownership(OwnershipKind.TRANSFERRED)] Resource resource)
                {
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004"]);
    }

    /// <summary>
    /// 验证方法释放其接收所有权的输入参数后不会报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_complete_transferred_input_parameter_when_disposed()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Consume([Ownership(OwnershipKind.TRANSFERRED)] Resource resource)
                {
                    resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证借用返回契约不能返回方法中新建的自有资源。
    /// </summary>
    [Test]
    public async Task Should_report_invalid_contract_when_borrowed_return_exposes_owned_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                [return: Ownership(OwnershipKind.UNCHANGED)]
                Resource Get()
                {
                    var resource = new Resource();
                    return resource;
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    /// <summary>
    /// 验证转移返回契约不能把借用资源声明为调用方所有。
    /// </summary>
    [Test]
    public async Task Should_report_invalid_contract_when_owned_return_exposes_borrowed_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Holder
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                Resource Get(Holder holder)
                {
                    var resource = holder.Resource;
                    return resource;
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    /// <summary>
    /// 验证实现方法与接口继承的所有权契约冲突时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_conflict_between_declared_and_inherited_contracts()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            interface IReceiver
            {
                void Consume([Ownership(OwnershipKind.TRANSFERRED)] Resource resource);
            }

            sealed class Receiver : IReceiver
            {
                public void Consume([Ownership(OwnershipKind.UNCHANGED)] Resource resource)
                {
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    /// <summary>
    /// 验证借用输出契约不能向调用方交付方法中新建的自有资源。
    /// </summary>
    [Test]
    public async Task Should_report_invalid_contract_when_borrowed_output_exposes_owned_resource()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Create([Ownership(OwnershipKind.UNCHANGED)] out Resource resource)
                {
                    resource = new Resource();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    private static string CreateSource(string members)
    {
        return $$"""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{members}}
            """;
    }
}