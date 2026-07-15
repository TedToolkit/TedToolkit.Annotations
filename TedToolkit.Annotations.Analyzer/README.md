# TedToolkit.Annotations.Analyzer

Roslyn analyzers and code fixes bundled with the `TedToolkit.Annotations` NuGet package. The analyzer targets .NET Standard 2.0 and analyzes C# source without contributing runtime dependencies to the consuming application.

## Diagnostic catalog

### Disposable lifetime and ownership

Disposable lifetime and ownership checks are disabled by default. Enable them per project when the project follows the ownership contracts:

```xml
<PropertyGroup>
  <TedToolkitEnableOwnershipAnalysis>true</TedToolkitEnableOwnershipAnalysis>
</PropertyGroup>
```

When the property is absent or set to any value other than `true`, rules `TTA001` through `TTA014` do not run.

| ID | Severity | Meaning |
| --- | --- | --- |
| `TTA001` | Error | An owned disposable resource is disposed more than once. |
| `TTA002` | Error | A disposed resource is used. |
| `TTA003` | Error | A resource is used after its ownership has been transferred. |
| `TTA004` | Warning | A locally owned disposable resource can leave the operation without being released or transferred. |
| `TTA005` | Error | A retained callback can outlive a disposable resource that it captures. |
| `TTA006` | Error | Borrowed disposable state is disposed by code that does not own it. |
| `TTA007` | Error | A disposed resource is returned to the caller. |
| `TTA008` | Warning | A type that owns a disposable member lacks the compatible disposal interface. |
| `TTA009` | Warning | An owned disposable field or property is not released or transferred by the disposal member. |
| `TTA010` | Error | `OwnershipAttribute` is applied to a value that neither implements a disposal contract nor structurally carries a disposable resource. |
| `TTA011` | Warning | An owned disposable resource is overwritten before its previous value is released. |
| `TTA012` | Info | An owned disposable property may be overwritten before its previous value is released. |
| `TTA013` | Warning | The result of `DisposeAsync` is neither awaited, returned, nor otherwise observed. |
| `TTA014` | Error | Ownership annotations contain an invalid flow or conflicting contracts. |

The lifetime analysis is control-flow aware. It tracks aliases and state across branches, loops, exception paths, `using`/`await using`, explicit disposal, ownership transfers, returned resources, and synchronous or asynchronous owned members. Diagnostics are emitted only for states the analysis can model; the analyzer is not a runtime leak detector.

Unannotated boundaries follow target-specific ownership defaults rather than one global `OwnershipKind`: method returns and `out` parameters transfer ownership to the caller, while ordinary parameters and property getters are borrowed. See the [ownership contract defaults](../TedToolkit.Annotations/README.md#ownership-contracts) for the complete flow/kind table, including `ref`, setters, fields, and object creation.

### Maintenance usage

| ID | Severity | Meaning |
| --- | --- | --- |
| `TTA100` | Info | A member annotated with `WorkaroundAttribute` is invoked. |
| `TTA101` | Info | A member annotated with `TemporaryImplementationAttribute` is invoked. |
| `TTA102` | Info | A member annotated with `TechnicalDebtAttribute` is invoked. |
| `TTA103` | Info | A member annotated with `CleanupRequiredAttribute` is invoked. |

These rules require the maintenance attributes to be present in the analyzed compilation. Define `ANNOTATIONS_MAINTENANCE` when call-site diagnostics are required.

### Documentation, behavior, and boxing

| ID | Severity | Meaning |
| --- | --- | --- |
| `TTA200` | Info | XML `<exception>` documentation can be generated from a typed precondition. |
| `TTA201` | Info | A boxing conversion should be written with `Explicit.Box`. |
| `TTA202` | Info | A `BehaviorCaseAttribute` annotation is not covered by a unit test. |

The `TTA200` code fix adds missing exception entries without duplicating existing XML documentation. The `TTA201` code fix preserves the target reference type, including interface targets and nullable boxing semantics.

### Const contracts

Const checks are disabled by default. Enable them per project when the project uses `ConstAttribute` contracts:

```xml
<PropertyGroup>
  <TedToolkitEnableConstAnalysis>true</TedToolkitEnableConstAnalysis>
</PropertyGroup>
```

When the property is absent or set to any value other than `true`, rules `TTA300` through `TTA305` do not run.

| ID | Severity | Meaning |
| --- | --- | --- |
| `TTA300` | Error | A write reaches an object-graph depth protected by a const contract. |
| `TTA301` | Error | `ConstAttribute` is applied to an `out` parameter. |
| `TTA302` | Error | `Explicit.Const` is not a direct local initializer or its depth mask is not constant. |
| `TTA304` | Error | A source method call lacks a compatible const receiver or parameter contract. |
| `TTA305` | Info | An external method call has no verifiable compatible const contract. |

Const analysis follows aliases through control flow, including `ref` aliases, deconstruction, conditional and coalescing expressions, loops, and exception handlers. Contracts inherited from overridden and interface members are combined with the implementation contract.

## Configuration

Use standard `.editorconfig` diagnostic configuration to change severities or disable a rule:

```ini
[*.cs]
dotnet_diagnostic.TTA305.severity = none
dotnet_diagnostic.TTA004.severity = error
```

## Localization

Diagnostic messages use embedded .NET resources. English is the neutral fallback and Simplified Chinese is currently supplied by `Resources/DiagnosticResources.zh-CN.resx`.

To add a language:

1. Copy `Resources/DiagnosticResources.resx` to `Resources/DiagnosticResources.{culture}.resx`.
2. Translate every value while preserving resource keys and composite-format placeholders such as `{0}`.
3. Extend `DiagnosticLocalizationTests` with the requested `CultureInfo` and representative formatted messages.

The SDK creates satellite assemblies automatically, and Roslyn hosts select a resource using their UI culture.

## Development

Run the analyzer test project in Release configuration:

```shell
dotnet run --project TedToolkit.Annotations.Analyzer.Tests/TedToolkit.Annotations.Analyzer.Tests.csproj --configuration Release
```

New or changed diagnostics must also be recorded in `AnalyzerReleases.Unshipped.md`. Move entries to `AnalyzerReleases.Shipped.md` unchanged when a package containing them is released.
