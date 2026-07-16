// -----------------------------------------------------------------------
// <copyright file="BoxingTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Boxing;

namespace TedToolkit.Annotations.Boxing.Tests;

/// <summary>
/// Contains tests for boxing.
/// </summary>
internal sealed class BoxingTests
{
    /// <summary>
    /// Verifies that should box value as requested interface.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_box_value_as_requested_interface()
    {
        const int value = 42;

        var boxed = global::TedToolkit.Annotations.Boxing.Boxer.Box(value);
        var comparable = global::TedToolkit.Annotations.Boxing.Boxer.Box<IComparable, int>(value);

        await Assert.That(boxed).IsTypeOf<int>();
        await Assert.That(comparable.CompareTo(value)).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that should propagate non null state from nullable input.
    /// </summary>
    /// <param name="value">The nullable input value.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(42)]
    public async Task Should_propagate_non_null_state_from_nullable_input(int? value)
    {
        if (value is null)
        {
            return;
        }

        var comparable = global::TedToolkit.Annotations.Boxing.Boxer.Box<IComparable, int>(value);
        await Assert.That(comparable.CompareTo(value.Value)).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that should preserve boxing semantics.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_boxing_semantics()
    {
        int? present = 42;
        int? missing = null;

        await Assert.That(global::TedToolkit.Annotations.Boxing.Boxer.Box(present)).IsTypeOf<int>();
        await Assert.That(global::TedToolkit.Annotations.Boxing.Boxer.Box(missing)).IsNull();
    }
}