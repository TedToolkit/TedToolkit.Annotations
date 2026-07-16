# TedToolkit.Annotations.Ownership

Document ownership transfer and callback lifetime for disposable resources. The bundled analyzer follows these contracts to detect lifetime mistakes.

## Install

```shell
dotnet add package TedToolkit.Annotations.Ownership
```

No separate analyzer package or configuration is required.

## Use

Annotate the boundary where ownership changes. Returns and `out` parameters transfer ownership by default; ordinary parameters borrow by default. State a non-default contract explicitly:

```csharp
using TedToolkit.Annotations.Ownership;

public sealed class Cache
{
    public void Store([Ownership(OwnershipKind.TRANSFERRED)] IDisposable item)
    {
        // This instance now owns and must dispose item.
    }

    [return: Ownership(OwnershipKind.UNCHANGED)]
    public Stream GetSharedStream() => _stream;
}
```

For `ref` parameters and properties, specify the direction with `OwnershipFlow.INPUT` or `OwnershipFlow.OUTPUT` when their input and output contracts differ.

Annotate callback parameters to show whether they can escape the call:

```csharp
public void Subscribe(
    [CallbackLifetime(CallbackLifetimeKind.SUBSCRIPTION)] Action handler)
{
    _handlers += handler;
}

public void Visit([CallbackLifetime(CallbackLifetimeKind.IMMEDIATE)] Action visitor)
    => visitor();
```

Use `DEFERRED` for a callback retained for later invocation, and `SUBSCRIPTION` for a callback retained until unsubscription or disposal.

## Attribute reference

| Attribute | Effect | Use it when |
| --- | --- | --- |
| `Ownership` | States whether a disposable value is borrowed (`UNCHANGED`) or ownership is transferred (`TRANSFERRED`) across a parameter, return, property, or field boundary. | The analyzer default for the boundary does not describe the API, especially when a callee takes responsibility for a parameter or returns a shared resource. |
| `CallbackLifetime` | States whether a callback is invoked only during the call (`IMMEDIATE`), kept for a later call (`DEFERRED`), or retained as a subscription (`SUBSCRIPTION`). | A callback can outlive the invoking call, or callers need to know it definitely cannot escape. |

`Ownership` is not a disposal operation; it documents who is responsible for disposal after the boundary. Use `OwnershipFlow.INPUT` and `OUTPUT` only for `ref` parameters and properties with distinct incoming and outgoing contracts.

## Analyzer diagnostics

The analyzer checks both `IDisposable` and `IAsyncDisposable` lifetimes. It reports double disposal, use after disposal or transfer, leaks, borrowed disposal, callback capture that outlives a resource, unobserved asynchronous disposal, and invalid ownership contracts (`TAO001` through `TAO014`).

## Compatibility

Targets .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1.

## Related documentation

- [Repository overview](../README.md)

## License

Licensed under the [GNU Lesser General Public License v3.0](../COPYING.LESSER).
