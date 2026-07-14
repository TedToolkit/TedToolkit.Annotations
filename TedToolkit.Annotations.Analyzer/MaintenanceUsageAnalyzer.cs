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
    private static readonly ImmutableDictionary<string, MaintenanceRule> MAINTENANCE_ATTRIBUTES =
        new Dictionary<string, MaintenanceRule>(StringComparer.Ordinal)
        {
            ["TedToolkit.Annotations.Maintenances.WorkaroundAttribute"] = new("workaround", MaintenanceUsageDiagnostics.WorkaroundInvoked),
            ["TedToolkit.Annotations.Maintenances.TemporaryImplementationAttribute"] = new("temporary implementation", MaintenanceUsageDiagnostics.TemporaryImplementationInvoked),
            ["TedToolkit.Annotations.Maintenances.TechnicalDebtAttribute"] = new("technical-debt API", MaintenanceUsageDiagnostics.TechnicalDebtInvoked),
            ["TedToolkit.Annotations.Maintenances.CleanupRequiredAttribute"] = new("cleanup-required API", MaintenanceUsageDiagnostics.CleanupRequiredInvoked),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MaintenanceUsageDiagnostics.WorkaroundInvoked,
        MaintenanceUsageDiagnostics.TemporaryImplementationInvoked,
        MaintenanceUsageDiagnostics.TechnicalDebtInvoked,
        MaintenanceUsageDiagnostics.CleanupRequiredInvoked);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
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
            return;

        var attribute = target.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is not null && MAINTENANCE_ATTRIBUTES.ContainsKey(candidate.AttributeClass.ToDisplayString()));
        if (attribute?.AttributeClass is null || !MAINTENANCE_ATTRIBUTES.TryGetValue(attribute.AttributeClass.ToDisplayString(), out var rule))
            return;

        var reason = attribute.ConstructorArguments.LastOrDefault().Value as string ?? "No reason was specified";
        var removeWhen = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "RemoveWhen").Value.Value as string;
        var removalCondition = string.IsNullOrWhiteSpace(removeWhen) ? "No removal condition is specified" : $"Remove when: {removeWhen}";
        var kind = attribute.AttributeClass.Name == "TechnicalDebtAttribute" ? $"{GetTechnicalDebtKind(attribute)} technical-debt API" : rule.MemberKind;

        context.ReportDiagnostic(Diagnostic.Create(rule.Diagnostic, context.Operation.Syntax.GetLocation(), kind,
            target.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), reason, removalCondition));
    }

    private static string GetTechnicalDebtKind(AttributeData attribute) =>
        attribute.ConstructorArguments.FirstOrDefault().Value is int kind ? kind switch
        {
            0 => "DESIGN", 1 => "COMPATIBILITY", 2 => "PERFORMANCE", 3 => "RELIABILITY", _ => "UNKNOWN",
        } : "UNKNOWN";

    private sealed class MaintenanceRule(string memberKind, DiagnosticDescriptor diagnostic)
    {
        public DiagnosticDescriptor Diagnostic { get; } = diagnostic;
        public string MemberKind { get; } = memberKind;
    }
}
