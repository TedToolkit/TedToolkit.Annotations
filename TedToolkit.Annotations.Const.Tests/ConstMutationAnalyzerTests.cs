// -----------------------------------------------------------------------
// <copyright file="ConstMutationAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer.Tests.Const;
using TedToolkit.Annotations.Const;

namespace TedToolkit.Annotations.Analyzer.Tests;

/// <summary>
/// Contains tests for const mutation analyzer.
/// </summary>
internal sealed class ConstMutationAnalyzerTests
{
    /// <summary>
    /// 验证未在项目中显式启用时不执行 Const 检查。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_not_run_const_analysis_by_default()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Sample
            {
                [Const]
                void Mutate([Const] object value) => value = new object();
            }
            """, enableConstAnalysis: false).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证分析器会报告参数和实例成员受保护深度上的写入。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_mutations_at_protected_parameter_and_method_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Child
            {
                public Child? Next { get; set; }

                public int Value { get; set; }
            }

            sealed class Sample
            {
                public Child Child { get; } = new();

                public int Value { get; set; }

                [Const]
                public void Mutate([Const] Child child)
                {
                    child = new Child();
                    child.Value++;
                    child.Next!.Value = 1;
                    Value++;
                    Child.Value = 1;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 0", StringComparison.Ordinal))).IsTrue();
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证未选中的深度允许写入而选中的深度仍会报告。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_allow_mutations_at_unprotected_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public Child Child { get; } = new();

                public int Value { get; set; }

                [Const(ConstDepth.DEPTH1)]
                public void Mutate([Const(ConstDepth.DEPTH1)] Child child)
                {
                    child = new Child();
                    child.Value = 1;
                    Value = 1;
                    Child.Value = 1;
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
    /// 验证普通局部变量和 ref 局部变量都会继承 AsConst.Local 的深度约束。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_mutations_of_const_local_and_ref_local_variables()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate(ref Child source)
                {
                    var local = AsConst.Local(source, ConstDepth.DEPTH1);
                    ref var alias = ref AsConst.Local(ref source, ConstDepth.DEPTH1);

                    local.Value = 1;
                    alias.Value = 1;
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
    /// 验证 Const 特性应用于 out 参数会报告配置错误且不报告写入错误。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_error_when_const_is_applied_to_out_parameter()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Child
            {
            }

            sealed class Sample
            {
                public void Create([Const] out Child child)
                {
                    child = new Child();
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.OUT_PARAMETER_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证未标注的属性访问器默认约束 getter 的全部深度及 setter 的第一层以上深度。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_apply_default_const_contracts_to_property_accessors()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                private int _value;

                public Child Child { get; } = new();

                public int Value
                {
                    get
                    {
                        _value = 1;
                        Child.Value = 1;
                        return _value;
                    }
                    set
                    {
                        _value = value;
                        Child.Value = value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 0", StringComparison.Ordinal))).IsTrue();
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证属性和访问器上的 Const 特性会覆盖属性访问器的默认约束。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_use_property_and_accessor_const_contracts()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                private int _value;

                public Child Child { get; } = new();

                [Const(ConstDepth.DEPTH1)]
                public int FromProperty
                {
                    get
                    {
                        _value = 1;
                        Child.Value = 1;
                        return _value;
                    }
                    set
                    {
                        _value = value;
                        Child.Value = value;
                    }
                }

                public int FromAccessor
                {
                    [Const(ConstDepth.DEPTH0)]
                    get
                    {
                        _value = 1;
                        Child.Value = 1;
                        return _value;
                    }
                    [Const(ConstDepth.DEPTH1)]
                    set
                    {
                        _value = value;
                        Child.Value = value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证普通局部变量别名会继承受保护对象图的深度。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_mutations_through_local_aliases()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public Node? Next { get; set; }

                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate([Const] Node node)
                {
                    var alias = node;
                    var nestedAlias = node.Next!;

                    alias.Value = 1;
                    nestedAlias.Value = 1;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证引用类型别名继承 Const 约束，而值类型复制和对复制的写入保持允许。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_track_reference_aliases_but_allow_value_type_copies()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            struct ValueNode
            {
                public int Value { get; set; }
            }

            sealed class ReferenceNode
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate([Const] ValueNode valueNode, [Const] ReferenceNode referenceNode)
                {
                    var valueCopy = valueNode;
                    var referenceAlias = referenceNode;
                    ref var valueReference = ref valueNode;

                    valueCopy.Value = 1;
                    referenceAlias.Value = 1;
                    valueReference.Value = 1;
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
    /// 验证数组元素写入及其成员写入会计入对象图深度。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_mutations_of_protected_array_elements()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate([Const] Node[] nodes)
                {
                    nodes[0] = new Node();
                    nodes[0].Value = 1;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证合并赋值、解构赋值、事件订阅和可写引用传递不会绕过 Const 约束。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_all_supported_mutation_operations_at_protected_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public string? Name;

                public Node? Next { get; set; }

                public int Value;

                public event Action? Changed;
            }

            sealed class Sample
            {
                public void Mutate([Const] Node node)
                {
                    node.Name ??= "updated";
                    (node.Value, node.Next!.Value) = (1, 2);
                    node.Changed += static () => { };
                    Increment(ref node.Value);
                    Reset(out node.Value);
                    ref var alias = ref node.Value;
                    alias = 3;
                }

                private static void Increment(ref int value)
                {
                    value++;
                }

                private static void Reset(out int value)
                {
                    value = 0;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证属性的 init 访问器保留 setter 的默认深度一以上约束。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_apply_setter_default_contract_to_init_accessor()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                private int _value;

                public Child Child { get; } = new();

                public int Value
                {
                    init
                    {
                        _value = value;
                        Child.Value = value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Single().GetMessage(CultureInfo.InvariantCulture)).Contains("depth 1");
    }

    /// <summary>
    /// 验证分支和循环汇合会保留所有可能的 Const 引用别名。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_merge_possible_aliases_across_branches_and_loops()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate(bool condition, [Const] Node protectedNode, Node other)
                {
                    Node branchAlias;
                    if (condition)
                    {
                        branchAlias = protectedNode;
                    }
                    else
                    {
                        branchAlias = other;
                    }

                    branchAlias.Value = 1;

                    var loopAlias = other;
                    while (condition)
                    {
                        loopAlias = protectedNode;
                        condition = false;
                    }

                    loopAlias.Value = 2;
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
    /// 验证条件表达式、空合并表达式和 foreach 元素不会丢失 Const 引用来源。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_track_conditional_coalesce_and_foreach_aliases()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate(bool condition, [Const] Node protectedNode, Node other, Node? optional, [Const] Node[] nodes)
                {
                    var conditional = condition ? protectedNode : other;
                    var coalesced = optional ?? protectedNode;
                    var (deconstructed, _) = (protectedNode, other);
                    conditional.Value = 1;
                    coalesced.Value = 2;
                    deconstructed.Value = 3;

                    foreach (var node in nodes)
                    {
                        node.Value = 4;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.Any(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证 AsConst.Local 只允许直接初始化局部变量并要求编译期常量深度。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_invalid_const_local_context_and_dynamic_depth()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
            }

            sealed class Sample
            {
                public void Mutate(Node node, ConstDepth depths)
                {
                    var local = AsConst.Local(node, depths);
                    Consume(AsConst.Local(node));
                }

                private static void Consume(Node node)
                {
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.INVALID_LOCAL_DIAGNOSTIC_ID,
            ConstMutationAnalyzer.INVALID_LOCAL_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证 Const 特性可应用于静态属性的 Current-or-throw 模式。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_allow_const_contract_on_static_current_or_error_property()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            using System;

            sealed class ObjectPartScope
            {
                public static ObjectPartScope? Current { get; set; }

                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                internal static ObjectPartScope CurrentOrError
                {
                    get
                    {
                        return Current ?? throw new InvalidOperationException(
                            "请在计算前先创建一个ObjectPartScope！");
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证静态方法和静态属性会以其声明类型的静态状态作为 Const 深度根节点。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_mutations_of_static_state_at_protected_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public int Value { get; set; }
            }

            static class Sample
            {
                private static int _value;
                private static Node _node = new();

                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                internal static void Update()
                {
                    _value = 1;
                    _node.Value = 1;
                }

                [Const(ConstDepth.DEPTH1_OR_GREATER)]
                internal static int Value
                {
                    get
                    {
                        _value = 2;
                        _node.Value = 2;
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
        await Assert.That(
            diagnostics.All(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证深度零会保护静态类型根节点上的字段、属性和事件。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_direct_static_state_mutations_when_depth_zero_is_protected()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Const;

            static class Sample
            {
                private static int _field;

                private static event EventHandler? Changed;

                private static int Value { get; set; }

                [Const(ConstDepth.DEPTH0)]
                internal static void Update()
                {
                    _field = 1;
                    Value = 1;
                    Changed += OnChanged;
                }

                private static void OnChanged(object? sender, EventArgs arguments)
                {
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(
            diagnostics.All(
                diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    .Contains("depth 0", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证静态属性访问器继承 getter 和 setter 的默认 Const 深度。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_apply_default_const_contracts_to_static_property_accessors()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Node
            {
                public int Value { get; set; }
            }

            static class Sample
            {
                private static int _value;
                private static Node _node = new();

                internal static int Value
                {
                    get
                    {
                        _value = 1;
                        _node.Value = 1;
                        return _value;
                    }
                    set
                    {
                        _value = value;
                        _node.Value = value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Count(
            diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("depth 0", StringComparison.Ordinal)))
            .IsEqualTo(1);
        await Assert.That(diagnostics.Count(
            diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("depth 1", StringComparison.Ordinal)))
            .IsEqualTo(2);
    }

    /// <summary>
    /// 验证静态 Const 契约不会覆盖其他声明类型的静态状态。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_not_report_mutations_of_static_state_owned_by_another_type()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            static class Other
            {
                internal static int Value { get; set; }
            }

            static class Sample
            {
                [Const]
                internal static void Update()
                {
                    Other.Value = 1;
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证重写方法和接口实现会继承方法及参数上的 Const 契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_inherit_const_contracts_from_base_and_interface_methods()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            sealed class Node
            {
                public int Value { get; set; }
            }

            interface IInspector
            {
                void Inspect([Const] Node node);
            }

            abstract class Base
            {
                public int Value { get; set; }

                [Const]
                public abstract void Read();
            }

            sealed class Sample : Base, IInspector
            {
                public void Inspect(Node node)
                {
                    node.Value = 1;
                }

                public override void Read()
                {
                    Value = 1;
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
    /// 验证接口属性的 Const 契约会由隐式实现的访问器继承。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_inherit_const_contracts_from_interface_properties()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Const;

            interface IValue
            {
                [Const(ConstDepth.DEPTH0)]
                int Value { get; }
            }

            sealed class Sample : IValue
            {
                private int _value;

                public int Value
                {
                    get
                    {
                        _value = 1;
                        return _value;
                    }
                }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, bool enableConstAnalysis = true)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        await Assert.That(compilerDiagnostics).IsEmpty();

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new ConstMutationAnalyzer()),
            ConstAnalyzerTestHelper.CreateAnalyzerOptions(enableConstAnalysis));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ConstAttribute).Assembly.Location))
            .ToImmutableArray();
    }
}