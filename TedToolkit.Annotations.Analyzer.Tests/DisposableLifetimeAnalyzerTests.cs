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
using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

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
            "TTA001:Error",
            "TTA002:Error",
            "TTA003:Error",
            "TTA004:Warning",
            "TTA005:Error",
            "TTA006:Error",
            "TTA007:Error",
        ]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA001"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA002"]);
    }

    /// <summary>
    /// 验证所有权转移后调用方继续释放资源时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_dispose_after_ownership_transfer_when_caller_disposes_resource()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([TransfersOwnership] Resource resource) { }
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA003"]);
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
            using TedToolkit.Annotations.Documentations;

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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA005"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA001"]);
    }

    /// <summary>
    /// 验证所有权转移后继续调用资源成员会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_member_use_after_ownership_transfer()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Attach([TransfersOwnership] IDisposable resource) { }
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA003"]);
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
    /// 验证覆盖仍由当前方法拥有的资源会报告旧实例泄漏。
    /// </summary>
    [Test]
    public async Task Should_report_leak_when_owned_disposable_is_overwritten()
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004"]);
    }

    /// <summary>
    /// 验证延迟回调捕获普通资源后提前释放资源会报告生命周期错误。
    /// </summary>
    [Test]
    public async Task Should_report_disposal_after_disposable_is_captured_by_deferred_callback()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA005"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA006"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA006"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA007"]);
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA007"]);
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
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
                public void Use() { }
            }

            sealed class Receiver
            {
                public void Attach([TransfersOwnership] Resource resource) { }
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
    [Arguments("IDisposable resource = new Resource(); resource.Dispose(); resource.Dispose();", "TTA001")]
    [Arguments("var resource = new Resource(); ((IDisposable)resource).Dispose(); resource.Use();", "TTA002")]
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004", "TTA006"]);
    }

    /// <summary>
    /// 验证已释放或已转移的资源不能再次转移所有权。
    /// </summary>
    [Test]
    [Arguments("resource.Dispose(); receiver.Attach(resource);", "TTA002")]
    [Arguments("receiver.Attach(resource); receiver.Attach(resource);", "TTA003")]
    public async Task Should_report_invalid_transfer_for_non_owned_resource(string statements, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([TransfersOwnership] Resource resource) { }
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
    [Arguments("var resource = new Resource(); new Receiver().Attach(resource); return resource;", "TTA003")]
    [Arguments("using var resource = new Resource(); return resource;", "TTA007")]
    public async Task Should_report_invalid_return_for_non_returnable_resource(string statements, string expectedDiagnosticId)
    {
        var diagnostics = await AnalyzeAsync($$"""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Receiver
            {
                public void Attach([TransfersOwnership] Resource resource) { }
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

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA004"]);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
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
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(DocumentationAttribute).Assembly.Location))
            .ToImmutableArray();
    }
}
