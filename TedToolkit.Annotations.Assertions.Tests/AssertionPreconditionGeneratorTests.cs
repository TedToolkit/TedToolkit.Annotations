// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionGeneratorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.Annotations.Assertions.Analyzer;
using TedToolkit.Assertions;

namespace TedToolkit.Annotations.Assertions.Tests;

/// <summary>
/// Contains tests for <see cref="AssertionPreconditionGenerator"/>.
/// </summary>
internal sealed class AssertionPreconditionGeneratorTests
{
    /// <summary>
    /// 验证断言项会生成以后缀 Precondition 命名的特性。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_precondition_attribute_when_assertion_item_is_declared()
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct BePositive : IAssertionItem<int>
                {
                    public bool IsPassed(int subject) => subject > 0;
                    public string GenerateMessage(scoped in ObjectAssertion<int> assertion) => "";
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results
            .SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.BePositive.PreconditionAttribute.g.cs")
            .SourceText.ToString();
        await Assert.That(generatedSource).Contains("BePositivePreconditionAttribute");
        await Assert.That(generatedSource).DoesNotContain("TException");
        await Assert.That(generatedSource).Contains("Type? exceptionType = null");
        await Assert.That(generatedSource).Contains("AssertionPreconditionAttribute");
    }

    /// <summary>
    /// 验证引用的 Assertions 程序集中已有断言项也会生成前置条件特性。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_generate_precondition_attribute_when_assertion_item_is_referenced()
    {
        var compilation = CSharpCompilation.Create(
            "ReferencedGeneratorTests",
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSources = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources).ToArray();
        await Assert.That(generatedSources.Select(static source => source.HintName))
            .Contains("TedToolkit.Assertions.BeGreaterThan.PreconditionAttribute.g.cs");
        await Assert.That(generatedSources
                .Single(static source => source.HintName == "TedToolkit.Assertions.BeGreaterThan.PreconditionAttribute.g.cs")
                .SourceText.ToString())
            .Contains("string comparedValue");
        await Assert.That(generatedSources
                .Single(static source => source.HintName == "TedToolkit.Assertions.BeGreaterThan.PreconditionAttribute.g.cs")
                .SourceText.ToString())
            .DoesNotContain("comparedValueName");
    }

    /// <summary>
    /// 验证生成的特性保留可在 Attribute 中表达的参数类型，并将默认参数改为可空的 null 默认值。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_preserve_attribute_parameter_types_and_nullable_defaults()
    {
        var compilation = CSharpCompilation.Create(
            "TypedParameterGeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct BeWithin(int minimum, int maximum = 10, object? comparer = null) : IAssertionItem<int>
                {
                    public bool IsPassed(int subject) => true;
                    public string GenerateMessage(scoped in ObjectAssertion<int> assertion) => "";
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.BeWithin.PreconditionAttribute.g.cs").SourceText.ToString();
        await Assert.That(generatedSource).Contains("int minimum");
        await Assert.That(generatedSource).Contains("int? maximum = null");
        await Assert.That(generatedSource).Contains("object? comparer = null");
        await Assert.That(generatedSource).Contains("string? reason = null");
        await Assert.That(generatedSource).Contains("Type? exceptionType = null");
        await Assert.That(generatedSource).Contains("/// <inheritdoc/>");
        await Assert.That(generatedSource).Contains("AttributeTargets.Parameter");
        await Assert.That(generatedSource).Contains("AllowMultiple = true");
    }

    /// <summary>
    /// 验证字符串断言项会保留字符串参数类型。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_preserve_string_parameter_for_string_assertion_item()
    {
        var compilation = CSharpCompilation.Create(
            "StringAssertionGeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct HavePrefix(string prefix) : IAssertionItem<string>
                {
                    public bool IsPassed(string subject) => true;
                    public string GenerateMessage(scoped in ObjectAssertion<string> assertion) => "";
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.HavePrefix.PreconditionAttribute.g.cs").SourceText.ToString();
        await Assert.That(generatedSource).Contains("string prefix");
    }

    /// <summary>
    /// 验证生成的特性只允许标注参数且可重复标注。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_allow_multiple_preconditions_only_on_parameters()
    {
        var compilation = CSharpCompilation.Create(
            "AttributeUsageGeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct HavePrefix(string prefix) : IAssertionItem<string>
                {
                    public bool IsPassed(string subject) => true;
                    public string GenerateMessage(scoped in ObjectAssertion<string> assertion) => "";
                }

                public sealed class Consumer
                {
                    [HavePrefixPrecondition("a")]
                    public void InvalidTarget() { }

                    public void ValidTarget(
                        [HavePrefixPrecondition("a"), HavePrefixPrecondition("b")] string value) { }
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.HavePrefix.PreconditionAttribute.g.cs").SourceText;
        var diagnostics = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(generatedSource))
            .GetDiagnostics();
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("CS0592");
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("CS0579");
    }

    /// <summary>
    /// 验证用于捕获调用方表达式的参数不会出现在生成的特性中。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_skip_caller_argument_expression_parameter()
    {
        var compilation = CSharpCompilation.Create(
            "CallerArgumentExpressionGeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using System.Runtime.CompilerServices;
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct BeExpected(
                    int expected,
                    [CallerArgumentExpression(nameof(expected))] string? expectedName = null,
                    string? generatedExpressionName = null) : IAssertionItem<int>
                {
                    public bool IsPassed(int subject) => true;
                    public string GenerateMessage(scoped in ObjectAssertion<int> assertion) => "";
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.BeExpected.PreconditionAttribute.g.cs").SourceText.ToString();
        await Assert.That(generatedSource).Contains("int expected");
        await Assert.That(generatedSource).DoesNotContain("expectedName");
        await Assert.That(generatedSource).DoesNotContain("generatedExpressionName");
    }

    /// <summary>
    /// 验证泛型断言项会同时生成保留泛型参数和使用字符串参数的前置条件特性。
    /// </summary>
    /// <returns>表示异步测试操作的任务。</returns>
    [Test]
    public async Task Should_preserve_generic_assertion_item_shape()
    {
        var compilation = CSharpCompilation.Create(
            "GenericGeneratorTests",
            [CSharpSyntaxTree.ParseText("""
                using TedToolkit.Assertions;

                namespace Sample;

                public readonly struct BeEquivalent<T>(T expected, object? comparer = null) : IAssertionItem<T>
                {
                    public bool IsPassed(T subject) => true;
                    public string GenerateMessage(scoped in ObjectAssertion<T> assertion) => "";
                }
                """),],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AssertionPreconditionGenerator());

        driver = driver.RunGenerators(compilation);

        var generatedSource = driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Single(static source => source.HintName == "Sample.BeEquivalent.PreconditionAttribute.g.cs").SourceText.ToString();
        await Assert.That(generatedSource).Contains("class BeEquivalentPreconditionAttribute<");
        await Assert.That(generatedSource).Contains("class BeEquivalentPreconditionAttribute :");
        await Assert.That(generatedSource).Contains("T expected");
        await Assert.That(generatedSource).Contains("string expected");
        await Assert.That(generatedSource).Contains("object? comparer = null");
        await Assert.That(generatedSource).Contains("string? reason = null");
        await Assert.That(generatedSource).Contains("Type? exceptionType = null");
        await Assert.That(generatedSource).DoesNotContain("TException");
        var generatedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(generatedSource));
        await Assert.That(generatedCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error))
            .IsEmpty();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IAssertionItem<>).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(AssertionPreconditionAttribute).Assembly.Location));
    }
}