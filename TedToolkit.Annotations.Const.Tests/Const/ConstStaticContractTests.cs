// -----------------------------------------------------------------------
// <copyright file="ConstStaticContractTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

/// <summary>
/// Contains tests for const static contract.
/// </summary>
internal sealed class ConstStaticContractTests
{
    /// <summary>
    /// 验证静态 Const 方法调用同类型静态方法时会检查其静态状态契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_require_compatible_contract_for_static_method_calls()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            static class Sample
            {
                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                internal static void Inspect()
                {
                    Mutate();
                    Read();
                }

                private static void Mutate()
                {
                }

                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                private static void Read()
                {
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,]);
    }

    /// <summary>
    /// 验证静态 Const 方法读取静态属性时会检查 getter 契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_require_compatible_contract_for_static_property_getters()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            static class Sample
            {
                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                internal static int Inspect() => Value;

                [Const(ConstDepth.NONE)]
                private static int Value => 0;
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,]);
    }

    /// <summary>
    /// 验证实例属性的 getter 和允许直接写入的 setter 仍需兼容调用方契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_require_compatible_contract_for_instance_property_accessors()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Sample
            {
                [Const(ConstDepth.DEPTH1)]
                internal int Inspect()
                {
                    Value = 1;
                    return Other;
                }

                [Const(ConstDepth.NONE)]
                private int Value { set { } }

                [Const(ConstDepth.NONE)]
                private int Other => 0;
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
            ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证取得受保护静态状态的地址会报告潜在的非托管写入。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_address_of_protected_static_state()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            static class Sample
            {
                private static int _value;

                [Const(ConstDepth.DEPTH0)]
                internal static unsafe void Inspect()
                {
                    fixed (int* pointer = &_value)
                    {
                        *pointer = 1;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID,]);
    }

    /// <summary>
    /// 验证 Unsafe.AsRef 和 extern in 参数会报告受保护静态状态的直接逃逸。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_readonly_reference_escape_to_unsafe_boundaries()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using System.Runtime.CompilerServices;
            using TedToolkit.Annotations.Const;

            static class Sample
            {
                private static int _value;

                [Const(ConstDepth.DEPTH0)]
                internal static void Inspect()
                {
                    ref var alias = ref Unsafe.AsRef(ref _value);
                    Mutate(in _value);
                    alias = 1;
                }

                private static extern void Mutate(in int value);
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.SOURCE_CALL_DIAGNOSTIC_ID,
        ]);
    }
}