// -----------------------------------------------------------------------
// <copyright file="DocumentationAttributeContractTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Documentations;

namespace TedToolkit.Annotations.Analyzer.Tests;

/// <summary>
/// Contains contract tests for categorized documentation attributes.
/// </summary>
internal sealed class DocumentationAttributeContractTests
{
    /// <summary>
    /// 验证线程安全特性保留兼容构造函数，并公开分类构造函数的值。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_and_expose_thread_safety_contracts()
    {
        var legacy = new ThreadSafetyAttribute("Calls are serialized by the owner.");
        var categorized = new ThreadSafetyAttribute(
            ThreadSafetyKind.EXTERNAL_SYNCHRONIZATION_REQUIRED,
            "Callers synchronize access with the owner's gate.");

        await Assert.That(legacy.Kind).IsNull();
        await Assert.That(legacy.Description).IsEqualTo("Calls are serialized by the owner.");
        await Assert.That(categorized.Kind).IsEqualTo(ThreadSafetyKind.EXTERNAL_SYNCHRONIZATION_REQUIRED);
        await Assert.That(categorized.Description).IsEqualTo("Callers synchronize access with the owner's gate.");
    }

    /// <summary>
    /// 验证副作用特性保留兼容构造函数，并公开分类构造函数的值。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_and_expose_side_effect_contracts()
    {
        var legacy = new SideEffectAttribute("Refreshes the cache.");
        var categorized = new SideEffectAttribute(
            SideEffectKind.NOTIFICATION_PUBLICATION,
            "Raises PackageChanged after committing the replacement.");

        await Assert.That(legacy.Kind).IsNull();
        await Assert.That(legacy.Description).IsEqualTo("Refreshes the cache.");
        await Assert.That(categorized.Kind).IsEqualTo(SideEffectKind.NOTIFICATION_PUBLICATION);
        await Assert.That(categorized.Description).IsEqualTo("Raises PackageChanged after committing the replacement.");
    }

    /// <summary>
    /// 验证阻塞特性保留兼容构造函数，并使用与副作用一致的输入输出类别名称。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_preserve_and_expose_may_block_contracts()
    {
        var legacy = new MayBlockAttribute("Waits for the stream to flush.");
        var categorized = new MayBlockAttribute(
            MayBlockKind.INPUT_OUTPUT,
            "Synchronously flushes the output stream.");

        await Assert.That(legacy.Kind).IsNull();
        await Assert.That(legacy.Description).IsEqualTo("Waits for the stream to flush.");
        await Assert.That(categorized.Kind).IsEqualTo(MayBlockKind.INPUT_OUTPUT);
        await Assert.That(categorized.Description).IsEqualTo("Synchronously flushes the output stream.");
    }

    /// <summary>
    /// 验证文档特性按契约、运行特性、规格与设计理由分组。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_group_documentation_attributes_by_intent()
    {
        await Assert.That(typeof(PreconditionAttribute).BaseType).IsEqualTo(typeof(ContractAttribute));
        await Assert.That(typeof(ThreadSafetyAttribute).BaseType).IsEqualTo(typeof(OperationalAttribute));
        await Assert.That(typeof(BehaviorCaseAttribute).BaseType).IsEqualTo(typeof(SpecificationAttribute));
        await Assert.That(typeof(StateTransitionAttribute).BaseType).IsEqualTo(typeof(SpecificationAttribute));
        await Assert.That(typeof(DesignDecisionAttribute).BaseType).IsEqualTo(typeof(RationaleAttribute));
        await Assert.That(typeof(DesignConstraintAttribute).BaseType).IsEqualTo(typeof(RationaleAttribute));
    }

    /// <summary>
    /// 验证状态转换和设计理由特性公开其结构化信息。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_expose_specification_and_rationale_contracts()
    {
        var transition = new StateTransitionAttribute("Created", "Running", "The operation is accepted.");
        var decision = new DesignDecisionAttribute(
            "ADR-012",
            "Use immutable snapshots for reads.",
            "Readers must not observe a partially updated cache.",
            "Locking every read was rejected because it would reduce throughput.");
        var constraint = new DesignConstraintAttribute(
            "Do not invoke user callbacks while holding _gate.",
            "A synchronous callback can re-enter the cache and deadlock.");

        await Assert.That(transition.FromState).IsEqualTo("Created");
        await Assert.That(transition.ToState).IsEqualTo("Running");
        await Assert.That(transition.Condition).IsEqualTo("The operation is accepted.");
        await Assert.That(decision.Id).IsEqualTo("ADR-012");
        await Assert.That(decision.Alternatives).IsEqualTo("Locking every read was rejected because it would reduce throughput.");
        await Assert.That(constraint.Constraint).IsEqualTo("Do not invoke user callbacks while holding _gate.");
    }
}