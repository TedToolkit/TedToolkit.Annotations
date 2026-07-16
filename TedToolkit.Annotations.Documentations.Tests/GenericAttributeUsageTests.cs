// -----------------------------------------------------------------------
// <copyright file="GenericAttributeUsageTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

/// <summary>
/// Contains regression tests for generic documentation attribute usage.
/// </summary>
internal sealed class GenericAttributeUsageTests
{
    /// <summary>
    /// 验证泛型行为用例特性继承拆分前的基类契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_inherit_pre_split_behavior_case_contract()
    {
        var attributeType = typeof(BehaviorCaseAttribute<ArgumentException>);
        var usage = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: true)
            .Cast<AttributeUsageAttribute>()
            .Single();

        await Assert.That(attributeType.BaseType).IsEqualTo(typeof(BehaviorCaseAttribute));
        await Assert.That(usage.ValidOn).IsEqualTo(AttributeTargets.Constructor | AttributeTargets.Method);
        await Assert.That(usage.AllowMultiple).IsTrue();
        await Assert.That(usage.Inherited).IsFalse();
    }

    /// <summary>
    /// 验证泛型前置条件特性继承拆分前的基类契约。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_inherit_pre_split_precondition_contract()
    {
        var attributeType = typeof(PreconditionAttribute<ArgumentException>);
        var usage = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: true)
            .Cast<AttributeUsageAttribute>()
            .Single();

        await Assert.That(attributeType.BaseType).IsEqualTo(typeof(PreconditionAttribute));
        await Assert.That(usage.ValidOn).IsEqualTo(
            AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter);
        await Assert.That(usage.AllowMultiple).IsTrue();
        await Assert.That(usage.Inherited).IsFalse();
    }
}