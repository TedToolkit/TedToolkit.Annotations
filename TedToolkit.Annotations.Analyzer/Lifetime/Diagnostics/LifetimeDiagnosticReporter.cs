// -----------------------------------------------------------------------
// <copyright file="LifetimeDiagnosticReporter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Annotations.Analyzer.Lifetime.Model;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Diagnostics;

/// <summary>
/// Maps lifetime transition results to diagnostics.
/// </summary>
/// <param name="reportDiagnostic">The diagnostic sink.</param>
internal sealed class LifetimeDiagnosticReporter(Action<Diagnostic> reportDiagnostic)
{
    /// <summary>
    /// Reports invalid resource use.
    /// </summary>
    /// <param name="result">The abstract state encountered by the use.</param>
    /// <param name="location">The source location of the use.</param>
    /// <param name="resourceName">The user-facing resource name.</param>
    internal void ReportUse(LifetimeTransitionResultType result, Location location, string resourceName)
    {
        Report(
            result,
            location,
            resourceName,
            DisposableLifetimeDiagnostics.UseAfterDispose,
            DisposableLifetimeDiagnostics.UseAfterTransfer);
    }

    /// <summary>
    /// Reports invalid resource disposal.
    /// </summary>
    /// <param name="result">The abstract state encountered by the disposal.</param>
    /// <param name="location">The source location of the disposal.</param>
    /// <param name="resourceName">The user-facing resource name.</param>
    internal void ReportDispose(LifetimeTransitionResultType result, Location location, string resourceName)
    {
        var rule = result switch
        {
            LifetimeTransitionResultType.BORROWED => DisposableLifetimeDiagnostics.DisposeBorrowedProperty,
            LifetimeTransitionResultType.ALREADY_DISPOSED => DisposableLifetimeDiagnostics.DoubleDispose,
            LifetimeTransitionResultType.TRANSFERRED => DisposableLifetimeDiagnostics.UseAfterTransfer,
            _ => null,
        };
        if (rule is null)
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(rule, location, resourceName));
    }

    /// <summary>
    /// Reports invalid ownership transfer.
    /// </summary>
    /// <param name="result">The abstract state encountered by the transfer.</param>
    /// <param name="location">The source location of the transfer.</param>
    /// <param name="resourceName">The user-facing resource name.</param>
    internal void ReportTransfer(LifetimeTransitionResultType result, Location location, string resourceName)
    {
        Report(
            result,
            location,
            resourceName,
            DisposableLifetimeDiagnostics.UseAfterDispose,
            DisposableLifetimeDiagnostics.UseAfterTransfer);
    }

    /// <summary>
    /// Reports an unreleased resource.
    /// </summary>
    /// <param name="resource">The owned resource that remains live at an exit.</param>
    internal void ReportUndisposed(LifetimeObject resource)
    {
        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.UndisposedResource,
            resource.CreationLocation,
            resource.Symbol.Name));
    }

    /// <summary>
    /// Reports a resource overwrite.
    /// </summary>
    /// <param name="resource">The live owned resource being replaced.</param>
    /// <param name="location">The overwrite location.</param>
    internal void ReportOverwrite(LifetimeObject resource, Location location)
    {
        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.OwnedResourceOverwritten,
            location,
            resource.Symbol.Name));
    }

    /// <summary>
    /// Reports disposal of a borrowed resource.
    /// </summary>
    /// <param name="location">The disposal location.</param>
    /// <param name="resourceName">The borrowed resource name.</param>
    internal void ReportBorrowedDisposal(Location location, string resourceName)
    {
        reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DisposeBorrowedProperty, location, resourceName));
    }

    /// <summary>
    /// Reports returning a disposed resource.
    /// </summary>
    /// <param name="location">The return location.</param>
    /// <param name="resourceName">The disposed resource name.</param>
    internal void ReportDisposedReturn(Location location, string resourceName)
    {
        reportDiagnostic(Diagnostic.Create(DisposableLifetimeDiagnostics.DisposedResourceReturned, location, resourceName));
    }

    /// <summary>
    /// Reports a callback that can outlive its resource.
    /// </summary>
    /// <param name="location">The callback argument location.</param>
    /// <param name="callbackLifetime">The localized retention category.</param>
    /// <param name="resourceName">The captured resource name.</param>
    internal void ReportCallbackOutlivesResource(Location location, LocalizableString callbackLifetime, string resourceName)
    {
        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.CallbackOutlivesResource,
            location,
            callbackLifetime,
            resourceName));
    }

    /// <summary>
    /// Reports an asynchronous release whose result is not observed.
    /// </summary>
    /// <param name="location">The unobserved invocation location.</param>
    /// <param name="resourceName">The asynchronously disposed resource name.</param>
    internal void ReportUnobservedAsyncDispose(Location location, string resourceName)
    {
        reportDiagnostic(Diagnostic.Create(
            DisposableLifetimeDiagnostics.UnobservedAsyncDispose,
            location,
            resourceName));
    }

    private void Report(
        LifetimeTransitionResultType result,
        Location location,
        string resourceName,
        DiagnosticDescriptor disposedRule,
        DiagnosticDescriptor transferredRule)
    {
        var rule = result switch
        {
            LifetimeTransitionResultType.DISPOSED => disposedRule,
            LifetimeTransitionResultType.TRANSFERRED => transferredRule,
            _ => null,
        };
        if (rule is null)
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(rule, location, resourceName));
    }
}