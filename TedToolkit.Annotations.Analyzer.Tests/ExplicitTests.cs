// -----------------------------------------------------------------------
// <copyright file="ExplicitTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

internal sealed class ExplicitTests
{
    /// <summary>
    /// 验证 Explicit.Const 的值重载返回原始对象且不产生运行时副作用。
    /// </summary>
    [Test]
    public async Task Should_preserve_value_identity_for_explicit_const()
    {
        var value = new object();

        var result = Explicit.Const(value, ConstDepth.DEPTH1_OR_GREATER);

        await Assert.That(result).IsSameReferenceAs(value);
    }

    /// <summary>
    /// 验证 Explicit.Const 的 ref 重载保持对原始存储位置的引用。
    /// </summary>
    [Test]
    public async Task Should_preserve_storage_identity_for_explicit_const_ref()
    {
        var value = 1;

        ref var result = ref Explicit.Const(ref value);
        result = 2;

        await Assert.That(value).IsEqualTo(2);
    }

    /// <summary>
    /// 验证 Explicit.Box 可将值显式装箱为 object 或指定接口。
    /// </summary>
    [Test]
    public async Task Should_box_value_as_object_or_requested_interface()
    {
        var value = 42;

        var boxed = Explicit.Box(value);
        var comparable = Explicit.Box<IComparable, int>(value);

        await Assert.That(boxed).IsTypeOf<int>();
        await Assert.That(comparable.CompareTo(value)).IsEqualTo(0);
    }

    /// <summary>
    /// 验证可空值类型有值时装箱基础值，无值时保持 null。
    /// </summary>
    [Test]
    public async Task Should_preserve_nullable_boxing_semantics()
    {
        int? present = 42;
        int? missing = null;

        var boxedPresent = Explicit.Box(present);
        var boxedMissing = Explicit.Box(missing);
        var comparableMissing = Explicit.Box<IComparable, int>(missing);

        await Assert.That(boxedPresent).IsTypeOf<int>();
        await Assert.That(boxedMissing).IsNull();
        await Assert.That(comparableMissing).IsNull();
    }

    /// <summary>
    /// 验证已知有值的 nullable 输入会使目标装箱返回值被识别为非空。
    /// </summary>
    [Test]
    public async Task Should_propagate_non_null_state_from_nullable_input()
    {
        int? value = 42;

        if (value is not null)
        {
            IComparable comparable = Explicit.Box<IComparable, int>(value);

            await Assert.That(comparable.CompareTo(value.Value)).IsEqualTo(0);
        }
    }
}