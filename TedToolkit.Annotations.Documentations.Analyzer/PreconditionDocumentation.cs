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
        if (attributeType is null || attribute.ConstructorArguments.FirstOrDefault().Value is not string description)
        {
            entry = default;
            return false;
        }

        ITypeSymbol? exceptionType = null;
        if (attributeType.ToDisplayString() == PRECONDITION_ATTRIBUTE_NAME && attribute.ConstructorArguments.Length > 1)
        {
            exceptionType = attribute.ConstructorArguments[1].Value as ITypeSymbol;
        }
        else if (attributeType.OriginalDefinition.ToDisplayString() == GENERIC_PRECONDITION_ATTRIBUTE_NAME)
        {
            exceptionType = attributeType.TypeArguments[0];
        }

        if (exceptionType is null)
        {
            entry = default;
            return false;
        }

        entry = new(exceptionType, parameterName, description);
        return true;
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