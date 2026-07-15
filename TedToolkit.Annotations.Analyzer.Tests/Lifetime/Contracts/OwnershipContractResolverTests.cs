// -----------------------------------------------------------------------
// <copyright file="OwnershipContractResolverTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Contracts;

internal sealed class OwnershipContractResolverTests
{
    /// <summary>
    /// 验证实现方法会继承接口参数上的所有权转移契约。
    /// </summary>
    [Test]
    public async Task Should_inherit_parameter_ownership_from_interface()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            interface IReceiver
            {
                void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource);
            }

            sealed class Receiver : IReceiver
            {
                public void Attach(Resource resource) => resource.Dispose();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Receiver().Attach(resource);
                    resource.Use();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA003"]);
    }

    /// <summary>
    /// 验证实现方法会继承接口返回值上的借用契约。
    /// </summary>
    [Test]
    public async Task Should_inherit_return_ownership_from_interface()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            interface IProvider
            {
                [return: Ownership(OwnershipKind.UNCHANGED)]
                Resource Get();
            }

            sealed class Provider : IProvider
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _resource = new Resource();

                public Resource Get() => _resource;
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Provider().Get();
                    resource.Dispose();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA006"]);
    }

    /// <summary>
    /// 验证重写方法会继承基类参数上的所有权转移契约。
    /// </summary>
    [Test]
    public async Task Should_inherit_parameter_ownership_from_overridden_method()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            abstract class ReceiverBase
            {
                public abstract void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource);
            }

            sealed class Receiver : ReceiverBase
            {
                public override void Attach(Resource resource) => resource.Dispose();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Receiver().Attach(resource);
                    resource.Use();
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA003"]);
    }

    /// <summary>
    /// 验证同一流向上的冲突 Ownership 注解会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_conflicting_ownership_annotations()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Execute(
                    [Ownership(OwnershipKind.TRANSFERRED)]
                    [Ownership(OwnershipKind.UNCHANGED)] Resource resource)
                {
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    /// <summary>
    /// 验证普通输入参数不能声明 OUTPUT 所有权流向。
    /// </summary>
    [Test]
    public async Task Should_report_invalid_output_flow_on_non_ref_parameter()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            sealed class Sample
            {
                void Execute(
                    [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.OUTPUT)] Resource resource)
                {
                }
            }
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA014"]);
    }

    /// <summary>
    /// 验证未约束的泛型返回值可以声明所有权注解。
    /// </summary>
    [Test]
    public async Task Should_allow_ownership_annotation_on_unconstrained_generic_return()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            interface IGeometryObject
            {
            }

            delegate TRawValue GetDataHandler<TGeometry, TRawValue>(scoped in TGeometry obj)
                where TGeometry : struct, IGeometryObject;

            static class GeometryData
            {
                [return: Ownership(OwnershipKind.UNCHANGED)]
                public static TRawValue GetData<TRawValue, TGeometry>(
                    scoped in TGeometry obj,
                    GetDataHandler<TGeometry, TRawValue> creator,
                    string implementName)
                    where TGeometry : struct, IGeometryObject
                {
                    return creator(in obj);
                }
            }
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string members)
    {
        return $$"""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }

                public void Use() { }
            }

            {{members}}
            """;
    }
}
