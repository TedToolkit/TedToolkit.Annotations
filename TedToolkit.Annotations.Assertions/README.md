# TedToolkit.Annotations.Assertions

Document assertion-backed preconditions with attributes generated from
`TedToolkit.Assertions` assertion items.

## Install

```shell
dotnet add package TedToolkit.Annotations.Assertions
```

The package includes an analyzer that generates a `*PreconditionAttribute` for
each assertion item. Attribute-compatible assertion parameters keep their
original types; other parameters are represented as `string`.

For a generic assertion item, the generator emits both a generic attribute that
accepts the generic value and a non-generic attribute that accepts its string
representation. Both forms end with an optional `Type` exception parameter.

## Use

Reference `TedToolkit.Assertions` and declare assertion items as usual. The bundled source generator discovers each `IAssertionItem<T>` and makes the matching precondition attribute available on parameters:

```csharp
public void SetLimit([BeGreaterThanPrecondition(0)] int limit)
{
}
```

The bundled analyzer reports a parameter precondition that is not enforced by the method body, and its code fix adds the corresponding assertion. Generated attributes derive from `AssertionPreconditionAttribute`, which in turn is a `PreconditionAttribute` from `TedToolkit.Annotations.Documentations`.

## Compatibility

Targets .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1. `TedToolkit.Assertions` and `TedToolkit.Annotations.Documentations` are package dependencies.

## Related documentation

- [Repository overview](../README.md)
- [Documentation annotations](../TedToolkit.Annotations.Documentations/README.md)

## License

Licensed under the [GNU Lesser General Public License v3.0](../COPYING.LESSER).
