// -----------------------------------------------------------------------
// <copyright file="ConstInvocationContractTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

internal sealed class ConstInvocationContractTests
{
    /// <summary>
    /// 验证 Const 接收者和参数调用无契约源码方法时会报告错误。
    /// </summary>
    [Test]
    public async Task Should_report_error_for_incompatible_source_calls()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Node
            {
                public void Mutate() { }
            }

            sealed class Sample
            {
                public void Run([Const] Node node)
                {
                    node.Mutate();
                    Consume(node);
                }

                private static void Consume(Node node) { }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
            ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
    }

    /// <summary>
    /// 验证目标方法和参数具有兼容 Const 深度时允许调用。
    /// </summary>
    [Test]
    public async Task Should_allow_calls_with_compatible_const_contracts()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Node
            {
                [Const]
                public void Inspect() { }
            }

            sealed class Sample
            {
                public void Run([Const] Node node)
                {
                    node.Inspect();
                    Consume(node);
                }

                private static void Consume([Const] Node node) { }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证无法验证 Const 契约的外部方法调用只报告信息。
    /// </summary>
    [Test]
    public async Task Should_report_info_for_unverifiable_external_call()
    {
        var externalReference = ConstAnalyzerTestHelper.CompileReference("""
            namespace ExternalLibrary;

            public sealed class ExternalNode
            {
                public void Mutate() { }
            }
            """);
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync(
            """
            using ExternalLibrary;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                public void Run([Const] ExternalNode node)
                {
                    node.Mutate();
                }
            }
            """,
            externalReference);

        var diagnostic = diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo(ConstMutationAnalyzer.EXTERNAL_CALL_DIAGNOSTIC_ID);
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    /// <summary>
    /// 验证外部方法具有兼容 Const 元数据时不会报告信息。
    /// </summary>
    [Test]
    public async Task Should_allow_external_call_with_compatible_const_metadata()
    {
        var externalReference = ConstAnalyzerTestHelper.CompileReference("""
            using TedToolkit.Annotations.Documentations;

            namespace ExternalLibrary;

            public sealed class ExternalNode
            {
                [Const]
                public void Inspect() { }
            }
            """);
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync(
            """
            using ExternalLibrary;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                public void Run([Const] ExternalNode node)
                {
                    node.Inspect();
                }
            }
            """,
            externalReference);

        await Assert.That(diagnostics).IsEmpty();
    }
}