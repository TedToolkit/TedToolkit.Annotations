// -----------------------------------------------------------------------
// <copyright file="BoxingCodeFixProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.Annotations.Boxing;

namespace TedToolkit.Annotations.Analyzer.Tests.Boxing;

internal sealed class BoxingCodeFixProviderTests
{
    /// <summary>
    /// 验证装箱修复不支持批量操作。
    /// </summary>
    [Test]
    public async Task Should_not_support_fix_all()
    {
        await Assert.That(new BoxingCodeFixProvider().GetFixAllProvider()).IsNull();
    }

    /// <summary>
    /// 验证 object 装箱会修复为不指定目标类型的 Boxing.Box 调用。
    /// </summary>
    [Test]
    public async Task Should_wrap_object_boxing_with_explicit_box()
    {
        var updatedSource = await ApplyFixAsync("""
            sealed class Sample
            {
                object Box(int value) => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box(value)");
    }

    /// <summary>
    /// 验证接口装箱会修复为指定接口目标类型的 Boxing.Box 调用。
    /// </summary>
    [Test]
    public async Task Should_wrap_interface_boxing_with_typed_explicit_box()
    {
        var updatedSource = await ApplyFixAsync("""
            using System;

            sealed class Sample
            {
                IComparable Box(int value) => (IComparable)value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box<global::System.IComparable, int>(value)");
        await Assert.That(updatedSource).DoesNotContain("(IComparable)value");
    }

    /// <summary>
    /// 验证接口的隐式装箱也会修复为指定目标类型的 Boxing.Box 调用。
    /// </summary>
    [Test]
    public async Task Should_wrap_implicit_interface_boxing_with_typed_explicit_box()
    {
        var updatedSource = await ApplyFixAsync("""
            using System;

            sealed class Sample
            {
                IComparable Box(int value) => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box<global::System.IComparable, int>(value)");
    }

    /// <summary>
    /// 验证特殊引用基类目标会生成对应的泛型 Boxing.Box 调用。
    /// </summary>
    [Test]
    [Arguments("ValueType", "global::System.ValueType")]
    [Arguments("Enum", "global::System.Enum")]
    public async Task Should_preserve_special_reference_target_when_fixing_boxing(
        string targetType,
        string expectedTargetType)
    {
        var updatedSource = await ApplyFixAsync($$"""
            using System;

            enum Mode
            {
                Default,
            }

            sealed class Sample
            {
                {{targetType}} Box(Mode value) => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            $"global::TedToolkit.Annotations.Boxing.Boxing.Box<{expectedTargetType}, global::Mode>(value)");
    }

    /// <summary>
    /// 验证受值类型约束的泛型参数会使用可推断的 object 重载。
    /// </summary>
    [Test]
    public async Task Should_use_inferred_object_overload_for_struct_constrained_generic_boxing()
    {
        var updatedSource = await ApplyFixAsync("""
            sealed class Sample
            {
                object Box<T>(T value)
                    where T : struct
                    => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box(value)");
    }

    /// <summary>
    /// 验证 dynamic 目标会保留为 Boxing.Box 的泛型目标类型。
    /// </summary>
    [Test]
    public async Task Should_preserve_dynamic_target_when_fixing_boxing()
    {
        var updatedSource = await ApplyFixAsync("""
            sealed class Sample
            {
                dynamic Box(int value) => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box<dynamic, int>(value)");
    }

    /// <summary>
    /// 验证可空值类型装箱为 object 时会使用保持 null 语义的可空值重载。
    /// </summary>
    [Test]
    public async Task Should_use_nullable_value_overload_for_nullable_boxing()
    {
        var updatedSource = await ApplyFixAsync("""
            sealed class Sample
            {
                object? Box(int? value) => value;
            }
            """);

        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box(value)");
    }

    /// <summary>
    /// 验证修复会保留装箱表达式前的注释。
    /// </summary>
    [Test]
    public async Task Should_preserve_trivia_when_fixing_boxing()
    {
        var updatedSource = await ApplyFixAsync("""
            sealed class Sample
            {
                object Box(int value) => /* intentional */ value;
            }
            """);

        await Assert.That(updatedSource).Contains("/* intentional */");
        await Assert.That(updatedSource).Contains(
            "global::TedToolkit.Annotations.Boxing.Boxing.Box(value)");
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "BoxingCodeFixTests", "BoxingCodeFixTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var compilerDiagnostics = await GetCompilerDiagnosticsAsync(document);
        var diagnostic = (await AnalyzeAsync(document)).Single();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await new BoxingCodeFixProvider().RegisterCodeFixesAsync(context);
        await Assert.That(actions).Count().IsEqualTo(1);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var updatedDocument = applyChanges.ChangedSolution.GetDocument(documentId)!;
        var updatedDiagnostics = await AnalyzeAsync(updatedDocument);
        await Assert.That(updatedDiagnostics).IsEmpty();

        var updatedCompilerDiagnostics = await GetCompilerDiagnosticsAsync(updatedDocument);
        await Assert.That(updatedCompilerDiagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
            compilerDiagnostics.Select(diagnostic => diagnostic.Id));

        return (await updatedDocument.GetTextAsync()).ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BoxingAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetCompilerDiagnosticsAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return compilation!.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(global::TedToolkit.Annotations.Boxing.Boxing).Assembly.Location))
            .ToImmutableArray();
    }
}
