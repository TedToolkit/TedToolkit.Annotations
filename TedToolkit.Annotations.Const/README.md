# TedToolkit.Annotations.Const

Declare and verify object-graph immutability contracts. The bundled analyzer reports writes that violate a `Const` contract and checks contracts across calls.

## Install

```shell
dotnet add package TedToolkit.Annotations.Const
```

No separate analyzer package or configuration is required.

## Use

Apply `Const` to a parameter, method, or property. `DEPTH0_OR_GREATER` means the annotated object and every reachable member depth must not be mutated:

```csharp
using TedToolkit.Annotations.Const;

public sealed class Printer
{
    [Const(ConstDepth.DEPTH0_OR_GREATER)]
    public string Describe([Const] Order order)
    {
        // order.Status = Status.Processed; // Reported by the analyzer.
        return order.Customer.Name;
    }
}
```

For a local contract, wrap the assignment with `AsConst.Local`:

```csharp
var snapshot = AsConst.Local(order, ConstDepth.DEPTH1_OR_GREATER);
```

`DEPTH0` protects the parameter variable itself; `DEPTH1` protects its direct fields and properties, and each next depth protects one further member access. Use `DEPTHn_OR_GREATER` for a depth and all deeper depths, or `ConstDepth.ALL` (the default) for all 32 depths.

## Attribute reference

| Attribute | Effect | Use it when |
| --- | --- | --- |
| `Const` | Declares selected object-graph depths must not be mutated; the analyzer reports incompatible writes and calls. | An API observes data without changing it, or an instance member must preserve part of its own state. Apply it to methods, properties, and parameters. |

Use the narrowest depth that expresses the contract. Do not annotate `out` parameters: their value is produced by the callee, not supplied by the caller.

## License

Licensed under the [GNU Lesser General Public License v3.0](https://www.gnu.org/licenses/lgpl-3.0.html).
