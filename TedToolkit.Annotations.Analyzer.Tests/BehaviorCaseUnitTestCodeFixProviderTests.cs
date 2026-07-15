// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseUnitTestCodeFixProviderTests.cs" company="TedToolkit">
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

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class BehaviorCaseUnitTestCodeFixProviderTests
{
    /// <summary>
    /// 验证修复会将显式未覆盖的行为用例标记为已覆盖。
    /// </summary>
    [Test]
    public async Task Should_mark_explicitly_uncovered_behavior_case_as_tested()
    {
        var updatedSource = await ApplyFixAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [BehaviorCase("empty input", "Returns an empty result.", hasUnitTest: false)]
                void Execute() { }
            }
            """);

        await Assert.That(updatedSource).Contains("hasUnitTest: true");
    }

    /// <summary>
    /// 验证修复会为使用默认值的行为用例添加已覆盖标记。
    /// </summary>
    [Test]
    public async Task Should_add_tested_flag_when_behavior_case_uses_default_value()
    {
        var updatedSource = await ApplyFixAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [BehaviorCase("empty input", "Returns an empty result.")]
                void Execute() { }
            }
            """);

        await Assert.That(updatedSource).Contains("hasUnitTest: true");
    }

    /// <summary>
    /// 验证修复不支持批量操作。
    /// </summary>
    [Test]
    public async Task Should_not_support_fix_all()
    {
        await Assert.That(new BehaviorCaseUnitTestCodeFixProvider().GetFixAllProvider()).IsNull();
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "BehaviorCaseCodeFixTests", "BehaviorCaseCodeFixTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["ANNOTATIONS_BEHAVIOR_CASE"]))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var diagnostic = (await AnalyzeAsync(document)).Single();
        var actions = new List<CodeAction>();
        await new BehaviorCaseUnitTestCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).Count().IsEqualTo(1);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var updatedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(documentId)!;
        await Assert.That(await AnalyzeAsync(updatedDocument)).IsEmpty();
        return (await updatedDocument.GetTextAsync()).ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BehaviorCaseUnitTestAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(BehaviorCaseAttribute).Assembly.Location))
            .ToImmutableArray();
    }
}
