// -----------------------------------------------------------------------
// <copyright file="AssertionPreconditionGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using static TedToolkit.RoslynHelper.SourceComposer;
using static TedToolkit.RoslynHelper.SourceComposer<TedToolkit.Annotations.Assertions.Analyzer.AssertionPreconditionGenerator>;

namespace TedToolkit.Annotations.Assertions.Analyzer;

/// <summary>
/// Generates a <c>*PreconditionAttribute</c> for each assertion item.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AssertionPreconditionGenerator : IIncrementalGenerator
{
    private const string AssertionItemMetadataName = "TedToolkit.Assertions.IAssertionItem<TSubject>";

    private const string AssertionMethodNameAttribute = "TedToolkit.Assertions.Attributes.AssertionMethodNameAttribute";

    private const string AssertionParameterNameAttribute = "TedToolkit.Assertions.Attributes.AssertionParameterNameAttribute";

    private const string CallerArgumentExpressionAttribute = "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is StructDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    syntaxContext.SemanticModel.GetDeclaredSymbol((StructDeclarationSyntax)syntaxContext.Node,
                        cancellationToken) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        context.RegisterSourceOutput(candidates, static (sourceContext, symbol) => Generate(sourceContext, symbol!));
        context.RegisterSourceOutput(context.CompilationProvider, static (sourceContext, compilation) =>
        {
            foreach (var assembly in compilation.References
                         .Select(compilation.GetAssemblyOrModuleSymbol)
                         .OfType<IAssemblySymbol>())
            {
                foreach (var type in GetTypes(assembly.GlobalNamespace))
                {
                    Generate(sourceContext, type);
                }
            }
        });
    }

    private static IEnumerable<INamedTypeSymbol> GetTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetTypes(nestedNamespace))
            {
                yield return type;
            }
        }
    }

    private static void Generate(in SourceProductionContext context, INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind is not TypeKind.Struct
            || !symbol.AllInterfaces.Any(static item => item.OriginalDefinition.ToDisplayString() == AssertionItemMetadataName))
        {
            return;
        }

        var constructor = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static item => item.MethodKind is MethodKind.Constructor)
            .OrderByDescending(static item => item.Parameters.Length)
            .FirstOrDefault();
        var parameters = constructor?.Parameters
            .Where(static parameter => !IsAssertionMetadataParameter(parameter))
            .ToArray() ?? Array.Empty<IParameterSymbol>();

        foreach (var assertionName in GetAssertionNames(symbol))
        {
            Generate(context, symbol, assertionName, parameters);
        }
    }

    private static void Generate(
        in SourceProductionContext context,
        INamedTypeSymbol assertionItem,
        string assertionName,
        IReadOnlyList<IParameterSymbol> parameters)
    {
        var attributeName = $"{assertionName}PreconditionAttribute";
        var nameSpace = NameSpace(assertionItem.ContainingNamespace.ToDisplayString());

        // A generic assertion has two usable attribute forms: the generic form preserves
        // its type parameter, while the non-generic form accepts its value as text.
        if (assertionItem.TypeParameters.Length > 0)
        {
            nameSpace.AddMember(CreateAttributeDeclaration(
                attributeName,
                assertionName,
                parameters,
                assertionItem.TypeParameters,
                preserveGenericParameters: true));
        }

        nameSpace.AddMember(CreateAttributeDeclaration(
            attributeName,
            assertionName,
            parameters,
            [],
            preserveGenericParameters: false));
        File().AddNameSpace(nameSpace).Generate(
            context,
            $"{assertionItem.ContainingNamespace.ToDisplayString()}.{assertionName}.PreconditionAttribute");
    }

    private static TypeDeclaration CreateAttributeDeclaration(
        string attributeName,
        string assertionName,
        IReadOnlyList<IParameterSymbol> assertionParameters,
        IEnumerable<ITypeParameterSymbol> typeParameters,
        bool preserveGenericParameters)
    {
        var declaration = Class(attributeName).Public.Sealed
            .AddBaseType(new DataType("TedToolkit.Annotations.Assertions.AssertionPreconditionAttribute".ToSimpleName()))
            .AddRootDescription(new DescriptionInheritDoc());
        declaration.AddAttribute(Attribute(DataType.FromType<AttributeUsageAttribute>())
            .AddArgument(Argument("global::System.AttributeTargets.Parameter".ToSimpleName()))
            .AddNamedArgument("AllowMultiple", "true".ToSimpleName())
            .AddNamedArgument("Inherited", "false".ToSimpleName()));
        AddTypeParameters(declaration, typeParameters);

        var constructor = Constructor().Public
            .AddRootDescription(new DescriptionInheritDoc())
            .AddInitializer(new ConstructorInitializer(isBase: true)
                .AddArgument(Argument($"Must satisfy {assertionName}.".ToLiteral()))
                .AddArgument(Argument("reason".ToSimpleName()))
                .AddArgument(Argument("exceptionType".ToSimpleName())));

        foreach (var parameter in assertionParameters)
        {
            constructor.AddParameter(CreateAttributeParameter(parameter, preserveGenericParameters));
        }

        constructor.AddParameter(Parameter(DataType.FromType<string>().Null, "reason").AddNull());
        constructor.AddParameter(Parameter(DataType.FromType<Type>().Null, "exceptionType").AddNull());
        declaration.AddMember(constructor);
        constructor.Owner = attributeName;

        return declaration;
    }

    private static Parameter CreateAttributeParameter(IParameterSymbol parameter, bool preserveGenericParameters)
    {
        var isAttributeCompatible = IsAttributeParameterType(parameter.Type)
            || (preserveGenericParameters && parameter.Type.TypeKind is TypeKind.TypeParameter);
        var type = isAttributeCompatible
            ? DataType.FromSymbol(parameter.Type)
            : DataType.FromType<string>();
        if (parameter.HasExplicitDefaultValue && parameter.Type.NullableAnnotation is not NullableAnnotation.Annotated)
        {
            type = type.Null;
        }

        var result = Parameter(type, parameter.Name);
        return parameter.HasExplicitDefaultValue ? result.AddNull() : result;
    }

    private static void AddTypeParameters(
        TypeDeclaration declaration,
        IEnumerable<ITypeParameterSymbol> typeParameters)
    {
        foreach (var typeParameter in typeParameters)
        {
            declaration.AddTypeParameter(TypeParameter(typeParameter));
        }
    }

    private static IEnumerable<string> GetAssertionNames(INamedTypeSymbol assertionItem)
    {
        var aliases = assertionItem.GetAttributes()
            .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == AssertionMethodNameAttribute)
            .SelectMany(static attribute => attribute.ConstructorArguments)
            .Select(static argument => argument.Value as string)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>();
        return aliases.Append(assertionItem.Name).Distinct(StringComparer.Ordinal);
    }

    private static bool IsAssertionMetadataParameter(IParameterSymbol parameter)
    {
        return parameter.GetAttributes().Any(static attribute =>
                   attribute.AttributeClass?.ToDisplayString() is AssertionParameterNameAttribute or CallerArgumentExpressionAttribute)
            || IsGeneratedExpressionNameParameter(parameter);
    }

    private static bool IsGeneratedExpressionNameParameter(IParameterSymbol parameter)
    {
        return parameter.Name.EndsWith("Name", StringComparison.Ordinal)
            && parameter.Type.SpecialType is SpecialType.System_String;
    }

    private static bool IsAttributeParameterType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Enum)
        {
            return true;
        }

        if (type is IArrayTypeSymbol { Rank: 1, } arrayType)
        {
            return IsAttributeParameterType(arrayType.ElementType);
        }

        return type.SpecialType is
            SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Char
            or SpecialType.System_String
            or SpecialType.System_Object
            || type.ToDisplayString() == "System.Type";
    }
}