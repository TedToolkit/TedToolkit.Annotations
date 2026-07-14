; Contract for rules introduced or changed in the next NuGet package release.
; Move each row to AnalyzerReleases.Shipped.md unchanged when that package is published.
; Format: https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TTA001 | Lifetime | Error | Disposable resource is disposed more than once
TTA002 | Lifetime | Error | Disposed resource is used
TTA003 | Lifetime | Error | Transferred resource is used by the previous owner
TTA004 | Lifetime | Warning | Locally owned disposable resource is not released
TTA005 | Lifetime | Error | Callback can outlive its captured resource
TTA006 | Lifetime | Error | Disposable property is borrowed
TTA007 | Lifetime | Error | Disposed resource is returned
TTA008 | Lifetime | Warning | Owned disposable field requires a disposable containing type
TTA009 | Lifetime | Warning | Owned disposable field is not released
TTA010 | Lifetime | Error | Ownership target must be disposable
TTA100 | Maintenance | Info | Workaround API is invoked
TTA101 | Maintenance | Info | Temporary implementation is invoked
TTA102 | Maintenance | Info | Technical-debt API is invoked
TTA103 | Maintenance | Info | Cleanup-required API is invoked
