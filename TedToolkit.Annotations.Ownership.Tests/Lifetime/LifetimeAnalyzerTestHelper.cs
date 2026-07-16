// -----------------------------------------------------------------------
// <copyright file="LifetimeAnalyzerTestHelper.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Ownership;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime;

/// <summary>
/// Provides shared functionality for lifetime analyzer test helper.
/// </summary>
internal static class LifetimeAnalyzerTestHelper
{
    /// <summary>
    /// Analyzes source code for disposable-lifetime diagnostics.
    /// </summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>The analyzer diagnostics.</returns>
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "LifetimeTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)),],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        await Assert.That(compilerDiagnostics).IsEmpty();

        return await compilation
            .WithAnalyzers([new DisposableLifetimeAnalyzer(),], CreateAnalyzerOptions(enableOwnershipAnalysis: true))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates analyzer options for the requested ownership-analysis setting.
    /// </summary>
    /// <param name="enableOwnershipAnalysis">Whether ownership analysis is enabled.</param>
    /// <returns>The configured analyzer options.</returns>
    internal static AnalyzerOptions CreateAnalyzerOptions(bool enableOwnershipAnalysis)
    {
        var options = enableOwnershipAnalysis
            ? ImmutableDictionary<string, string>.Empty.Add(
                OwnershipAnalysisOptions.ENABLE_OWNERSHIP_ANALYSIS_PROPERTY_NAME,
                bool.TrueString)
            : ImmutableDictionary<string, string>.Empty;
        return new([], new TestAnalyzerConfigOptionsProvider(options));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        return AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!
            .ToString()!
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(OwnershipAttribute).Assembly.Location))
            .ToImmutableArray();
    }

    private sealed class TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
        : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(globalOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return Empty;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return Empty;
        }

        private static AnalyzerConfigOptions Empty { get; } = new TestAnalyzerConfigOptions([]);
    }

    private sealed class TestAnalyzerConfigOptions(ImmutableDictionary<string, string> options) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            return options.TryGetValue(key, out value!);
        }
    }
}