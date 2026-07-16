// -----------------------------------------------------------------------
// <copyright file="ConstDepth.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Const;

/// <summary>
/// Specifies the object-graph depths that a <see cref="ConstAttribute"/> contract protects from mutation.
/// </summary>
/// <remarks>
/// <para>Each member maps directly to one bit of the underlying 32-bit <see cref="uint"/> value.</para>
/// <para>
/// On a parameter, <see cref="DEPTH0"/> protects the parameter variable itself and <see cref="DEPTH1"/> through
/// <see cref="DEPTH31"/> protect successive member depths. On a method, <see cref="DEPTH0"/> through
/// <see cref="DEPTH31"/> protect successive member depths of the current instance.
/// </para>
/// </remarks>
[Flags]
#pragma warning disable CA1028 // uint is required to represent all 32 depth flags without a negative public value.
public enum ConstDepth : uint
{
    /// <summary>
    /// No depth is protected.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Protects depth 0.
    /// </summary>
    DEPTH0 = 1U << 0,

    /// <summary>
    /// Protects depth 0.
    /// </summary>
    DEPTH0_OR_LOWER = DEPTH0,

    /// <summary>
    /// Protects depth 1.
    /// </summary>
    DEPTH1 = 1U << 1,

    /// <summary>
    /// Protects depths 0 through 1.
    /// </summary>
    DEPTH1_OR_LOWER = DEPTH0
        | DEPTH1,

    /// <summary>
    /// Protects depth 2.
    /// </summary>
    DEPTH2 = 1U << 2,

    /// <summary>
    /// Protects depths 0 through 2.
    /// </summary>
    DEPTH2_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2,

    /// <summary>
    /// Protects depth 3.
    /// </summary>
    DEPTH3 = 1U << 3,

    /// <summary>
    /// Protects depths 0 through 3.
    /// </summary>
    DEPTH3_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3,

    /// <summary>
    /// Protects depth 4.
    /// </summary>
    DEPTH4 = 1U << 4,

    /// <summary>
    /// Protects depths 0 through 4.
    /// </summary>
    DEPTH4_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4,

    /// <summary>
    /// Protects depth 5.
    /// </summary>
    DEPTH5 = 1U << 5,

    /// <summary>
    /// Protects depths 0 through 5.
    /// </summary>
    DEPTH5_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5,

    /// <summary>
    /// Protects depth 6.
    /// </summary>
    DEPTH6 = 1U << 6,

    /// <summary>
    /// Protects depths 0 through 6.
    /// </summary>
    DEPTH6_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6,

    /// <summary>
    /// Protects depth 7.
    /// </summary>
    DEPTH7 = 1U << 7,

    /// <summary>
    /// Protects depths 0 through 7.
    /// </summary>
    DEPTH7_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7,

    /// <summary>
    /// Protects depth 8.
    /// </summary>
    DEPTH8 = 1U << 8,

    /// <summary>
    /// Protects depths 0 through 8.
    /// </summary>
    DEPTH8_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8,

    /// <summary>
    /// Protects depth 9.
    /// </summary>
    DEPTH9 = 1U << 9,

    /// <summary>
    /// Protects depths 0 through 9.
    /// </summary>
    DEPTH9_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9,

    /// <summary>
    /// Protects depth 10.
    /// </summary>
    DEPTH10 = 1U << 10,

    /// <summary>
    /// Protects depths 0 through 10.
    /// </summary>
    DEPTH10_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10,

    /// <summary>
    /// Protects depth 11.
    /// </summary>
    DEPTH11 = 1U << 11,

    /// <summary>
    /// Protects depths 0 through 11.
    /// </summary>
    DEPTH11_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11,

    /// <summary>
    /// Protects depth 12.
    /// </summary>
    DEPTH12 = 1U << 12,

    /// <summary>
    /// Protects depths 0 through 12.
    /// </summary>
    DEPTH12_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12,

    /// <summary>
    /// Protects depth 13.
    /// </summary>
    DEPTH13 = 1U << 13,

    /// <summary>
    /// Protects depths 0 through 13.
    /// </summary>
    DEPTH13_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13,

    /// <summary>
    /// Protects depth 14.
    /// </summary>
    DEPTH14 = 1U << 14,

    /// <summary>
    /// Protects depths 0 through 14.
    /// </summary>
    DEPTH14_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14,

    /// <summary>
    /// Protects depth 15.
    /// </summary>
    DEPTH15 = 1U << 15,

    /// <summary>
    /// Protects depths 0 through 15.
    /// </summary>
    DEPTH15_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15,

    /// <summary>
    /// Protects depth 16.
    /// </summary>
    DEPTH16 = 1U << 16,

    /// <summary>
    /// Protects depths 0 through 16.
    /// </summary>
    DEPTH16_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16,

    /// <summary>
    /// Protects depth 17.
    /// </summary>
    DEPTH17 = 1U << 17,

    /// <summary>
    /// Protects depths 0 through 17.
    /// </summary>
    DEPTH17_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17,

    /// <summary>
    /// Protects depth 18.
    /// </summary>
    DEPTH18 = 1U << 18,

    /// <summary>
    /// Protects depths 0 through 18.
    /// </summary>
    DEPTH18_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18,

    /// <summary>
    /// Protects depth 19.
    /// </summary>
    DEPTH19 = 1U << 19,

    /// <summary>
    /// Protects depths 0 through 19.
    /// </summary>
    DEPTH19_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19,

    /// <summary>
    /// Protects depth 20.
    /// </summary>
    DEPTH20 = 1U << 20,

    /// <summary>
    /// Protects depths 0 through 20.
    /// </summary>
    DEPTH20_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20,

    /// <summary>
    /// Protects depth 21.
    /// </summary>
    DEPTH21 = 1U << 21,

    /// <summary>
    /// Protects depths 0 through 21.
    /// </summary>
    DEPTH21_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21,

    /// <summary>
    /// Protects depth 22.
    /// </summary>
    DEPTH22 = 1U << 22,

    /// <summary>
    /// Protects depths 0 through 22.
    /// </summary>
    DEPTH22_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22,

    /// <summary>
    /// Protects depth 23.
    /// </summary>
    DEPTH23 = 1U << 23,

    /// <summary>
    /// Protects depths 0 through 23.
    /// </summary>
    DEPTH23_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23,

    /// <summary>
    /// Protects depth 24.
    /// </summary>
    DEPTH24 = 1U << 24,

    /// <summary>
    /// Protects depths 0 through 24.
    /// </summary>
    DEPTH24_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24,

    /// <summary>
    /// Protects depth 25.
    /// </summary>
    DEPTH25 = 1U << 25,

    /// <summary>
    /// Protects depths 0 through 25.
    /// </summary>
    DEPTH25_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25,

    /// <summary>
    /// Protects depth 26.
    /// </summary>
    DEPTH26 = 1U << 26,

    /// <summary>
    /// Protects depths 0 through 26.
    /// </summary>
    DEPTH26_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26,

    /// <summary>
    /// Protects depth 27.
    /// </summary>
    DEPTH27 = 1U << 27,

    /// <summary>
    /// Protects depths 0 through 27.
    /// </summary>
    DEPTH27_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27,

    /// <summary>
    /// Protects depth 28.
    /// </summary>
    DEPTH28 = 1U << 28,

    /// <summary>
    /// Protects depths 0 through 28.
    /// </summary>
    DEPTH28_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28,

    /// <summary>
    /// Protects depth 29.
    /// </summary>
    DEPTH29 = 1U << 29,

    /// <summary>
    /// Protects depths 0 through 29.
    /// </summary>
    DEPTH29_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29,

    /// <summary>
    /// Protects depth 30.
    /// </summary>
    DEPTH30 = 1U << 30,

    /// <summary>
    /// Protects depths 0 through 30.
    /// </summary>
    DEPTH30_OR_LOWER = DEPTH0
        | DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30,

    /// <summary>
    /// Protects depth 31.
    /// </summary>
    DEPTH31 = 1U << 31,

    /// <summary>
    /// Protects depth 31.
    /// </summary>
    DEPTH31_OR_GREATER = DEPTH31,

    /// <summary>
    /// Protects depths 30 through 31.
    /// </summary>
    DEPTH30_OR_GREATER = DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 29 through 31.
    /// </summary>
    DEPTH29_OR_GREATER = DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 28 through 31.
    /// </summary>
    DEPTH28_OR_GREATER = DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 27 through 31.
    /// </summary>
    DEPTH27_OR_GREATER = DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 26 through 31.
    /// </summary>
    DEPTH26_OR_GREATER = DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 25 through 31.
    /// </summary>
    DEPTH25_OR_GREATER = DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 24 through 31.
    /// </summary>
    DEPTH24_OR_GREATER = DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 23 through 31.
    /// </summary>
    DEPTH23_OR_GREATER = DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 22 through 31.
    /// </summary>
    DEPTH22_OR_GREATER = DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 21 through 31.
    /// </summary>
    DEPTH21_OR_GREATER = DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 20 through 31.
    /// </summary>
    DEPTH20_OR_GREATER = DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 19 through 31.
    /// </summary>
    DEPTH19_OR_GREATER = DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 18 through 31.
    /// </summary>
    DEPTH18_OR_GREATER = DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 17 through 31.
    /// </summary>
    DEPTH17_OR_GREATER = DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 16 through 31.
    /// </summary>
    DEPTH16_OR_GREATER = DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 15 through 31.
    /// </summary>
    DEPTH15_OR_GREATER = DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 14 through 31.
    /// </summary>
    DEPTH14_OR_GREATER = DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 13 through 31.
    /// </summary>
    DEPTH13_OR_GREATER = DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 12 through 31.
    /// </summary>
    DEPTH12_OR_GREATER = DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 11 through 31.
    /// </summary>
    DEPTH11_OR_GREATER = DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 10 through 31.
    /// </summary>
    DEPTH10_OR_GREATER = DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 9 through 31.
    /// </summary>
    DEPTH9_OR_GREATER = DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 8 through 31.
    /// </summary>
    DEPTH8_OR_GREATER = DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 7 through 31.
    /// </summary>
    DEPTH7_OR_GREATER = DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 6 through 31.
    /// </summary>
    DEPTH6_OR_GREATER = DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 5 through 31.
    /// </summary>
    DEPTH5_OR_GREATER = DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 4 through 31.
    /// </summary>
    DEPTH4_OR_GREATER = DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 3 through 31.
    /// </summary>
    DEPTH3_OR_GREATER = DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 2 through 31.
    /// </summary>
    DEPTH2_OR_GREATER = DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects depths 1 through 31.
    /// </summary>
    DEPTH1_OR_GREATER = DEPTH1
        | DEPTH2
        | DEPTH3
        | DEPTH4
        | DEPTH5
        | DEPTH6
        | DEPTH7
        | DEPTH8
        | DEPTH9
        | DEPTH10
        | DEPTH11
        | DEPTH12
        | DEPTH13
        | DEPTH14
        | DEPTH15
        | DEPTH16
        | DEPTH17
        | DEPTH18
        | DEPTH19
        | DEPTH20
        | DEPTH21
        | DEPTH22
        | DEPTH23
        | DEPTH24
        | DEPTH25
        | DEPTH26
        | DEPTH27
        | DEPTH28
        | DEPTH29
        | DEPTH30
        | DEPTH31,

    /// <summary>
    /// Protects all 32 depths.
    /// </summary>
    ALL = DEPTH0
          | DEPTH1
          | DEPTH2
          | DEPTH3
          | DEPTH4
          | DEPTH5
          | DEPTH6
          | DEPTH7
          | DEPTH8
          | DEPTH9
          | DEPTH10
          | DEPTH11
          | DEPTH12
          | DEPTH13
          | DEPTH14
          | DEPTH15
          | DEPTH16
          | DEPTH17
          | DEPTH18
          | DEPTH19
          | DEPTH20
          | DEPTH21
          | DEPTH22
          | DEPTH23
          | DEPTH24
          | DEPTH25
          | DEPTH26
          | DEPTH27
          | DEPTH28
          | DEPTH29
          | DEPTH30
          | DEPTH31,

    /// <summary>
    /// Protects depths 0 through 31.
    /// </summary>
    DEPTH0_OR_GREATER = ALL,

    /// <summary>
    /// Protects depths 0 through 31.
    /// </summary>
    DEPTH31_OR_LOWER = ALL,
}
#pragma warning restore CA1028