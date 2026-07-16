// -----------------------------------------------------------------------
// <copyright file="Boxer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.Annotations.Boxing;

/// <summary>
/// Makes a boxing allocation explicit to source readers and analyzers.
/// </summary>
public static class Boxer
{
    /// <summary>
    /// Boxes a value as <see cref="object"/>.
    /// </summary>
    /// <typeparam name="T">The value type to box.</typeparam>
    /// <param name="value">The value to box.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Box<T>(T value)
        where T : struct
    {
        return value;
    }

    /// <summary>
    /// Boxes a nullable value or returns <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The underlying value type to box.</typeparam>
    /// <param name="value">The nullable value to box.</param>
    /// <returns>The boxed underlying value, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? Box<T>(T? value)
        where T : struct
    {
        return value;
    }

    /// <summary>
    /// Boxes a value and exposes it as the requested reference type.
    /// </summary>
    /// <typeparam name="TTarget">The reference type exposed by the boxed value.</typeparam>
    /// <typeparam name="TValue">The value type to box.</typeparam>
    /// <param name="value">The value to box.</param>
    /// <returns>The boxed value viewed as <typeparamref name="TTarget"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget Box<TTarget, TValue>(TValue value)
        where TTarget : class
        where TValue : struct
    {
        return (TTarget)(object)value;
    }

    /// <summary>
    /// Boxes a nullable value and exposes it as the requested reference type.
    /// </summary>
    /// <typeparam name="TTarget">The reference type exposed by the boxed value.</typeparam>
    /// <typeparam name="TValue">The underlying value type to box.</typeparam>
    /// <param name="value">The nullable value to box.</param>
    /// <returns>The boxed value viewed as <typeparamref name="TTarget"/>, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(value))]
    public static TTarget? Box<TTarget, TValue>(TValue? value)
        where TTarget : class
        where TValue : struct
    {
        return (TTarget?)(object?)value;
    }
}