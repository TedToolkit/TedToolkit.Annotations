; Contract for rules introduced or changed in the next NuGet package release.
; Move each row to AnalyzerReleases.Shipped.md unchanged when that package is published.
; Format: https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TAC300 | Const | Error | Const contract is violated
TAC301 | Const | Error | Const cannot annotate an out parameter
TAC302 | Const | Error | Const.Local usage is invalid
TAC304 | Const | Error | Source method requires a compatible Const contract
TAC305 | Const | Info | External method has no compatible Const contract
