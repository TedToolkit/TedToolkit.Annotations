// -----------------------------------------------------------------------
// <copyright file="BehaviorCaseUnitTestAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer;
using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

/// <summary>
/// Contains tests for behavior case unit test analyzer.
/// </summary>
internal sealed class BehaviorCaseUnitTestAnalyzerTests
{
    /// <summary>
    /// 验证未覆盖的行为用例报告信息级提示。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_info_when_behavior_case_has_no_unit_test()
    {
        var diagnostics = await AnalyzeAsync("""
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [BehaviorCase("empty input", "Returns an empty result.", hasUnitTest: false)]
                void Execute() { }
            }
            """).ConfigureAwait(false);

        var diagnostic = diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo(BehaviorCaseUnitTestAnalyzer.DIAGNOSTIC_ID);
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Info);
        await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .IsEqualTo("This behavior case is not covered by a unit test.");
    }

    /// <summary>
    /// 验证已覆盖的普通和泛型行为用例不会报告提示。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_not_report_when_behavior_case_has_unit_test()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                [BehaviorCase("empty input", "Returns an empty result.", hasUnitTest: true)]
                [BehaviorCase<ArgumentException>("invalid input", "Throws.", hasUnitTest: true)]
                void Execute() { }
            }
            """).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "BehaviorCaseUnitTestTests",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["ANNOTATIONS_BEHAVIOR_CASE",])),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        await Assert.That(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        return await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BehaviorCaseUnitTestAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
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