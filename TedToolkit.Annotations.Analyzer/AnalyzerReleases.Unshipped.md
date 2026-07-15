; Contract for rules introduced or changed in the next NuGet package release.
; Move each row to AnalyzerReleases.Shipped.md unchanged when that package is published.
; Hidden diagnostics used only to offer code fixes, such as TTA200, are not release-tracked rules.
; Format: https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TTA001 | Lifetime | Error | Disposable resource is disposed more than once
TTA002 | Lifetime | Error | Disposed resource is used
TTA003 | Lifetime | Error | Transferred resource is used by the previous owner
TTA004 | Lifetime | Warning | Locally owned disposable resource is not released
TTA005 | Lifetime | Error | Callback can outlive its captured resource
TTA006 | Lifetime | Error | Disposable resource is borrowed
TTA007 | Lifetime | Error | Disposed resource is returned
TTA008 | Lifetime | Warning | Owned disposable member requires a compatible disposal contract
TTA009 | Lifetime | Warning | Owned disposable member is not released
TTA010 | Lifetime | Error | Ownership target must be disposable
TTA011 | Lifetime | Warning | Owned disposable resource is overwritten
TTA012 | Lifetime | Info | Owned disposable property is overwritten
TTA013 | Lifetime | Warning | Asynchronous disposal result is not observed
TTA014 | Lifetime | Error | Ownership contract is invalid
TTA100 | Maintenance | Info | Workaround API is invoked
TTA101 | Maintenance | Info | Temporary implementation is invoked
TTA102 | Maintenance | Info | Technical-debt API is invoked
TTA103 | Maintenance | Info | Cleanup-required API is invoked
TTA201 | Performance | Info | Boxing conversion should be explicit
TTA300 | Const | Error | Const contract is violated
TTA301 | Const | Error | Const cannot annotate an out parameter
TTA302 | Const | Error | Explicit.Const usage is invalid
TTA304 | Const | Error | Source method requires a compatible Const contract
TTA305 | Const | Info | External method has no compatible Const contract
