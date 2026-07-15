// -----------------------------------------------------------------------
// <copyright file="OwnedMemberOverwriteControlFlowTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Annotations.Analyzer.Tests.Lifetime;

namespace TedToolkit.Annotations.Analyzer.Tests.Lifetime.Members;

internal sealed class OwnedMemberOverwriteControlFlowTests
{
    /// <summary>
    /// 验证只有部分路径释放拥有字段时，后续覆盖仍会报告所有权丢失。
    /// </summary>
    [Test]
    public async Task Should_report_overwrite_when_only_one_path_releases_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                _resource.Dispose();
            }

            _resource = new Resource();
            """));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(["TTA011"]);
    }

    /// <summary>
    /// 验证所有路径都释放拥有字段后再覆盖时不会报告所有权丢失。
    /// </summary>
    [Test]
    public async Task Should_not_report_overwrite_when_all_paths_release_owned_field()
    {
        var diagnostics = await LifetimeAnalyzerTestHelper.AnalyzeAsync(CreateSource("""
            if (condition)
            {
                _resource.Dispose();
            }
            else
            {
                _resource.Dispose();
            }

            _resource = new Resource();
            """));

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string CreateSource(string statements)
    {
        return $$"""
            using System;
            using TedToolkit.Annotations.Documentations;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Owner : IDisposable
            {
                [Ownership(OwnershipKind.TRANSFERRED)]
                private Resource _resource = new Resource();

                public void Dispose() => _resource.Dispose();

                private void Replace(bool condition)
                {
                    {{statements}}
                }
            }
            """;
    }
}