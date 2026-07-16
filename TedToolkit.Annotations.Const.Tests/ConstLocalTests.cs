// -----------------------------------------------------------------------
// <copyright file="ConstLocalTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Const;

namespace TedToolkit.Annotations.Const.Tests;

/// <summary>
/// Contains tests for const local.
/// </summary>
internal sealed class ConstLocalTests
{
    /// <summary>
    /// Verifies that should preserve storage identity for ref local.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_storage_identity_for_ref_local()
    {
        var value = 1;

        ref var local = ref global::TedToolkit.Annotations.Const.AsConst.Local(ref value);
        local = 2;

        await Assert.That(value).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that should preserve value and reference identity.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_value_and_reference_identity()
    {
        var value = new object();
        var local = global::TedToolkit.Annotations.Const.AsConst.Local(value, ConstDepth.DEPTH1_OR_GREATER);
        var number = 1;
        ref var alias = ref global::TedToolkit.Annotations.Const.AsConst.Local(ref number);
        alias = 2;

        await Assert.That(local).IsSameReferenceAs(value);
        await Assert.That(number).IsEqualTo(2);
    }
}