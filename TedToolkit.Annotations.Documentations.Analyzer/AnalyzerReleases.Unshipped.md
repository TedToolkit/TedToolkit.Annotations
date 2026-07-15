; Contract for rules introduced or changed in the next NuGet package release.
; Move each row to AnalyzerReleases.Shipped.md unchanged when that package is published.
; Format: https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TAO001 | Lifetime | Error | Disposable resource is disposed more than once
TAO002 | Lifetime | Error | Disposed resource is used
TAO003 | Lifetime | Error | Transferred resource is used by the previous owner
TAO004 | Lifetime | Warning | Locally owned disposable resource is not released
TAO005 | Lifetime | Error | Callback can outlive its captured resource
TAO006 | Lifetime | Error | Disposable resource is borrowed
TAO007 | Lifetime | Error | Disposed resource is returned
TAO008 | Lifetime | Warning | Owned disposable member requires a compatible disposal contract
TAO009 | Lifetime | Warning | Owned disposable member is not released
TAO010 | Lifetime | Error | Ownership target must be disposable
TAO011 | Lifetime | Warning | Owned disposable resource is overwritten
TAO012 | Lifetime | Info | Owned disposable property is overwritten
TAO013 | Lifetime | Warning | Asynchronous disposal result is not observed
TAO014 | Lifetime | Error | Ownership contract is invalid
TAM100 | Maintenance | Info | Workaround API is invoked
TAM101 | Maintenance | Info | Temporary implementation is invoked
TAM102 | Maintenance | Info | Technical-debt API is invoked
TAM103 | Maintenance | Info | Cleanup-required API is invoked
TAB201 | Performance | Info | Boxing conversion should be explicit
TAC300 | Const | Error | Const contract is violated
TAC301 | Const | Error | Const cannot annotate an out parameter
TAC302 | Const | Error | Const.Local usage is invalid
TAC304 | Const | Error | Source method requires a compatible Const contract
TAC305 | Const | Info | External method has no compatible Const contract
