# TedToolkit.Annotations.Maintenance

Record intentional maintenance work directly on the affected code: external workarounds, temporary implementations, technical debt, and cleanup tasks.

## Install

```shell
dotnet add package TedToolkit.Annotations.Maintenance
```

## Use

Use the most specific annotation and state the reason in terms a future maintainer can act on:

```csharp
using TedToolkit.Annotations.Maintenance;

[Workaround("Work around upstream parser issue #1842 until version 4.2 is adopted.")]
public string Normalize(string value) => value.Trim();

[TechnicalDebt(TechnicalDebtKind.PERFORMANCE,
    "The linear scan is acceptable until the collection exceeds 1,000 items.")]
public Item? Find(string id) => _items.FirstOrDefault(item => item.Id == id);

[CleanupRequired("Remove this adapter after all callers migrate to the new API.")]
public void LegacyEntryPoint() { }
```

Use `TemporaryImplementation` for deliberately incomplete behavior. `TechnicalDebtKind` categorizes debt as `DESIGN`, `COMPATIBILITY`, `PERFORMANCE`, or `RELIABILITY`.

## Attribute reference

| Attribute | Effect | Use it when |
| --- | --- | --- |
| `Workaround` | Records a workaround for an external defect or limitation. | Code exists solely because of an upstream bug, platform behavior, or third-party constraint. Include the external issue and removal condition. |
| `TemporaryImplementation` | Records deliberately incomplete behavior. | A short-term implementation is shipped while the intended implementation is pending. |
| `TechnicalDebt` | Records a deliberate, categorized trade-off and its maintenance cost. | You knowingly accept a design, compatibility, performance, or reliability compromise. |
| `CleanupRequired` | Records code that is correct but should be removed, simplified, or reorganized. | There is no defect, but future maintenance should improve the structure. |

All maintenance attributes accept a reason and support `RemoveWhen` as a named argument. Use a concrete removal condition rather than a date or an unsearchable reminder.

## Conditional metadata

These attributes are always available in source. Define `ANNOTATIONS_MAINTENANCE` only when the attributes must also be retained in compiled metadata for reflection or downstream tooling:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ANNOTATIONS_MAINTENANCE</DefineConstants>
</PropertyGroup>
```

## Compatibility

Targets .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1.

## Related documentation

- [Repository overview](../README.md)

## License

Licensed under the [GNU Lesser General Public License v3.0](../COPYING.LESSER).
