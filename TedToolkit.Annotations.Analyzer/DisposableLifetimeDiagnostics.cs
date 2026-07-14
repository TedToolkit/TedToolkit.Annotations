// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

internal static class DisposableLifetimeDiagnostics
{
    private const string DOUBLE_DISPOSE_ID = "TTA001";
    private const string USE_AFTER_DISPOSE_ID = "TTA002";
    private const string USE_AFTER_TRANSFER_ID = "TTA003";
    private const string UNDISPOSED_RESOURCE_ID = "TTA004";
    private const string CALLBACK_OUTLIVES_RESOURCE_ID = "TTA005";
    private const string DISPOSE_BORROWED_PROPERTY_ID = "TTA006";
    private const string DISPOSED_RESOURCE_RETURNED_ID = "TTA007";

    internal static readonly DiagnosticDescriptor DoubleDispose = CreateDiagnostic(DOUBLE_DISPOSE_ID, DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UseAfterDispose = CreateDiagnostic(USE_AFTER_DISPOSE_ID, DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UseAfterTransfer = CreateDiagnostic(USE_AFTER_TRANSFER_ID, DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UndisposedResource = CreateDiagnostic(UNDISPOSED_RESOURCE_ID, DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor CallbackOutlivesResource = CreateDiagnostic(CALLBACK_OUTLIVES_RESOURCE_ID, DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor DisposeBorrowedProperty = CreateDiagnostic(DISPOSE_BORROWED_PROPERTY_ID, DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor DisposedResourceReturned = CreateDiagnostic(DISPOSED_RESOURCE_RETURNED_ID, DiagnosticSeverity.Error);

    private static DiagnosticDescriptor CreateDiagnostic(
        string id,
        DiagnosticSeverity defaultSeverity) =>
        new(
            id: id,
            title: DiagnosticResources.Get($"{id}Title"),
            messageFormat: DiagnosticResources.Get($"{id}Message"),
            category: "Lifetime",
            defaultSeverity: defaultSeverity,
            isEnabledByDefault: true);
}
