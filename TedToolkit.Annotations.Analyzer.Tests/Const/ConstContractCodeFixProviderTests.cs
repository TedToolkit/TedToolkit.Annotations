// -----------------------------------------------------------------------
// <copyright file="ConstContractCodeFixProviderTests.cs" company="TedToolkit">
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

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

internal sealed class ConstContractCodeFixProviderTests
{
    /// <summary>
    /// 验证修复会根据接收者和实参访问深度生成方法及参数 Const 契约。
    /// </summary>
    [Test]
    public async Task Should_generate_method_and_parameter_contracts_when_annotated_calls_require_them()
    {
        var updatedSource = await ApplyFixAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Node
            {
                public Node Child { get; } = null!;

                [Const(ConstDepth.DEPTH0)]
                public void Read() { }
            }

            sealed class Sample
            {
                private readonly Node cache = null!;

                [Const(ConstDepth.DEPTH0)]
                private void Validate([Const(ConstDepth.DEPTH1)] Node node) { }

                void Inspect(Node node)
                {
                    cache.Read();
                    Validate(node.Child);
                }
            }
            """);

        await Assert.That(updatedSource).Contains("[Const(ConstDepth.DEPTH1_OR_LOWER)]\n    void Inspect");
        await Assert.That(updatedSource).Contains("void Inspect([Const(ConstDepth.DEPTH2)] Node node)");
    }

    /// <summary>
    /// 验证修复会将全部深度掩码简化为无参数的 Const 特性。
    /// </summary>
    [Test]
    public async Task Should_omit_depth_argument_when_inferred_contract_protects_all_depths()
    {
        var updatedSource = await ApplyFixAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Node
            {
                [Const]
                public void Read() { }
            }

            sealed class Sample
            {
                private readonly Node cache = null!;

                void Inspect() => cache.Read();
            }
            """);

        await Assert.That(updatedSource).Contains("[Const]\n    void Inspect");
    }

    /// <summary>
    /// 验证调用未标注 Const 的方法时不会提供契约生成修复。
    /// </summary>
    [Test]
    public async Task Should_not_offer_fix_when_an_invoked_method_has_no_const_contract()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Sample
            {
                private void Mutate() { }

                private void Inspect() => Mutate();
            }
            """);

        await Assert.That(diagnostics.Where(candidate => candidate.Id == ConstContractInferenceAnalyzer.DIAGNOSTIC_ID)).IsEmpty();
    }

    /// <summary>
    /// 验证多个不连续深度会合并为按位或的 ConstDepth 表达式。
    /// </summary>
    [Test]
    public async Task Should_combine_disjoint_depths_when_multiple_invocations_require_them()
    {
        var updatedSource = await ApplyFixAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [Const(ConstDepth.DEPTH0)]
                private void InspectDirectState() { }

                [Const(ConstDepth.DEPTH2)]
                private void InspectNestedState() { }

                private void Inspect()
                {
                    InspectDirectState();
                    InspectNestedState();
                }
            }
            """);

        await Assert.That(updatedSource).Contains(
            "ConstDepth.DEPTH0 | ConstDepth.DEPTH2");
    }

    /// <summary>
    /// 验证连续深度掩码会保留为对应的边界枚举成员。
    /// </summary>
    [Test]
    [Arguments("DEPTH1_OR_GREATER")]
    [Arguments("DEPTH2_OR_LOWER")]
    public async Task Should_preserve_contiguous_depth_range_when_formatting_inferred_contract(string depthName)
    {
        var updatedSource = await ApplyFixAsync($$"""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [Const(ConstDepth.{{depthName}})]
                private void InspectState() { }

                private void Inspect() => InspectState();
            }
            """);

        await Assert.That(updatedSource).Contains(
            $"ConstDepth.{depthName}");
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ConstCodeFixTests", "ConstCodeFixTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var diagnostic = (await AnalyzeAsync(document)).Single(candidate => candidate.Id == ConstContractInferenceAnalyzer.DIAGNOSTIC_ID);
        var actions = new List<CodeAction>();
        await new ConstContractCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).Count().IsEqualTo(1);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var updatedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(documentId)!;
        return (await updatedDocument.GetTextAsync()).ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ConstContractInferenceAnalyzer()),
                ConstAnalyzerTestHelper.CreateAnalyzerOptions(enableConstAnalysis: true))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ConstCodeFixDiagnostics", "ConstCodeFixDiagnostics", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        return await AnalyzeAsync(workspace.CurrentSolution.GetDocument(documentId)!);
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
