// -----------------------------------------------------------------------
// <copyright file="ConstFlowRegressionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

internal sealed class ConstFlowRegressionTests
{
    /// <summary>
    /// 验证 Const.Local 不会清除来源参数已有的 Const 契约。
    /// </summary>
    [Test]
    public async Task Should_preserve_source_contract_for_explicit_const_local()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate([Const] Node source)
                {
                    var local = Const.Local(source, ConstDepth.NONE);
                    local.Value = 1;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证 Const.Local 局部变量重新赋值后仍保留自身契约。
    /// </summary>
    [Test]
    public async Task Should_preserve_local_contract_after_reassignment()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate(Node first, Node second)
                {
                    var local = Const.Local(first, ConstDepth.DEPTH1);
                    local = second;
                    local.Value = 1;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证 ref 局部变量重新绑定后只关联新的引用目标。
    /// </summary>
    [Test]
    public async Task Should_replace_ref_alias_after_ref_reassignment()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate([Const] Node protectedNode, Node other)
                {
                    ref Node alias = ref protectedNode;
                    alias = ref other;
                    alias.Value = 1;
                }
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证 ref 局部变量重新绑定到受保护目标后会继承该目标的契约。
    /// </summary>
    [Test]
    public async Task Should_track_protected_target_after_ref_reassignment()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate([Const] Node protectedNode, Node other)
                {
                    ref Node alias = ref other;
                    alias = ref protectedNode;
                    alias.Value = 1;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证异常处理分支会接收 try 区域中建立的潜在 Const 别名。
    /// </summary>
    [Test]
    public async Task Should_propagate_const_alias_to_catch_block()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate([Const] Node source, Node other)
                {
                    var alias = other;
                    try
                    {
                        alias = source;
                        throw new InvalidOperationException();
                    }
                    catch
                    {
                        alias.Value = 1;
                    }
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证 finally 分支会接收 try 区域中建立的潜在 Const 别名。
    /// </summary>
    [Test]
    public async Task Should_propagate_const_alias_to_finally_block()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            sealed class Sample
            {
                public void Mutate([Const] Node source, Node other)
                {
                    var alias = other;
                    try
                    {
                        alias = source;
                    }
                    finally
                    {
                        alias.Value = 1;
                    }
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证结构体 Const 方法重新赋值 this 时会违反深度零契约。
    /// </summary>
    [Test]
    public async Task Should_report_struct_this_reassignment()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            struct Sample
            {
                public int Value;

                [Const(ConstDepth.DEPTH0)]
                public void Reset()
                {
                    this = default;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
    }

    /// <summary>
    /// 验证值类型复制允许修改复制字段，但仍跟踪其中共享的引用对象。
    /// </summary>
    [Test]
    public async Task Should_track_reference_members_through_value_type_copy()
    {
        var diagnostics = await ConstAnalyzerTestHelper.AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node { public int Value; }
            struct Wrapper
            {
                public int Value;
                public Node Node;
            }

            sealed class Sample
            {
                public void Mutate([Const] Wrapper wrapper)
                {
                    var copy = wrapper;
                    copy.Value = 1;
                    copy.Node.Value = 2;
                    copy.Node = new Node();
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([ConstMutationAnalyzer.DIAGNOSTIC_ID]);
        await Assert.That(diagnostics.Single().GetMessage()).Contains("depth 2");
    }
}