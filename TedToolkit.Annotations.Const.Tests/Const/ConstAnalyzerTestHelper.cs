// -----------------------------------------------------------------------
// <copyright file="ConstAnalyzerTestHelper.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Const;

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

/// <summary>
/// Provides shared functionality for const analyzer test helper.
/// </summary>
internal static class ConstAnalyzerTestHelper
{
    /// <summary>
    /// Analyzes source with const analysis enabled.
    /// </summary>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="additionalReferences">Additional compilation references.</param>
    /// <returns>The analyzer diagnostics.</returns>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        params MetadataReference[] additionalReferences)
    {
        return AnalyzeAsync(source, enableConstAnalysis: true, additionalReferences: additionalReferences);
    }

    /// <summary>
    /// Analyzes source with the requested const-analysis setting.
    /// </summary>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="enableConstAnalysis">Whether const analysis is enabled.</param>
    /// <param name="additionalReferences">Additional compilation references.</param>
    /// <returns>The analyzer diagnostics.</returns>
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        bool enableConstAnalysis,
        params MetadataReference[] additionalReferences)
    {
        var compilation = CreateCompilation(source, additionalReferences);
        var compilerDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        await Assert.That(compilerDiagnostics).IsEmpty();

        return await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ConstMutationAnalyzer()),
                CreateAnalyzerOptions(enableConstAnalysis))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates analyzer options for the requested const-analysis setting.
    /// </summary>
    /// <param name="enableConstAnalysis">Whether const analysis is enabled.</param>
    /// <returns>The configured analyzer options.</returns>
    internal static AnalyzerOptions CreateAnalyzerOptions(bool enableConstAnalysis)
    {
        var options = enableConstAnalysis
            ? ImmutableDictionary<string, string>.Empty.Add(
                ConstAnalysisOptions.ENABLE_CONST_ANALYSIS_PROPERTY_NAME,
                bool.TrueString)
            : ImmutableDictionary<string, string>.Empty;
        return new([], new TestAnalyzerConfigOptionsProvider(options));
    }

    /// <summary>
    /// Compiles source code into an in-memory metadata reference.
    /// </summary>
    /// <param name="source">The source code to compile.</param>
    /// <returns>The compiled metadata reference.</returns>
    /// <exception cref="InvalidOperationException">The source code cannot be compiled.</exception>
    internal static MetadataReference CompileReference(string source)
    {
        using var stream = new MemoryStream();
        var compilation = CreateCompilation(source, []);
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        IReadOnlyCollection<MetadataReference> additionalReferences)
    {
        return CSharpCompilation.Create(
            "ConstAnalyzerTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)),],
            GetMetadataReferences().AddRange(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true));
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