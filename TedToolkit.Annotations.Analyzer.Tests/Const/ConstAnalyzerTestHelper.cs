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

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests.Const;

internal static class ConstAnalyzerTestHelper
{
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        params MetadataReference[] additionalReferences)
    {
        return await AnalyzeAsync(source, enableConstAnalysis: true, additionalReferences: additionalReferences);
    }

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
            .GetAnalyzerDiagnosticsAsync();
    }

    internal static AnalyzerOptions CreateAnalyzerOptions(bool enableConstAnalysis)
    {
        var options = enableConstAnalysis
            ? ImmutableDictionary<string, string>.Empty.Add(
                AnalysisOptions.ENABLE_CONST_ANALYSIS_PROPERTY_NAME,
                bool.TrueString)
            : ImmutableDictionary<string, string>.Empty;
        return new AnalyzerOptions([], new TestAnalyzerConfigOptionsProvider(options));
    }

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
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
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

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

        private static AnalyzerConfigOptions Empty { get; } = new TestAnalyzerConfigOptions([]);
    }

    private sealed class TestAnalyzerConfigOptions(ImmutableDictionary<string, string> options) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => options.TryGetValue(key, out value!);
    }
}
