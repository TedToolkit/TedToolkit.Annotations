# TedToolkit.Annotations

`TedToolkit.Annotations` provides focused .NET packages for making contracts, resource lifetime, intentional boxing, and maintenance intent explicit in C# source. Each package ships its relevant Roslyn analyzer and code fixes; install only the contract you need.

## Choose a package

| I want to... | Package |
| --- | --- |
| Generate precondition attributes from `TedToolkit.Assertions` assertions | [`TedToolkit.Annotations.Assertions`](TedToolkit.Annotations.Assertions/README.md) |
| Make intentional boxing allocations explicit | [`TedToolkit.Annotations.Boxing`](TedToolkit.Annotations.Boxing/README.md) |
| Declare and enforce non-mutation contracts | [`TedToolkit.Annotations.Const`](TedToolkit.Annotations.Const/README.md) |
| Document API contracts, specifications, and design rationale | [`TedToolkit.Annotations.Documentations`](TedToolkit.Annotations.Documentations/README.md) |
| Mark workarounds, temporary implementations, and technical debt | [`TedToolkit.Annotations.Maintenance`](TedToolkit.Annotations.Maintenance/README.md) |
| Document disposable ownership and callback lifetime | [`TedToolkit.Annotations.Ownership`](TedToolkit.Annotations.Ownership/README.md) |

## Quick start

Install the package for the contract you want to express. For example, to make a boxing allocation explicit:

```shell
dotnet add package TedToolkit.Annotations.Boxing
```

```csharp
object value = Boxer.Box(42);
```

See the selected package README for its complete API and analyzer behavior.

## Compatibility

The packages target .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1. Bundled analyzers target .NET Standard 2.0 for Roslyn host compatibility.

## Development

Build the solution in Release configuration:

```shell
dotnet build TedToolkit.Annotations.slnx --configuration Release
```

Each `*.Tests` project contains the corresponding TUnit test suite.

## License

Licensed under the [GNU Lesser General Public License v3.0](COPYING.LESSER).
