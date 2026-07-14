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

    internal static readonly DiagnosticDescriptor DoubleDispose = CreateDiagnostic(
        DOUBLE_DISPOSE_ID,
        "Disposable resource is disposed more than once",
        "The disposable resource '{0}' is disposed more than once",
        DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UseAfterDispose = CreateDiagnostic(
        USE_AFTER_DISPOSE_ID,
        "Disposed resource is used",
        "The disposable resource '{0}' is used after it was disposed",
        DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UseAfterTransfer = CreateDiagnostic(
        USE_AFTER_TRANSFER_ID,
        "Transferred resource is used by the previous owner",
        "The disposable resource '{0}' is used after its ownership was transferred",
        DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UndisposedResource = CreateDiagnostic(
        UNDISPOSED_RESOURCE_ID,
        "Locally owned disposable resource is not released",
        "The disposable resource '{0}' is neither disposed nor transferred to another owner",
        DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor CallbackOutlivesResource = CreateDiagnostic(
        CALLBACK_OUTLIVES_RESOURCE_ID,
        "Callback can outlive its captured resource",
        "The {0} callback captures '{1}', whose lifetime can end before the callback runs",
        DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor DisposeBorrowedProperty = CreateDiagnostic(
        DISPOSE_BORROWED_PROPERTY_ID,
        "Disposable property is borrowed",
        "The disposable property '{0}' is borrowed and must not be disposed by this caller",
        DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor DisposedResourceReturned = CreateDiagnostic(
        DISPOSED_RESOURCE_RETURNED_ID,
        "Disposed resource is returned",
        "The disposable resource '{0}' is returned after it was disposed or scheduled for disposal",
        DiagnosticSeverity.Error);

    private static DiagnosticDescriptor CreateDiagnostic(
        string id,
        string title,
        string messageFormat,
        DiagnosticSeverity defaultSeverity) =>
        new(
            id: id,
            title: title,
            messageFormat: messageFormat,
            category: "Lifetime",
            defaultSeverity: defaultSeverity,
            isEnabledByDefault: true);
}
