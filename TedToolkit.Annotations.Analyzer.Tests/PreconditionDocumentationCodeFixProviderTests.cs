// -----------------------------------------------------------------------
// <copyright file="PreconditionDocumentationCodeFixProviderTests.cs" company="TedToolkit">
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
using TedToolkit.Annotations.Analyzer;
using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class PreconditionDocumentationCodeFixProviderTests
{
    /// <summary>
    /// 验证缺少异常文档的前置条件报告信息级提示。
    /// </summary>
    [Test]
    public async Task Should_report_info_diagnostic_when_exception_documentation_can_be_generated()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                void Execute([Precondition<ArgumentNullException>("Must not be null.")] string value) { }
            }
            """);

        var diagnostic = diagnostics.Single(candidate => candidate.Id == PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID);
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    /// <summary>
    /// 验证修复会为相同异常类型的参数前置条件分别生成异常文档。
    /// </summary>
    [Test]
    public async Task Should_generate_separate_exception_documentation_when_parameters_use_same_exception()
    {
        var updatedSource = await ApplyFixAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                /// <summary>Copies bytes.</summary>
                void Copy(
                    [Precondition<ArgumentNullException>("Must not be null.")] byte[] source,
                    [Precondition<ArgumentNullException>("Must not be null.")] byte[] destination) { }
            }
            """);

        await Assert.That(updatedSource).Contains("<summary>Copies bytes.</summary>");
        await Assert.That(updatedSource).Contains("<exception cref=\"global::System.ArgumentNullException\">");
        await Assert.That(updatedSource).Contains("<paramref name=\"source\"/> Must not be null.");
        await Assert.That(updatedSource).Contains("<paramref name=\"destination\"/> Must not be null.");
        await Assert.That(CountOccurrences(updatedSource, "<exception cref=\"global::System.ArgumentNullException\">")).IsEqualTo(2);
    }

    /// <summary>
    /// 验证修复会为成员级跨参数前置条件生成单独的异常文档。
    /// </summary>
    [Test]
    public async Task Should_generate_exception_documentation_when_precondition_describes_parameter_relationship()
    {
        var updatedSource = await ApplyFixAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [Precondition<ArgumentException>("start and length must designate a valid range.")]
                void Slice(int start, int length) { }
            }
            """);

        await Assert.That(updatedSource).Contains("<exception cref=\"global::System.ArgumentException\">");
        await Assert.That(updatedSource).Contains("start and length must designate a valid range.");
    }

    /// <summary>
    /// 验证已生成的异常文档不会再次触发前置条件文档提示。
    /// </summary>
    [Test]
    public async Task Should_not_report_diagnostic_when_generated_exception_documentation_is_present()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                /// <exception cref="global::System.ArgumentNullException">
                /// <paramref name="value"/> Must not be null.
                /// </exception>
                void Execute([Precondition<ArgumentNullException>("Must not be null.")] string value) { }
            }
            """);

        await Assert.That(diagnostics.Where(candidate => candidate.Id == PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID)).IsEmpty();
    }

    /// <summary>
    /// 验证未声明异常类型的前置条件不会触发异常文档提示。
    /// </summary>
    [Test]
    public async Task Should_not_report_diagnostic_when_precondition_does_not_declare_exception_type()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                void Execute([Precondition("Must not be empty.")] string value) { }
            }
            """);

        await Assert.That(diagnostics.Where(candidate => candidate.Id == PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID)).IsEmpty();
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "CodeFixTests", "CodeFixTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var diagnostics = await AnalyzeAsync(document);
        var diagnostic = diagnostics.Single(candidate => candidate.Id == PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new PreconditionDocumentationCodeFixProvider().RegisterCodeFixesAsync(context);
        await Assert.That(actions).Count().IsEqualTo(1);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var updatedDocument = applyChanges.ChangedSolution.GetDocument(documentId)!;
        var updatedSource = (await updatedDocument.GetTextAsync()).ToString();

        var updatedDiagnostics = await AnalyzeAsync(updatedDocument);
        await Assert.That(updatedDiagnostics.Where(candidate => candidate.Id == PreconditionDocumentationAnalyzer.DIAGNOSTIC_ID)).IsEmpty();

        return updatedSource;
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "PreconditionDocumentationTests", "PreconditionDocumentationTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, GetMetadataReferences())
            .AddDocument(documentId, "Sample.cs", SourceText.From(source));
        workspace.TryApplyChanges(solution);

        return await AnalyzeAsync(workspace.CurrentSolution.GetDocument(documentId)!);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PreconditionDocumentationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(PreconditionAttribute).Assembly.Location))
            .ToImmutableArray();
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
