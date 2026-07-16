// -----------------------------------------------------------------------
// <copyright file="AsConst.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace TedToolkit.Annotations.Const;

/// <summary>
/// Makes a local const contract explicit to source readers and analyzers.
/// </summary>
public static class AsConst
{
    /// <summary>
    /// Returns <paramref name="value"/> unchanged while applying a const contract to the receiving local variable.
    /// </summary>
    /// <typeparam name="T">The local variable type.</typeparam>
    /// <param name="value">The value assigned to the local variable.</param>
    /// <param name="depths">The object-graph depths protected from mutation.</param>
    /// <returns><paramref name="value"/> unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Local<T>(T value, ConstDepth depths = ConstDepth.ALL)
    {
        _ = depths;
        return value;
    }

    /// <summary>
    /// Returns a reference to <paramref name="value"/> while applying a const contract to the receiving ref local.
    /// </summary>
    /// <typeparam name="T">The ref local type.</typeparam>
    /// <param name="value">The referenced value assigned to the ref local.</param>
    /// <param name="depths">The object-graph depths protected from mutation.</param>
    /// <returns>A reference to <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Local<T>(ref T value, ConstDepth depths = ConstDepth.ALL)
    {
        _ = depths;
        return ref value;
    }
}