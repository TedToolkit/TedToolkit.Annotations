// -----------------------------------------------------------------------
// <copyright file="ConstAttributeTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class ConstAttributeTests
{
    /// <summary>
    /// 验证默认构造的 Const 特性保护全部三十二个深度。
    /// </summary>
    [Test]
    public async Task Should_protect_all_32_depths_when_depths_are_omitted()
    {
        var attribute = new ConstAttribute();

        await Assert.That(attribute.Depths).IsEqualTo(ConstDepth.ALL);
        await Assert.That((uint)attribute.Depths).IsEqualTo(uint.MaxValue);
    }

    /// <summary>
    /// 验证深度枚举的每个成员都映射到唯一的三十二位掩码位。
    /// </summary>
    [Test]
    public async Task Should_map_each_depth_to_its_corresponding_bit()
    {
        for (var depth = 0; depth < 32; depth++)
        {
            var value = Enum.Parse<ConstDepth>($"DEPTH{depth}");

            await Assert.That((uint)value).IsEqualTo(1U << depth);
        }
    }

    /// <summary>
    /// 验证 Const 特性可显式限制受保护的对象图深度。
    /// </summary>
    [Test]
    public async Task Should_preserve_explicit_depth_mask_when_constructed()
    {
        var expected = ConstDepth.DEPTH0 | ConstDepth.DEPTH2 | ConstDepth.DEPTH31;
        var attribute = new ConstAttribute(expected);

        await Assert.That(attribute.Depths).IsEqualTo(expected);
    }

    /// <summary>
    /// 验证大于等于深度掩码会保护其起始深度及全部更深层级。
    /// </summary>
    [Test]
    [Arguments(ConstDepth.DEPTH0_OR_GREATER, uint.MaxValue)]
    [Arguments(ConstDepth.DEPTH1_OR_GREATER, 0xFFFFFFFEU)]
    [Arguments(ConstDepth.DEPTH16_OR_GREATER, 0xFFFF0000U)]
    [Arguments(ConstDepth.DEPTH31_OR_GREATER, 0x80000000U)]
    public async Task Should_protect_starting_depth_and_all_greater_depths(
        ConstDepth depths,
        uint expectedMask)
    {
        await Assert.That((uint)depths).IsEqualTo(expectedMask);
    }

    /// <summary>
    /// 验证每个大于等于和小于等于深度掩码均覆盖正确的连续位范围。
    /// </summary>
    [Test]
    public async Task Should_map_all_contiguous_depth_masks_to_expected_bits()
    {
        for (var depth = 0; depth < 32; depth++)
        {
            var greater = Enum.Parse<ConstDepth>($"DEPTH{depth}_OR_GREATER");
            var lower = Enum.Parse<ConstDepth>($"DEPTH{depth}_OR_LOWER");
            var expectedLower = depth == 31 ? uint.MaxValue : (1U << (depth + 1)) - 1U;

            await Assert.That((uint)greater).IsEqualTo(uint.MaxValue << depth);
            await Assert.That((uint)lower).IsEqualTo(expectedLower);
        }
    }
}