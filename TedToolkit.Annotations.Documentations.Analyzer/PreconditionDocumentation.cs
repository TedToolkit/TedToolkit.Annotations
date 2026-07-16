// -----------------------------------------------------------------------
// <copyright file="PreconditionDocumentation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Creates XML documentation entries from precondition attributes.
/// </summary>
internal static class PreconditionDocumentation
{
    private const string PRECONDITION_ATTRIBUTE_NAME = "TedToolkit.Annotations.Documentations.PreconditionAttribute";

    private const string GENERIC_PRECONDITION_ATTRIBUTE_NAME = "TedToolkit.Annotations.Documentations.PreconditionAttribute<TException>";

    /// <summary>
    /// Gets precondition entries that are absent from XML documentation.
    /// </summary>
    /// <param name="method">The annotated method.</param>
    /// <param name="documentation">Existing XML documentation.</param>
    /// <returns>The entries not present in <paramref name="documentation"/>.</returns>
    internal static IEnumerable<Entry> GetMissingEntries(IMethodSymbol method, string documentation)
    {
        var normalizedDocumentation = string.Join(
            "\n",
            documentation.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n')
                .Select(line => line.TrimStart()));
        return GetEntries(method).Where(
            entry => !normalizedDocumentation.Contains(entry.ToDocumentation().Trim(), StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets entries represented by a method's precondition attributes.
    /// </summary>
    /// <param name="method">The annotated method.</param>
    /// <returns>The corresponding documentation entries.</returns>
    internal static IEnumerable<Entry> GetEntries(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (TryGetEntry(attribute, parameterName: null, out var entry))
            {
                yield return entry;
            }
        }

        foreach (var parameter in method.Parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (TryGetEntry(attribute, parameter.Name, out var entry))
                {
                    yield return entry;
                }
            }
        }
    }

    private static bool TryGetEntry(AttributeData attribute, string? parameterName, out Entry entry)
    {
        var attributeType = attribute.AttributeClass;
        if (attributeType is null)
        {
            entry = default;
            return false;
        }

        var description = attribute.ConstructorArguments.FirstOrDefault().Value as string;

        ITypeSymbol? exceptionType = null;
        if (attributeType.ToDisplayString() == PRECONDITION_ATTRIBUTE_NAME && attribute.ConstructorArguments.Length > 1)
        {
            exceptionType = attribute.ConstructorArguments[1].Value as ITypeSymbol;
        }
        else if (attributeType.OriginalDefinition.ToDisplayString() == GENERIC_PRECONDITION_ATTRIBUTE_NAME)
        {
            exceptionType = attributeType.TypeArguments[0];
        }

        if (description is null && TryGetGeneratedAssertionPrecondition(attribute, out var generatedEntry))
        {
            description = generatedEntry.Description;
            exceptionType = generatedEntry.ExceptionType;
        }

        if (description is null || exceptionType is null)
        {
            entry = default;
            return false;
        }

        entry = new(exceptionType, parameterName, description);
        return true;
    }

    private static bool TryGetGeneratedAssertionPrecondition(
        AttributeData attribute,
        out (string Description, ITypeSymbol? ExceptionType) metadata)
    {
        const string suffix = "PreconditionAttribute";
        if (attribute.AttributeClass is not { } attributeType
            || !attributeType.Name.EndsWith(suffix, StringComparison.Ordinal)
            || attributeType.Name.Length == suffix.Length
            || !DerivesFromPrecondition(attributeType))
        {
            metadata = default;
            return false;
        }

        var assertionName = attributeType.Name.Substring(0, attributeType.Name.Length - suffix.Length);
        var exceptionType = GetGeneratedExceptionType(attribute);
        metadata = ($"Must satisfy {assertionName}.", exceptionType);
        return true;
    }

    private static ITypeSymbol? GetGeneratedExceptionType(AttributeData attribute)
    {
        var attributeType = attribute.AttributeClass!;
        var exceptionTypeParameterIndex = attributeType.OriginalDefinition.TypeParameters
            .Select(static (parameter, index) => (parameter, index))
            .Where(static item => item.parameter.Name == "TException")
            .Select(static item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        if (exceptionTypeParameterIndex >= 0)
        {
            return attributeType.TypeArguments[exceptionTypeParameterIndex];
        }

        var exceptionParameterIndex = attribute.AttributeConstructor?.Parameters
            .Select(static (parameter, index) => (parameter, index))
            .Where(static item => item.parameter.Name == "exceptionType")
            .Select(static item => item.index)
            .DefaultIfEmpty(-1)
            .First() ?? -1;
        return exceptionParameterIndex >= 0 && attribute.ConstructorArguments.Length > exceptionParameterIndex
            ? attribute.ConstructorArguments[exceptionParameterIndex].Value as ITypeSymbol
            : null;
    }

    private static bool DerivesFromPrecondition(INamedTypeSymbol attributeType)
    {
        for (var type = attributeType.BaseType; type is not null; type = type.BaseType)
        {
            if (type.OriginalDefinition.ToDisplayString() == PRECONDITION_ATTRIBUTE_NAME)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Represents one exception documentation entry.
    /// </summary>
    /// <param name="exceptionType">The exception type.</param>
    /// <param name="parameterName">The associated parameter name, if any.</param>
    /// <param name="description">The precondition description.</param>
    internal readonly struct Entry(ITypeSymbol exceptionType, string? parameterName, string description)
    {
        /// <summary>
        /// Gets the exception type.
        /// </summary>
        internal ITypeSymbol ExceptionType { get; } = exceptionType;

        /// <summary>
        /// Gets the associated parameter name, if any.
        /// </summary>
        internal string? ParameterName { get; } = parameterName;

        /// <summary>
        /// Gets the precondition description.
        /// </summary>
        internal string Description { get; } = description;

        /// <summary>
        /// Gets the plain-text representation of the entry.
        /// </summary>
        internal string Text
        {
            get
            {
                return ParameterName is null
                    ? Description
                    : $"<paramref name=\"{ParameterName}\"/> {Description}";
            }
        }

        /// <summary>
        /// Creates XML documentation for this entry.
        /// </summary>
        /// <returns>The generated documentation text.</returns>
        internal string ToDocumentation()
        {
            var exceptionType = ExceptionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"/// <exception cref=\"{exceptionType}\">\n/// {ToXmlText()}\n/// </exception>\n";
        }

        private string ToXmlText()
        {
            return ParameterName is null
                ? Escape(Description)
                : $"<paramref name=\"{ParameterName}\"/> {Escape(Description)}";
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}