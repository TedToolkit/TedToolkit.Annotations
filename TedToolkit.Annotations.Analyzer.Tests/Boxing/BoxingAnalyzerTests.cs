// -----------------------------------------------------------------------
// <copyright file="BoxingAnalyzerTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests.Boxing;

internal sealed class BoxingAnalyzerTests
{
    /// <summary>
    /// 验证到 object 和接口的隐式及显式装箱都会产生信息提示。
    /// </summary>
    [Test]
    public async Task Should_report_implicit_and_explicit_boxing_conversions()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            sealed class Sample
            {
                object BoxObject(int value) => value;

                IComparable BoxInterface(int value) => (IComparable)value;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            BoxingAnalyzer.DIAGNOSTIC_ID,
            BoxingAnalyzer.DIAGNOSTIC_ID,
        ]);
        await Assert.That(diagnostics.All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Info)).IsTrue();
    }

    /// <summary>
    /// 验证引用转换和通过 Explicit.Box 表达的显式装箱不会产生提示。
    /// </summary>
    [Test]
    public async Task Should_not_report_reference_conversions_or_explicit_box_calls()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Sample
            {
                object BoxObject(int value) => Explicit.Box(value);

                IComparable BoxInterface(int value) => Explicit.Box<IComparable, int>(value);

                object Upcast(string value) => value;
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证可空值类型和受值类型约束泛型参数的确定装箱会产生提示。
    /// </summary>
    [Test]
    public async Task Should_report_nullable_and_struct_constrained_generic_boxing()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Sample
            {
                object? BoxNullable(int? value) => value;

                object BoxGeneric<T>(T value)
                    where T : struct
                    => value;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(
        [
            BoxingAnalyzer.DIAGNOSTIC_ID,
            BoxingAnalyzer.DIAGNOSTIC_ID,
        ]);
    }

    /// <summary>
    /// 验证未约束泛型参数因运行时可能是引用类型而不会报告确定装箱。
    /// </summary>
    [Test]
    public async Task Should_not_report_runtime_dependent_boxing_for_unconstrained_generic()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Sample
            {
                object Box<T>(T value) => value;
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// 验证装箱为 ValueType、Enum、接口、dynamic、参数和 params 元素都会产生提示。
    /// </summary>
    [Test]
    public async Task Should_report_boxing_for_supported_reference_targets_and_arguments()
    {
        var diagnostics = await AnalyzeAsync("""
            using System;

            enum Mode
            {
                Default,
            }

            sealed class Sample
            {
                void Consume(object value) { }

                void Execute(int value, Mode mode)
                {
                    ValueType asValueType = value;
                    Enum asEnum = mode;
                    IComparable asInterface = value;
                    dynamic asDynamic = value;
                    Consume(value);
                    _ = string.Format("{0}", value);
                }
            }
            """);

        await Assert.That(diagnostics).Count().IsEqualTo(6);
        await Assert.That(diagnostics.All(diagnostic => diagnostic.Id == BoxingAnalyzer.DIAGNOSTIC_ID)).IsTrue();
    }

    /// <summary>
    /// 验证不会为无装箱分配的插值、模式匹配和受约束泛型调用产生提示。
    /// </summary>
    [Test]
    public async Task Should_not_report_operations_that_do_not_box_at_runtime()
    {
        var diagnostics = await AnalyzeAsync("""
            sealed class Sample
            {
                string Interpolate(int value) => $"{value}";

                bool Match(int value) => value is object;

                string ConstrainedCall<T>(T value)
                    where T : struct
                    => value.ToString();
            }
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "BoxingAnalyzerTests",
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

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BoxingAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return trustedPlatformAssemblies!
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Explicit).Assembly.Location))
            .ToImmutableArray();
    }
}