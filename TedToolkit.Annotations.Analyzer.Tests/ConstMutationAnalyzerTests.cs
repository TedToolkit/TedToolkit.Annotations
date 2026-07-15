// -----------------------------------------------------------------------
// <copyright file="ConstMutationAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class ConstMutationAnalyzerTests
{
    /// <summary>
    /// 验证分析器会报告参数和实例成员受保护深度上的写入。
    /// </summary>
    [Test]
    public async Task Should_report_mutations_at_protected_parameter_and_method_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 0", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证未选中的深度允许写入而选中的深度仍会报告。
    /// </summary>
    [Test]
    public async Task Should_allow_mutations_at_unprotected_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证普通局部变量和 ref 局部变量都会继承 Explicit.Const 的深度约束。
    /// </summary>
    [Test]
    public async Task Should_report_mutations_of_const_local_and_ref_local_variables()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Child
            {
                public int Value { get; set; }
            }

            sealed class Sample
            {
                public void Mutate(ref Child source)
                {
                    var local = Explicit.Const(source, ConstDepth.DEPTH1);
                    ref var alias = ref Explicit.Const(ref source, ConstDepth.DEPTH1);

                    local.Value = 1;
                    alias.Value = 1;
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证 Const 特性应用于 out 参数会报告配置错误且不报告写入错误。
    /// </summary>
    [Test]
    public async Task Should_report_error_when_const_is_applied_to_out_parameter()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.OUT_PARAMETER_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证未标注的属性访问器默认约束 getter 的全部深度及 setter 的第一层以上深度。
    /// </summary>
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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 0", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证属性和访问器上的 Const 特性会覆盖属性访问器的默认约束。
    /// </summary>
    [Test]
    public async Task Should_use_property_and_accessor_const_contracts()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

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
    [Test]
    public async Task Should_report_mutations_through_local_aliases()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证引用类型别名继承 Const 约束，而值类型复制和对复制的写入保持允许。
    /// </summary>
    [Test]
    public async Task Should_track_reference_aliases_but_allow_value_type_copies()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证数组元素写入及其成员写入会计入对象图深度。
    /// </summary>
    [Test]
    public async Task Should_report_mutations_of_protected_array_elements()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证合并赋值、解构赋值、事件订阅和可写引用传递不会绕过 Const 约束。
    /// </summary>
    [Test]
    public async Task Should_report_all_supported_mutation_operations_at_protected_depths()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

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
            """);

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Single().GetMessage()).Contains("depth 1");
    }

    /// <summary>
    /// 验证分支和循环汇合会保留所有可能的 Const 引用别名。
    /// </summary>
    [Test]
    public async Task Should_merge_possible_aliases_across_branches_and_loops()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证条件表达式、空合并表达式和 foreach 元素不会丢失 Const 引用来源。
    /// </summary>
    [Test]
    public async Task Should_track_conditional_coalesce_and_foreach_aliases()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("depth 2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// 验证 Explicit.Const 只允许直接初始化局部变量并要求编译期常量深度。
    /// </summary>
    [Test]
    public async Task Should_report_invalid_const_local_context_and_dynamic_depth()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Node
            {
            }

            sealed class Sample
            {
                public void Mutate(Node node, ConstDepth depths)
                {
                    var local = Explicit.Const(node, depths);
                    Consume(Explicit.Const(node));
                }

                private static void Consume(Node node)
                {
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.INVALID_LOCAL_DIAGNOSTIC_ID,
            ConstMutationAnalyzer.INVALID_LOCAL_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证 Const 特性不能应用于没有实例接收者的静态方法和静态属性。
    /// </summary>
    [Test]
    public async Task Should_report_const_contracts_on_static_members()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            static class Sample
            {
                [Const]
                public static void Inspect()
                {
                }

                [Const]
                public static int Value { get; set; }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.STATIC_MEMBER_DIAGNOSTIC_ID,
            ConstMutationAnalyzer.STATIC_MEMBER_DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证重写方法和接口实现会继承方法及参数上的 Const 契约。
    /// </summary>
    [Test]
    public async Task Should_inherit_const_contracts_from_base_and_interface_methods()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证接口属性的 Const 契约会由隐式实现的访问器继承。
    /// </summary>
    [Test]
    public async Task Should_inherit_const_contracts_from_interface_properties()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

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
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            ConstMutationAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
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
            ImmutableArray.Create<DiagnosticAnalyzer>(new ConstMutationAnalyzer()));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
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