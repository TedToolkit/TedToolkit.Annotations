// -----------------------------------------------------------------------
// <copyright file="Explicit.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Makes otherwise implicit expression semantics explicit to readers and the bundled analyzers.
/// </summary>
public static class Explicit
{
    /// <summary>
    /// Returns <paramref name="value"/> unchanged while declaring a const contract for the receiving local variable.
    /// </summary>
    /// <typeparam name="T">The type of the local variable.</typeparam>
    /// <param name="value">The value assigned to the local variable.</param>
    /// <param name="depths">The depths protected from mutation. Defaults to all 32 depths.</param>
    /// <returns><paramref name="value"/> unchanged.</returns>
    /// <remarks>
    /// Use this method only in a local variable initializer, for example
    /// <c>var local = Explicit.Const(value, ConstDepth.DEPTH1_OR_GREATER);</c>.
    /// The depth argument must be a compile-time constant. The bundled analyzer applies the contract to
    /// <c>local</c>; this method has no runtime behavior.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Const<T>(T value, ConstDepth depths = ConstDepth.ALL)
    {
        _ = depths;
        return value;
    }

    /// <summary>
    /// Returns a reference to <paramref name="value"/> while declaring a const contract for the receiving ref local variable.
    /// </summary>
    /// <typeparam name="T">The type of the ref local variable.</typeparam>
    /// <param name="value">The value referenced by the ref local variable.</param>
    /// <param name="depths">The depths protected from mutation. Defaults to all 32 depths.</param>
    /// <returns>A reference to <paramref name="value"/>.</returns>
    /// <remarks>
    /// Use this method only in a ref local initializer, for example
    /// <c>ref var local = ref Explicit.Const(ref value, ConstDepth.DEPTH1_OR_GREATER);</c>.
    /// The depth argument must be a compile-time constant.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Const<T>(ref T value, ConstDepth depths = ConstDepth.ALL)
    {
        _ = depths;
        return ref value;
    }

    /// <summary>
    /// Explicitly boxes a value as <see cref="object"/>.
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
    /// Explicitly boxes a nullable value as <see cref="object"/> or returns <see langword="null"/> when it has no value.
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
    /// Explicitly boxes a value as a requested reference type, such as an interface implemented by the value type.
    /// </summary>
    /// <typeparam name="TTarget">The reference type exposed by the boxed value.</typeparam>
    /// <typeparam name="TValue">The value type to box.</typeparam>
    /// <param name="value">The value to box.</param>
    /// <returns>The boxed value viewed as <typeparamref name="TTarget"/>, or <see langword="null"/> when the input is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget Box<TTarget, TValue>(TValue value)
        where TTarget : class
        where TValue : struct
    {
        return (TTarget)(object)value;
    }

    /// <summary>
    /// Explicitly boxes a nullable value as a requested reference type.
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