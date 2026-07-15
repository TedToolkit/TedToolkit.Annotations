using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.Annotations.Analyzer;

internal static class ConstAnalysisOptions
{
    internal const string ENABLE_CONST_ANALYSIS_PROPERTY_NAME = "build_property.TedToolkitEnableConstAnalysis";

    internal static bool IsEnabled(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue(ENABLE_CONST_ANALYSIS_PROPERTY_NAME, out var value)
               && bool.TryParse(value, out var enabled)
               && enabled;
    }
}
