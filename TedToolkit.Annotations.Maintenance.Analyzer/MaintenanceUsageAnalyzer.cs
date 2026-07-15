// -----------------------------------------------------------------------
// <copyright file="MaintenanceUsageAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Annotations.Analyzer;

/// <summary>
/// Reports calls to members that are marked for future maintenance.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MaintenanceUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableDictionary<string, MaintenanceRule> _maintenanceAttributes =
        new Dictionary<string, MaintenanceRule>(StringComparer.Ordinal)
        {
            ["TedToolkit.Annotations.Maintenance.WorkaroundAttribute"] = new(
                DiagnosticResources.Get("MaintenanceWorkaroundKind"),
                MaintenanceUsageDiagnostics.WorkaroundInvoked),
            ["TedToolkit.Annotations.Maintenance.TemporaryImplementationAttribute"] = new(
                DiagnosticResources.Get("MaintenanceTemporaryImplementationKind"),
                MaintenanceUsageDiagnostics.TemporaryImplementationInvoked),
            ["TedToolkit.Annotations.Maintenance.TechnicalDebtAttribute"] = new(
                DiagnosticResources.Get("TechnicalDebtKindUnknown"),
                MaintenanceUsageDiagnostics.TechnicalDebtInvoked),
            ["TedToolkit.Annotations.Maintenance.CleanupRequiredAttribute"] = new(
                DiagnosticResources.Get("MaintenanceCleanupRequiredKind"),
                MaintenanceUsageDiagnostics.CleanupRequiredInvoked),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return ImmutableArray.Create(
                MaintenanceUsageDiagnostics.WorkaroundInvoked,
                MaintenanceUsageDiagnostics.TemporaryImplementationInvoked,
                MaintenanceUsageDiagnostics.TechnicalDebtInvoked,
                MaintenanceUsageDiagnostics.CleanupRequiredInvoked);
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
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var target = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            _ => null,
        };
        if (target is null)
        {
            return;
        }

        var attribute = target.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is not null && _maintenanceAttributes.ContainsKey(candidate.AttributeClass.ToDisplayString()));
        if (attribute?.AttributeClass is null
            || !_maintenanceAttributes.TryGetValue(attribute.AttributeClass.ToDisplayString(), out var rule))
        {
            return;
        }

        var reason = (object?)(attribute.ConstructorArguments.LastOrDefault().Value as string)
            ?? DiagnosticResources.Get("MaintenanceNoReasonSpecified");
        var removeWhen = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "RemoveWhen").Value.Value as string;
        var removalCondition = string.IsNullOrWhiteSpace(removeWhen)
            ? DiagnosticResources.Get("MaintenanceNoRemovalCondition")
            : DiagnosticResources.Get("MaintenanceRemovalCondition", removeWhen!);
        var kind = attribute.AttributeClass.Name == "TechnicalDebtAttribute" ? GetTechnicalDebtKind(attribute) : rule.MemberKind;

        context.ReportDiagnostic(Diagnostic.Create(rule.Diagnostic, context.Operation.Syntax.GetLocation(), kind,
            target.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), reason, removalCondition));
    }

    private static LocalizableResourceString GetTechnicalDebtKind(AttributeData attribute)
    {
        return attribute.ConstructorArguments.FirstOrDefault().Value is int kind ? kind switch
        {
            0 => DiagnosticResources.Get("TechnicalDebtKindDesign"),
            1 => DiagnosticResources.Get("TechnicalDebtKindCompatibility"),
            2 => DiagnosticResources.Get("TechnicalDebtKindPerformance"),
            3 => DiagnosticResources.Get("TechnicalDebtKindReliability"),
            _ => DiagnosticResources.Get("TechnicalDebtKindUnknown"),
        } : DiagnosticResources.Get("TechnicalDebtKindUnknown");
    }

    private sealed class MaintenanceRule(LocalizableString memberKind, DiagnosticDescriptor diagnostic)
    {
        public DiagnosticDescriptor Diagnostic { get; } = diagnostic;

        public LocalizableString MemberKind { get; } = memberKind;
    }
}