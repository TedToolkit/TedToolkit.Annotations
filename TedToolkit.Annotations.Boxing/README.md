# TedToolkit.Annotations.Boxing

Make intentional value-type boxing visible in C# source. The package includes an analyzer that reports implicit boxing conversions and offers a code fix.

## Install

```shell
dotnet add package TedToolkit.Annotations.Boxing
```

No separate analyzer package or configuration is required.

## Use

Replace an implicit conversion with `Boxer.Box` when allocating a boxed value is intentional:

```csharp
using TedToolkit.Annotations.Boxing;

int count = 42;
object value = Boxer.Box(count);
IComparable comparable = Boxer.Box<IComparable, int>(count);
```

Nullable values preserve their null state:

```csharp
int? count = GetCountOrNull();
object? value = Boxer.Box(count);
```

The call returns the same value represented as an object; it does not avoid the allocation. Its purpose is to make that allocation explicit to readers and the analyzer.

The bundled analyzer reports implicit boxing as `TAB201` (Info), and its code fix rewrites the conversion as the appropriate `Boxer.Box` call.

## When to use it

Use `Boxer.Box` at a boundary that requires a reference type—such as a non-generic API, an interface conversion, or a heterogeneous collection—when the resulting boxing allocation is intentional and acceptable. Do not use it merely to silence a diagnostic: prefer a generic API or another allocation-free design when boxing is avoidable.

This package has no public annotation attributes; its public contract is the `Boxer.Box` API.

## Compatibility

Targets .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1.

## Related documentation

- [Repository overview](../README.md)

## License

Licensed under the [GNU Lesser General Public License v3.0](../COPYING.LESSER).
