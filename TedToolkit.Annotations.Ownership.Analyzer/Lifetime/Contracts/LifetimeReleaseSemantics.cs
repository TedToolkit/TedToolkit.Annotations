// -----------------------------------------------------------------------
// <copyright file="LifetimeReleaseSemantics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Identifies values whose lifetime obligation is discharged by an operation.
/// </summary>
internal static class LifetimeReleaseSemantics
{
    /// <summary>
    /// Gets values released by an invocation.
    /// </summary>
    /// <param name="operation">The invocation that may release or transfer a resource.</param>
    /// <param name="contract">The known disposal methods.</param>
    /// <returns>Receiver and argument expressions whose ownership obligation is discharged.</returns>
    internal static IEnumerable<IOperation> GetReleasedValues(
        IInvocationOperation operation,
        DisposableContract contract)
    {
        if (contract.IsSynchronousRelease(operation.TargetMethod)
            || contract.IsAsynchronousRelease(operation.TargetMethod))
        {
            if (operation.Instance is { } instance)
            {
                yield return instance;
            }

            yield break;
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is not null && LifetimeOwnershipSemantics.IsTransferredInput(argument.Parameter))
            {
                yield return argument.Value;
            }
        }
    }

    /// <summary>
    /// Gets the value released by assigning it to an ownership-transferring property.
    /// </summary>
    /// <param name="operation">The assignment to inspect.</param>
    /// <param name="value">Receives the assigned resource when the property setter takes ownership.</param>
    /// <returns><see langword="true"/> when the assignment transfers ownership.</returns>
    internal static bool TryGetReleasedValue(ISimpleAssignmentOperation operation, out IOperation? value)
    {
        if (operation.Target is IPropertyReferenceOperation property
            && LifetimeOwnershipSemantics.IsTransferredInput(property.Property))
        {
            value = operation.Value;
            return true;
        }

        value = null;
        return false;
    }
}