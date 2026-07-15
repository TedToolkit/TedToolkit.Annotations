using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

internal static class OwnershipAnalysisOptions
{
    internal const string ENABLE_OWNERSHIP_ANALYSIS_PROPERTY_NAME = "build_property.TedToolkitEnableOwnershipAnalysis";

    internal static bool IsEnabled(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue(ENABLE_OWNERSHIP_ANALYSIS_PROPERTY_NAME, out var value)
               && bool.TryParse(value, out var enabled)
               && enabled;
    }
}
