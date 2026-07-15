# TedToolkit.Annotations

TedToolkit.Annotations is a .NET annotation library with a bundled Roslyn analyzer. It makes behavioral contracts, resource ownership, intentional boxing, and planned maintenance visible in C# source without introducing application logic.

## Packages and projects

| Project | Purpose |
| --- | --- |
| [`TedToolkit.Annotations`](TedToolkit.Annotations/README.md) | Public attributes and the `Explicit` marker API. This is the NuGet package consumed by applications and libraries. |
| [`TedToolkit.Annotations.Analyzer`](TedToolkit.Annotations.Analyzer/README.md) | Roslyn analyzers and code fixes bundled into the annotations package. |
| `TedToolkit.Annotations.Analyzer.Tests` | TUnit regression and behavior tests for the analyzers and code fixes. |

## Installation

```shell
dotnet add package TedToolkit.Annotations
```

The package adds both the attributes and its analyzer. No separate analyzer package is required.

## What it provides

- **Disposable lifetime analysis** tracks ownership, disposal, transfers, aliases, callbacks, and owned members for `IDisposable` and `IAsyncDisposable` values.
- **Const contracts** protect selected depths of an object graph from mutation and verify compatible contracts at method-call boundaries.
- **Explicit boxing** makes allocation-producing boxing conversions visible through `Explicit.Box`; a code fix rewrites implicit boxing.
- **Contract documentation** records preconditions, postconditions, invariants, assumptions, side effects, concurrency requirements, callback lifetime, and behavior cases.
- **Maintenance annotations** identify workarounds, temporary implementations, technical debt, and cleanup requirements at their call sites.
- **XML documentation generation** offers a code fix that derives `<exception>` entries from typed precondition annotations.

See the [annotations guide](TedToolkit.Annotations/README.md) for API usage and the [analyzer guide](TedToolkit.Annotations.Analyzer/README.md) for the complete diagnostic catalog.

## Conditional metadata

Most documentation attributes are always emitted. Two high-volume, source-oriented groups are conditional:

| Symbol | Metadata enabled |
| --- | --- |
| `ANNOTATIONS_MAINTENANCE` | Maintenance attributes such as `WorkaroundAttribute` and `TechnicalDebtAttribute`. |
| `ANNOTATIONS_BEHAVIOR_CASE` | Individual `BehaviorCaseAttribute` scenarios. |

Define a symbol only when reflection or downstream tooling needs that metadata:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ANNOTATIONS_MAINTENANCE;ANNOTATIONS_BEHAVIOR_CASE</DefineConstants>
</PropertyGroup>
```

The annotations remain in source when a symbol is absent.

## Supported target frameworks

- .NET 6.0 through .NET 10.0
- .NET Framework 4.7.2 and 4.8
- .NET Standard 2.0 and 2.1

The analyzer targets .NET Standard 2.0 for Roslyn host compatibility.

## Build and test

```shell
dotnet build TedToolkit.Annotations.slnx --configuration Release
dotnet run --project TedToolkit.Annotations.Analyzer.Tests/TedToolkit.Annotations.Analyzer.Tests.csproj --configuration Release
```

## License

Licensed under the [GNU Lesser General Public License v3.0](COPYING.LESSER).
