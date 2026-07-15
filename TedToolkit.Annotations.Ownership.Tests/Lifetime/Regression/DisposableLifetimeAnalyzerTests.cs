// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer;
using TedToolkit.Annotations.Ownership;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Regression;

internal sealed class DisposableLifetimeAnalyzerTests
{
    /// <summary>
    /// 验证分析器公开的诊断目录包含所有生命周期规则及其预期严重级别。
    /// </summary>
    [Test]
    public async Task Should_expose_complete_disposable_lifetime_diagnostic_catalog()
    {
        var diagnostics = new DisposableLifetimeAnalyzer()
            .SupportedDiagnostics
            .Select(diagnostic => $"{diagnostic.Id}:{diagnostic.DefaultSeverity}");

        await Assert.That(diagnostics).IsEquivalentTo(
        [
            "TAO001:Error",
            "TAO002:Error",
            "TAO003:Error",
            "TAO004:Warning",
            "TAO005:Error",
            "TAO006:Error",
            "TAO007:Error",
            "TAO008:Warning",
            "TAO009:Warning",
            "TAO010:Error",
            "TAO011:Warning",
            "TAO012:Info",
            "TAO013:Warning",
            "TAO014:Error",
        ]);
    }

    /// <summary>
    /// 验证未在项目中显式启用时不执行 Ownership 检查。
    /// </summary>
    [Test]
    public async Task Should_not_run_ownership_analysis_by_default()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute() { var resource = new Resource(); }
            }
            """, enableOwnershipAnalysis: false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 Ownership 不能标记在非 IDisposable 的字段、属性、参数或返回值上。
    /// </summary>
    [Test]
    public async Task Should_report_ownership_when_annotated_type_is_not_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Ownership;

            sealed class Token { }

            sealed class Sample
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Token _field = new Token();

                [Ownership(OwnershipKind.TRANSFERRED)]
                public Token Property => new Token();

                [return: Ownership(OwnershipKind.TRANSFERRED)]
                public Token Create([Ownership(OwnershipKind.TRANSFERRED)] Token token) => token;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO010", "TAO010", "TAO010", "TAO010"]);
    }

    /// <summary>
    /// 验证 Ownership 可以标记在实现 IDisposable 的字段、属性、参数和返回值上。
    /// </summary>
    [Test]
    public async Task Should_not_report_ownership_when_annotated_type_is_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _field = new Resource();

                [Ownership(OwnershipKind.TRANSFERRED)]
                public Resource Property => new Resource();

                [return: Ownership(OwnershipKind.TRANSFERRED)]
                public Resource Create([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => resource;
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 Ownership 可以标记在递归承载 IDisposable 元素的 Dictionary 字段上。
    /// </summary>
    [Test]
    public async Task Should_not_report_ownership_when_dictionary_structurally_carries_disposable_resources()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using System.Collections.Generic;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Dictionary<Guid, ICollection<Resource>> _resources = new();

                public void Dispose()
                {
                    foreach (var collection in _resources.Values)
                    {
                        foreach (var resource in collection)
                        {
                            resource.Dispose();
                        }
                    }

                    _resources.Clear();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证拥有 IDisposable 元素的 Dictionary 仅清空而未释放元素时会报告未释放资源。
    /// </summary>
    [Test]
    public async Task Should_report_owned_dictionary_when_dispose_does_not_release_contained_resources()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using System.Collections.Generic;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Dictionary<Guid, ICollection<Resource>> _resources = new();

                public void Dispose() => _resources.Clear();
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO009"]);
    }

    /// <summary>
    /// 验证 Ownership 不能标记在既不可释放也不承载可释放资源的 Dictionary 字段上。
    /// </summary>
    [Test]
    public async Task Should_report_ownership_when_dictionary_does_not_carry_disposable_resources()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using System.Collections.Generic;
            using TedToolkit.Annotations.Ownership;

            sealed class Token { }

            sealed class Sample
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Dictionary<Guid, ICollection<Token>> _tokens = new();
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO010"]);
    }

    /// <summary>
    /// 验证未转移且未释放的新建 IDisposable 会报告资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_owned_disposable_is_not_disposed()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证同一资源被重复释放时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_resource_is_disposed_twice()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    resource.Dispose();
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证已释放资源再次调用成员时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_use_after_dispose_when_member_is_invoked_after_disposal()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    resource.Dispose();
                    resource.Use();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO002"]);
    }

    /// <summary>
    /// 验证所有权转移后调用方继续释放资源时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_dispose_after_ownership_transfer_when_caller_disposes_resource()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => resource.Dispose();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Receiver().Attach(resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003"]);
    }

    /// <summary>
    /// 验证延迟或订阅回调捕获 using 资源时会报告生命周期错误。
    /// </summary>
    [Test]
    [Arguments("DEFERRED")]
    [Arguments("SUBSCRIPTION")]
    public async Task Should_report_using_resource_captured_by_non_immediate_callback(string lifetime)
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Schedule([CallbackLifetime(CallbackLifetimeKind.__LIFETIME__)] Action callback) { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    using var resource = new Resource();
                    new Receiver().Schedule(() => resource.Use());
                }
            }
            """.Replace("__LIFETIME__", lifetime));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO005"]);
    }

    /// <summary>
    /// 验证传统 using 语句会接管资源并避免泄漏报告。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_resource_is_managed_by_using_statement()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    using (resource)
                    {
                    }
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 using 范围内显式释放资源会报告重复释放。
    /// </summary>
    [Test]
    public async Task Should_report_double_dispose_when_using_resource_is_disposed_explicitly()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    using var resource = new Resource();
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO001"]);
    }

    /// <summary>
    /// 验证所有权转移后继续调用资源成员会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_member_use_after_ownership_transfer()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] IDisposable resource) => resource.Dispose();
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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003"]);
    }

    /// <summary>
    /// 验证通过局部变量别名释放资源会完成原始所有者的生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_disposable_is_disposed_through_local_alias()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    var alias = resource;
                    alias.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证覆盖仍由当前方法拥有的资源会报告覆盖错误。
    /// </summary>
    [Test]
    public async Task Should_report_overwrite_when_owned_disposable_is_overwritten()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    resource = new Resource();
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO011"]);
    }

    /// <summary>
    /// 验证延迟回调捕获普通资源后提前释放资源会报告生命周期错误。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_after_disposable_is_captured_by_deferred_callback()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Schedule([CallbackLifetime(CallbackLifetimeKind.DEFERRED)] Action callback) { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Receiver().Schedule(() => resource.Use());
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO005"]);
    }

    /// <summary>
    /// 验证重复赋值同一资源别名不会被误判为资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_local_alias_is_reassigned_to_same_resource()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    var alias = resource;
                    alias = resource;
                    alias.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 IDisposable 方法返回值未释放时会报告资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_disposable_created_by_method_is_not_disposed()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public Resource Create() => new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Factory().Create();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证 IDisposable 方法返回值被释放后不会报告资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_when_disposable_created_by_method_is_disposed()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public Resource Create() => new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Factory().Create();
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证标记为借用的方法返回值不能由调用方释放。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_when_method_returns_borrowed_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _resource = new Resource();

                [return: Ownership(OwnershipKind.UNCHANGED)]
                public Resource GetResource() => _resource;
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Owner().GetResource();
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证标记为拥有的属性返回值需要由调用方释放。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_property_returns_owned_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                public Resource Create => new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Factory().Create;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证属性的输入和输出所有权流分别作用于 setter 与 getter。
    /// </summary>
    [Test]
    public async Task Should_apply_ownership_flows_to_property_setter_and_getter()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.INPUT)]
                [Ownership(OwnershipKind.UNCHANGED, OwnershipFlow.OUTPUT)]
                public Resource Resource { get; set; } = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    var owner = new Owner();
                    owner.Resource = resource;
                    resource.Dispose();

                    var borrowed = owner.Resource;
                    borrowed.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003", "TAO006"]);
    }

    /// <summary>
    /// 验证从转移参数接收可释放字段的类和结构必须实现 IDisposable。
    /// </summary>
    [Test]
    [Arguments("class")]
    [Arguments("struct")]
    public async Task Should_report_owned_field_when_containing_type_is_not_disposable(string typeKind)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{typeKind}} Session
            {
                private readonly Resource _resource;

                public Session([Ownership(OwnershipKind.TRANSFERRED)] Resource resource)
                    => _resource = resource;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO008"]);
    }

    /// <summary>
    /// 验证显式拥有的字段同样要求其所在类型实现 IDisposable。
    /// </summary>
    [Test]
    public async Task Should_report_explicitly_owned_field_when_containing_type_is_not_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Session
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private readonly Resource _resource;

                public Session(Resource resource) => _resource = resource;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO008"]);
    }

    /// <summary>
    /// 验证拥有可释放字段的类和结构必须在 Dispose 中释放该字段。
    /// </summary>
    [Test]
    [Arguments("class")]
    [Arguments("struct")]
    public async Task Should_report_owned_field_when_dispose_does_not_release_it(string typeKind)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{typeKind}} Session : IDisposable
            {
                private readonly Resource _resource;

                public Session([Ownership(OwnershipKind.TRANSFERRED)] Resource resource)
                    => _resource = resource;

                public void Dispose() { }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO009"]);
    }

    /// <summary>
    /// 验证拥有可释放字段的类和结构在正确释放字段后不会报告诊断。
    /// </summary>
    [Test]
    [Arguments("class")]
    [Arguments("struct")]
    public async Task Should_not_report_owned_field_when_dispose_releases_it(string typeKind)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{typeKind}} Session : IDisposable
            {
                private readonly Resource _resource;

                public Session([Ownership(OwnershipKind.TRANSFERRED)] Resource resource)
                    => _resource = resource;

                public void Dispose() => _resource.Dispose();
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证借用字段不会要求所在类型实现 IDisposable，且不得由其释放。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_when_field_is_borrowed()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Session
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _resource;

                public Session(Resource resource) => _resource = resource;

                public void Dispose() => _resource.Dispose();
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证静态可释放字段不归属于单个实例的释放生命周期。
    /// </summary>
    [Test]
    public async Task Should_not_require_disposable_type_for_static_owned_field()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Session
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private static readonly Resource Resource = new Resource();
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证未标注的 out 参数默认向调用方交付所有权。
    /// </summary>
    [Test]
    public async Task Should_track_out_disposable_as_owned_by_default()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public void Create(out Resource resource) => resource = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    new Factory().Create(out var resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证标记为借用的 out 参数不能由调用方释放。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_when_out_parameter_returns_borrowed_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _resource = new Resource();

                public void Get([Ownership(OwnershipKind.UNCHANGED)] out Resource resource) => resource = _resource;
            }

            sealed class Sample
            {
                void Execute()
                {
                    new Owner().Get(out var resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证 ref 参数可以同时转移旧值的所有权并向调用方交付新值。
    /// </summary>
    [Test]
    public async Task Should_track_ref_parameter_that_consumes_and_returns_owned_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Replacer
            {
                public void Replace(
                    [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.INPUT)]
                    [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.OUTPUT)]
                    ref Resource resource)
                {
                    resource.Dispose();
                    resource = new Resource();
                }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Replacer().Replace(ref resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证属性、方法、out 参数和普通参数均使用各自的默认所有权方向。
    /// </summary>
    [Test]
    [Arguments("""
        sealed class Owner
        {
            public Resource Resource { get; } = new Resource();
        }

        sealed class Sample
        {
            void Execute() => new Owner().Resource.Dispose();
        }
        """, "TAO006")]
    [Arguments("""
        sealed class Factory
        {
            public Resource Create() => new Resource();
        }

        sealed class Sample
        {
            void Execute()
            {
                var resource = new Factory().Create();
            }
        }
        """, "TAO004")]
    [Arguments("""
        sealed class Factory
        {
            public void Create(out Resource resource) => resource = new Resource();
        }

        sealed class Sample
        {
            void Execute()
            {
                new Factory().Create(out var resource);
            }
        }
        """, "TAO004")]
    [Arguments("""
        sealed class Receiver
        {
            public void Inspect(Resource resource) { }
        }

        sealed class Sample
        {
            void Execute()
            {
                var resource = new Resource();
                new Receiver().Inspect(resource);
                resource.Dispose();
            }
        }
        """, "")]
    public async Task Should_infer_default_ownership_for_non_ref_boundaries(string memberDeclarations, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            {{memberDeclarations}}
            """);

        if (string.IsNullOrEmpty(expectedDiagnosticId))
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    /// <summary>
    /// 验证 in 参数可显式将调用方资源的所有权转移给被调方。
    /// </summary>
    [Test]
    public async Task Should_track_ownership_transfer_for_in_parameter()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] in Resource resource) => resource.Dispose();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Receiver().Attach(in resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO003"]);
    }

    /// <summary>
    /// 验证 out 参数覆盖已有所有者时会报告旧资源泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_out_parameter_overwrites_owned_resource()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public void Create(out Resource resource) => resource = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Factory().Create(out resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证 ref 的输出转移不会隐式接管旧资源所有权。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_ref_output_replaces_owned_resource_without_input_transfer()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Replacer
            {
                public void Replace(
                    [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.OUTPUT)]
                    ref Resource resource) => resource = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Replacer().Replace(ref resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    /// <summary>
    /// 验证 ref 可转移输入资源并以借用资源作为输出。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_when_ref_output_is_borrowed()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Replacer
            {
                [Ownership(OwnershipKind.UNCHANGED)]
                private readonly Resource _replacement = new Resource();

                public void Replace(
                    [Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.INPUT)]
                    [Ownership(OwnershipKind.UNCHANGED, OwnershipFlow.OUTPUT)]
                    ref Resource resource)
                {
                    resource.Dispose();
                    resource = _replacement;
                }
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    new Replacer().Replace(ref resource);
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证不能直接释放属性提供的借用资源。
    /// </summary>
    [Test]
    public async Task Should_report_dispose_when_disposable_is_accessed_through_property()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    new Owner().Resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证不能通过局部变量释放属性提供的借用资源。
    /// </summary>
    [Test]
    public async Task Should_report_dispose_when_property_resource_is_assigned_to_local()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Owner().Resource;
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006"]);
    }

    /// <summary>
    /// 验证方法返回前释放将要返回的资源会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_disposed_resource_when_method_returns_it()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public Resource Create()
                {
                    var resource = new Resource();
                    resource.Dispose();
                    return resource;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO007"]);
    }

    /// <summary>
    /// 验证通过别名返回已释放资源同样会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_disposed_resource_when_method_returns_alias()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public Resource Create()
                {
                    var resource = new Resource();
                    var alias = resource;
                    resource.Dispose();
                    return alias;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO007"]);
    }

    /// <summary>
    /// 验证返回仍由当前方法拥有的资源会把所有权交给调用方而不报告泄漏。
    /// </summary>
    [Test]
    public async Task Should_not_report_leak_when_method_returns_owned_disposable()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Factory
            {
                public Resource Create()
                {
                    var resource = new Resource();
                    return resource;
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 using、立即回调和所有权转移会完成资源生命周期而不报告错误。
    /// </summary>
    [Test]
    public async Task Should_not_report_when_resource_lifetime_is_completed_safely()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => resource.Dispose();
                public void Invoke([CallbackLifetime(CallbackLifetimeKind.IMMEDIATE)] Action callback) => callback();
            }

            sealed class Sample
            {
                void Execute()
                {
                    using var callbackResource = new Resource();
                    new Receiver().Invoke(() => callbackResource.Use());

                    var transferredResource = new Resource();
                    new Receiver().Attach(transferredResource);

                    var disposedResource = new Resource();
                    disposedResource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证通过接口转换或显式转换访问局部资源时仍能识别其释放状态。
    /// </summary>
    [Test]
    [Arguments("IDisposable resource = new Resource(); resource.Dispose(); resource.Dispose();", "TAO001")]
    [Arguments("var resource = new Resource(); ((IDisposable)resource).Dispose(); resource.Use();", "TAO002")]
    public async Task Should_track_disposable_through_conversion_operations(string statement, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    {{statement}}
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    /// <summary>
    /// 验证 using 声明、using 语句声明和 using 语句表达式都会接管资源生命周期。
    /// </summary>
    [Test]
    [Arguments("using var resource = new Resource();")]
    [Arguments("using (var resource = new Resource()) { }")]
    [Arguments("using (new Resource()) { }")]
    public async Task Should_not_report_leak_for_each_using_syntax_shape(string statement)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                void Execute()
                {
                    {{statement}}
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证将属性借用资源赋给已拥有局部变量时，会先报告被覆盖实例的泄漏且仍禁止释放借用资源。
    /// </summary>
    [Test]
    public async Task Should_report_leak_and_borrowed_disposal_when_owned_local_is_reassigned_from_property()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner
            {
                public Resource Resource { get; } = new Resource();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    resource = new Owner().Resource;
                    resource.Dispose();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO006", "TAO011"]);
    }

    /// <summary>
    /// 验证覆盖局部变量或拥有的字段前必须先释放原有资源。
    /// </summary>
    [Test]
    public async Task Should_report_overwrite_when_owned_local_or_field_is_reassigned_before_release()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource _field = new Resource();

                public void Dispose() => _field.Dispose();

                void Execute()
                {
                    var local = new Resource();
                    local = new Resource();
                    local.Dispose();
                    _field = new Resource();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO011", "TAO011"]);
    }

    /// <summary>
    /// 验证构造函数接管的字段被覆盖前同样必须先释放原有资源。
    /// </summary>
    [Test]
    public async Task Should_report_overwrite_when_inferred_owned_field_is_reassigned_before_release()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample : IDisposable
            {
                private Resource _field;

                public Sample([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => _field = resource;

                public void Dispose() => _field.Dispose();

                void Execute() => _field = new Resource();
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO011"]);
    }

    /// <summary>
    /// 验证覆盖拥有的可释放属性时会报告信息诊断，释放旧值后覆盖则不报告。
    /// </summary>
    [Test]
    [Arguments("Resource = new Resource();", "TAO012")]
    [Arguments("Resource.Dispose(); Resource = new Resource();", "")]
    public async Task Should_report_info_when_owned_property_is_overwritten_before_release(
        string statements,
        string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource Resource { get; set; } = new Resource();

                public void Dispose() => Resource.Dispose();

                void Execute()
                {
                    {{statements}}
                }
            }
            """);

        if (string.IsNullOrEmpty(expectedDiagnosticId))
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    /// <summary>
    /// 验证已释放或已转移的资源不能再次转移所有权。
    /// </summary>
    [Test]
    [Arguments("resource.Dispose(); receiver.Attach(resource);", "TAO002")]
    [Arguments("receiver.Attach(resource); receiver.Attach(resource);", "TAO003")]
    public async Task Should_report_invalid_transfer_for_non_owned_resource(string statements, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => resource.Dispose();
            }

            sealed class Sample
            {
                void Execute()
                {
                    var resource = new Resource();
                    var receiver = new Receiver();
                    {{statements}}
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    /// <summary>
    /// 验证返回已转移或 using 管理的资源会报告无效生命周期。
    /// </summary>
    [Test]
    [Arguments("var resource = new Resource(); new Receiver().Attach(resource); return resource;", "TAO003")]
    [Arguments("using var resource = new Resource(); return resource;", "TAO007")]
    public async Task Should_report_invalid_return_for_non_returnable_resource(string statements, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Ownership;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([Ownership(OwnershipKind.TRANSFERRED)] Resource resource) => resource.Dispose();
            }

            sealed class Factory
            {
                Resource Create()
                {
                    {{statements}}
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo([expectedDiagnosticId]);
    }

    /// <summary>
    /// 验证构造器、属性访问器和局部函数等独立 operation block 都会分析未释放资源。
    /// </summary>
    [Test]
    [Arguments("Sample() { var resource = new Resource(); }")]
    [Arguments("int Value { get { var resource = new Resource(); return 0; } }")]
    [Arguments("void Execute() { void Local() { var resource = new Resource(); } Local(); }")]
    public async Task Should_analyze_owned_resources_in_each_operation_block(string member)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Sample
            {
                {{member}}
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TAO004"]);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, bool enableOwnershipAnalysis = true)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        await Assert.That(compilerDiagnostics).IsEmpty();

        var analyzer = new DisposableLifetimeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            LifetimeAnalyzerTestHelper.CreateAnalyzerOptions(enableOwnershipAnalysis));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(OwnershipAttribute).Assembly.Location))
            .ToImmutableArray();
    }
}
