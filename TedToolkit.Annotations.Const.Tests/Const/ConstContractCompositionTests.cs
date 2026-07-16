// -----------------------------------------------------------------------
// <copyright file="ConstContractCompositionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

/// <summary>
/// Contains tests for const contract composition.
/// </summary>
internal sealed class ConstContractCompositionTests
{
    /// <summary>
    /// 验证多个接口声明的 Const 深度会合并为并集。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_union_parameter_contracts_from_multiple_interfaces()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public Node? Next;
                public int Value;
            }

            interface IShallow { void Mutate([Const(ConstDepth.DEPTH1)] Node node); }
            interface IDeep { void Mutate([Const(ConstDepth.DEPTH2)] Node node); }

            sealed class Sample : IShallow, IDeep
            {
                public void Mutate(Node node)
                {
                    node.Value = 1;
                    node.Next!.Value = 2;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证实现方法上的直接 Const 契约不能削弱继承契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_not_weaken_inherited_contract_with_direct_attribute()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            interface IContract { void Mutate([Const] Node node); }

            sealed class Sample : IContract
            {
                public void Mutate([Const(ConstDepth.NONE)] Node node)
                {
                    node.Value = 1;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID,]);
    }

    /// <summary>
    /// 验证多个接口属性的 Const 深度会由实现访问器合并。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_union_property_contracts_from_multiple_interfaces()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            interface IShallow
            {
                [Const(ConstDepth.DEPTH0)]
                int Value { get; }
            }

            interface IDeep
            {
                [Const(ConstDepth.DEPTH1)]
                int Value { get; }
            }

            sealed class Child { public int Value; }
            sealed class Sample : IShallow, IDeep
            {
                private int _value;
                private readonly Child _child = new();

                public int Value
                {
                    get
                    {
                        _value = 1;
                        _child.Value = 2;
                        return _value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }
}