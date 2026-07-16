// -----------------------------------------------------------------------
// <copyright file="LifetimeObjectTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.Annotations.Analyzer.Lifetime.Model;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Model;

/// <summary>
/// Contains tests for lifetime object.
/// </summary>
internal sealed class LifetimeObjectTests
{
    /// <summary>
    /// 验证拥有的对象释放后不能再次使用或释放。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_reject_use_and_disposal_when_object_is_disposed()
    {
        var lifetimeObject = CreateLifetimeObject();

        var disposeResult = lifetimeObject.Dispose();

        await Assert.That(disposeResult).IsEqualTo(LifetimeTransitionResultType.SUCCEEDED);
        await Assert.That(lifetimeObject.Use()).IsEqualTo(LifetimeTransitionResultType.DISPOSED);
        await Assert.That(lifetimeObject.Dispose()).IsEqualTo(LifetimeTransitionResultType.ALREADY_DISPOSED);
    }

    /// <summary>
    /// 验证转移所有权后不能继续使用或再次转移对象。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_reject_use_and_transfer_when_object_is_transferred()
    {
        var lifetimeObject = CreateLifetimeObject();

        var transferResult = lifetimeObject.TransferOwnership();

        await Assert.That(transferResult).IsEqualTo(LifetimeTransitionResultType.SUCCEEDED);
        await Assert.That(lifetimeObject.Use()).IsEqualTo(LifetimeTransitionResultType.TRANSFERRED);
        await Assert.That(lifetimeObject.TransferOwnership())
            .IsEqualTo(LifetimeTransitionResultType.TRANSFERRED);
    }

    /// <summary>
    /// 验证覆盖拥有对象或结束作用域都会只报告一次所有权丢失。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_report_ownership_loss_once_when_object_is_overwritten()
    {
        var lifetimeObject = CreateLifetimeObject();

        await Assert.That(lifetimeObject.Overwrite()).IsEqualTo(LifetimeTransitionResultType.OWNERSHIP_LOSS);
        await Assert.That(lifetimeObject.CompleteScope()).IsEqualTo(LifetimeTransitionResultType.SUCCEEDED);
    }

    /// <summary>
    /// 验证借用对象不能由当前调用方释放。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_reject_disposal_when_object_is_borrowed()
    {
        var lifetimeObject = CreateLifetimeObject(isBorrowed: true);

        var result = lifetimeObject.Dispose();

        await Assert.That(result).IsEqualTo(LifetimeTransitionResultType.BORROWED);
    }

    /// <summary>
    /// 验证所有合并路径均已失效时资源不能继续使用。
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Should_reject_use_when_merged_paths_are_disposed_or_transferred()
    {
        var disposed = CreateLifetimeObject();
        var transferred = disposed.Clone();
        disposed.Dispose();
        transferred.TransferOwnership();

        disposed.MergeFrom(transferred);

        await Assert.That(disposed.Use()).IsNotEqualTo(LifetimeTransitionResultType.SUCCEEDED);
    }

    private static LifetimeObject CreateLifetimeObject(bool isBorrowed = false)
    {
        var tree = CSharpSyntaxTree.ParseText("sealed class Sample { private object field = new object(); }");
        var compilation = CSharpCompilation.Create(
            "LifetimeObjectTests",
            [tree,],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),]);
        var symbol = compilation.GetTypeByMetadataName("Sample")!.GetMembers("field").Single();

        return new(symbol, symbol.Locations[0], isUsing: false, isBorrowed: isBorrowed);
    }
}