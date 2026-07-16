// -----------------------------------------------------------------------
// <copyright file="ConstMutationAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using TedToolkit.Annotations.Analyzer.Const;
using TedToolkit.Annotations.Analyzer.Const.Contracts;
using TedToolkit.Annotations.Analyzer.Const.Flow;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports invalid const contracts, incompatible calls, and writes that violate protected object-graph depths.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstMutationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier for writes that violate a const contract.
    /// </summary>
    public const string DIAGNOSTIC_ID = "TAC300";

    /// <summary>
    /// The diagnostic identifier for a const attribute applied to an out parameter.
    /// </summary>
    public const string OUT_PARAMETER_DIAGNOSTIC_ID = "TAC301";

    /// <summary>
    /// The diagnostic identifier for an invalid use of AsConst.Local.
    /// </summary>
    public const string INVALID_LOCAL_DIAGNOSTIC_ID = "TAC302";

    /// <summary>
    /// The diagnostic identifier for a source method call that lacks a compatible const contract.
    /// </summary>
    public const string SOURCE_CALL_DIAGNOSTIC_ID = "TAC304";

    /// <summary>
    /// The diagnostic identifier for an external method call that lacks a compatible const contract.
    /// </summary>
    public const string EXTERNAL_CALL_DIAGNOSTIC_ID = "TAC305";

    /// <summary>
    /// Describes a write that violates a const contract.
    /// </summary>
    internal static readonly DiagnosticDescriptor MutationNotAllowed = ConstDiagnostics.MutationNotAllowed;

    /// <summary>
    /// Describes an invalid const contract on an out parameter.
    /// </summary>
    internal static readonly DiagnosticDescriptor OutParameterNotAllowed = ConstDiagnostics.OutParameterNotAllowed;

    /// <summary>
    /// Describes an invalid use of <c>AsConst.Local</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidLocal = ConstDiagnostics.InvalidLocal;

    /// <summary>
    /// Describes an incompatible call to a source method.
    /// </summary>
    internal static readonly DiagnosticDescriptor SourceCallRequiresConst = ConstDiagnostics.SourceCallRequiresConst;

    /// <summary>
    /// Describes an unverifiable call to an external method.
    /// </summary>
    internal static readonly DiagnosticDescriptor ExternalCallRequiresConst = ConstDiagnostics.ExternalCallRequiresConst;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(
                MutationNotAllowed,
                OutParameterNotAllowed,
                InvalidLocal,
                SourceCallRequiresConst,
                ExternalCallRequiresConst);
        }
    }

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(RegisterConstAnalysis);
    }

    private static void RegisterConstAnalysis(CompilationStartAnalysisContext context)
    {
        if (!ConstAnalysisOptions.IsEnabled(context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => ConstContractResolver.AnalyzeParameter(in symbolContext),
            SymbolKind.Parameter);
        context.RegisterOperationBlockAction(blockContext => ConstDataFlowAnalysis.Analyze(in blockContext));
    }
}