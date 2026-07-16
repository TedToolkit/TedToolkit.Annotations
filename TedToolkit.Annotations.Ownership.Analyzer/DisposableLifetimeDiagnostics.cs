// -----------------------------------------------------------------------
// <copyright file="DisposableLifetimeDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Defines diagnostic descriptors for disposable-resource lifetime analysis.
/// </summary>
internal static class DisposableLifetimeDiagnostics
{
    private const string DOUBLE_DISPOSE_ID = "TAO001";

    private const string USE_AFTER_DISPOSE_ID = "TAO002";

    private const string USE_AFTER_TRANSFER_ID = "TAO003";

    private const string UNDISPOSED_RESOURCE_ID = "TAO004";

    private const string CALLBACK_OUTLIVES_RESOURCE_ID = "TAO005";

    private const string DISPOSE_BORROWED_PROPERTY_ID = "TAO006";

    private const string DISPOSED_RESOURCE_RETURNED_ID = "TAO007";

    private const string OWNED_FIELD_REQUIRES_DISPOSABLE_TYPE_ID = "TAO008";

    private const string OWNED_FIELD_NOT_RELEASED_ID = "TAO009";

    private const string OWNERSHIP_TARGET_MUST_BE_DISPOSABLE_ID = "TAO010";

    private const string OWNED_RESOURCE_OVERWRITTEN_ID = "TAO011";

    private const string OWNED_PROPERTY_OVERWRITTEN_ID = "TAO012";

    private const string UNOBSERVED_ASYNC_DISPOSE_ID = "TAO013";

    private const string INVALID_OWNERSHIP_CONTRACT_ID = "TAO014";

    /// <summary>
    /// Reports a resource that is disposed more than once.
    /// </summary>
    internal static readonly DiagnosticDescriptor DoubleDispose = CreateDiagnostic(DOUBLE_DISPOSE_ID, DiagnosticSeverity.Error);

    /// <summary>
    /// Reports use of a disposed resource.
    /// </summary>
    internal static readonly DiagnosticDescriptor UseAfterDispose = CreateDiagnostic(USE_AFTER_DISPOSE_ID, DiagnosticSeverity.Error);

    /// <summary>
    /// Reports use of a resource after ownership transfer.
    /// </summary>
    internal static readonly DiagnosticDescriptor UseAfterTransfer = CreateDiagnostic(USE_AFTER_TRANSFER_ID, DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an owned resource that is not disposed.
    /// </summary>
    internal static readonly DiagnosticDescriptor UndisposedResource = CreateDiagnostic(UNDISPOSED_RESOURCE_ID, DiagnosticSeverity.Warning);

    /// <summary>
    /// Reports a callback that can outlive its resource.
    /// </summary>
    internal static readonly DiagnosticDescriptor CallbackOutlivesResource = CreateDiagnostic(
        CALLBACK_OUTLIVES_RESOURCE_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports disposal of a borrowed property.
    /// </summary>
    internal static readonly DiagnosticDescriptor DisposeBorrowedProperty = CreateDiagnostic(
        DISPOSE_BORROWED_PROPERTY_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports return of a disposed resource.
    /// </summary>
    internal static readonly DiagnosticDescriptor DisposedResourceReturned = CreateDiagnostic(
        DISPOSED_RESOURCE_RETURNED_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an owned field whose containing type is not disposable.
    /// </summary>
    internal static readonly DiagnosticDescriptor OwnedFieldRequiresDisposableType = CreateDiagnostic(
        OWNED_FIELD_REQUIRES_DISPOSABLE_TYPE_ID,
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Reports an owned field that is not released by its containing type.
    /// </summary>
    internal static readonly DiagnosticDescriptor OwnedFieldNotReleased = CreateDiagnostic(
        OWNED_FIELD_NOT_RELEASED_ID,
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Reports ownership annotations applied to a non-disposable type.
    /// </summary>
    internal static readonly DiagnosticDescriptor OwnershipTargetMustBeDisposable = CreateDiagnostic(
        OWNERSHIP_TARGET_MUST_BE_DISPOSABLE_ID,
        DiagnosticSeverity.Error);

    /// <summary>
    /// Reports an owned resource that is overwritten before being released.
    /// </summary>
    internal static readonly DiagnosticDescriptor OwnedResourceOverwritten = CreateDiagnostic(
        OWNED_RESOURCE_OVERWRITTEN_ID,
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Reports an owned property that may be overwritten before being released.
    /// </summary>
    internal static readonly DiagnosticDescriptor OwnedPropertyOverwritten = CreateDiagnostic(
        OWNED_PROPERTY_OVERWRITTEN_ID,
        DiagnosticSeverity.Info);

    /// <summary>
    /// Reports an asynchronous release whose result is neither awaited nor returned.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnobservedAsyncDispose = CreateDiagnostic(
        UNOBSERVED_ASYNC_DISPOSE_ID,
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Reports conflicting ownership annotations or an invalid ownership flow.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidOwnershipContract = CreateDiagnostic(
        INVALID_OWNERSHIP_CONTRACT_ID,
        DiagnosticSeverity.Error);

    private static DiagnosticDescriptor CreateDiagnostic(
        string id,
        DiagnosticSeverity defaultSeverity)
    {
        return new(
            id: id,
            title: DiagnosticResources.Get($"{id}Title"),
            messageFormat: DiagnosticResources.Get($"{id}Message"),
            category: "Lifetime",
            defaultSeverity: defaultSeverity,
            isEnabledByDefault: true);
    }
}