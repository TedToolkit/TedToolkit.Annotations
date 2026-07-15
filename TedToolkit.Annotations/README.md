# TedToolkit.Annotations

Lightweight attributes for making contracts, behavior, and planned maintenance visible in C# source. Documentation annotations describe code behavior. `BehaviorCaseAttribute` is emitted only when `ANNOTATIONS_BEHAVIOR_CASE` is defined; maintenance annotations are emitted only when `ANNOTATIONS_MAINTENANCE` is defined. These source-oriented annotations are intended for source readers and analyzers rather than runtime reflection.

The NuGet package also installs the bundled Roslyn analyzer. See the [analyzer diagnostic catalog](../TedToolkit.Annotations.Analyzer/README.md) for rule severities, supported analysis, code fixes, and `.editorconfig` configuration.

## Installation

```shell
dotnet add package TedToolkit.Annotations
```

## Documentation annotations

Use documentation annotations when important behavior would otherwise remain implicit in implementation comments. They do not change runtime behavior; most are descriptive, while the bundled analyzer enforces `ConstAttribute`. Define `ANNOTATIONS_BEHAVIOR_CASE` when tooling needs individual behavior cases in assembly metadata.

```csharp
using TedToolkit.Annotations.Documentations;

[Assumption("The caller holds the cache lock.")]
[Invariant("Entries are ordered by expiration time.")]
public sealed class Cache
{
    [Precondition("key is not null.")]
    [Postcondition("The returned entry belongs to key.")]
    [SideEffect("Updates the entry's last-access timestamp.")]
    [BehaviorCase("key is absent", "Returns null.", hasUnitTest: true)]
    public Entry? Get(string key) => null;
}
```

| Attribute | Use it when |
| --- | --- |
| `PreconditionAttribute` | The caller must satisfy a condition before calling a member; it can record the exception type for a failed condition. |
| `PostconditionAttribute` | A member guarantees a condition after it completes successfully. |
| `InvariantAttribute` | A condition must remain true throughout a type's lifetime. |
| `BehaviorCaseAttribute` | A specific input condition has important expected behavior, especially a boundary case; it can record an expected exception. Use `BehaviorCaseAttribute<TException>` on C# 11+ for type-safe exception metadata. |
| `AssumptionAttribute` | Code relies on an external fact or convention that it does not verify. |
| `SideEffectAttribute` | A member causes an observable state change beyond its return value. |
| `IdempotentAttribute` | Repeating an operation has no additional observable effect. |
| `ThreadSafetyAttribute` | A type or member has a documented thread-safety guarantee or synchronization requirement. |
| `OwnershipAttribute` | A disposable value is borrowed or its ownership is transferred across an API boundary or into a field. |
| `CallbackLifetimeAttribute` | A callback parameter is invoked immediately, retained for deferred invocation, or retained as a subscription. |
| `ConstAttribute` | A parameter, method, or property accessor must not mutate selected object-graph depths. |
| `MayBlockAttribute` | An operation can block the calling thread; document the condition that causes it. |
| `ThreadAffinityAttribute` | A type or member requires a particular thread or synchronization context. |

For a parameter-level precondition, optionally record the exception type. The generic form is available to C# 11 or later consumers and guarantees that the supplied type derives from `Exception`.

```csharp
public void Reserve(
    [Precondition<ArgumentOutOfRangeException>("Must be greater than zero.")]
    int quantity)
{
}
```

Use the non-generic form when supporting an earlier C# language version:

```csharp
[Precondition("Must be greater than zero.", typeof(ArgumentOutOfRangeException))]
int quantity
```

`DocumentationAttribute` is the shared abstract base and is not applied directly.

Typed `PreconditionAttribute<TException>` annotations also drive the hidden `TTA200` code fix, which adds missing XML `<exception>` entries to methods and constructors.

### Describe const object-graph depth

`ConstAttribute` describes the object-graph depths that code must not mutate. Its `ConstDepth` mask has one `uint` bit per depth and defaults to `ConstDepth.ALL`, protecting all 32 depths.

On a parameter, `DEPTH0` prevents reassignment of the parameter itself; `DEPTH1` protects its direct fields and properties; `DEPTH2` protects members of those members; and so on. On an instance method, `DEPTH0` protects direct fields and properties of `this`, `DEPTH1` protects their members, and so on. Use `DEPTHn_OR_GREATER` when a depth and every deeper depth must be protected.

The attribute can also annotate a property or an individual accessor. An accessor annotation takes precedence over the property annotation. When neither is present, a getter protects every depth, while a setter or `init` accessor protects `DEPTH1_OR_GREATER`: it may write any direct member of the current instance, but not a member below that level.

The bundled analyzer reports `TTA300` as an error when a supported write reaches a protected depth of an annotated parameter, method, property accessor, or local variable. It uses control-flow-aware may-alias tracking across branches, loops, exception handlers, conditional/coalescing expressions, deconstruction, and `foreach`. Reference-type aliases preserve the contract. Value-type copies allow writes to copied value fields while retaining contracts for shared objects reached through reference fields, and `ref` aliases follow ref reassignment. Contracts declared by overridden or interface members and parameters are combined by their implementations.

It covers assignments (including `??=` and deconstruction), increment/decrement, event subscription changes, array elements, and `ref`/`out` arguments. Calling a method on a protected receiver or passing a protected value by value requires a compatible `ConstAttribute` contract on the target method or parameter. An incompatible source method reports error `TTA304`; unverifiable external metadata reports informational `TTA305`. `ConstAttribute` is invalid on an `out` parameter and reports `TTA301`; it is invalid on a static method or property and reports `TTA303`.

Use `Explicit.Const` to apply the same contract to a local variable. It returns its input unchanged and is aggressively inlined. The ref overload preserves aliasing for ref locals. The call must directly initialize a local variable and use a compile-time constant depth mask; invalid calls report `TTA302`.

```csharp
var local = Explicit.Const(node, ConstDepth.DEPTH1_OR_GREATER);
ref var alias = ref Explicit.Const(ref node, ConstDepth.DEPTH1_OR_GREATER);
```

Use `Explicit.Box` to state that allocation-producing boxing is intentional. Use its target-type overload for interfaces and other reference views:

```csharp
object boxed = Explicit.Box(42);
IComparable comparable = Explicit.Box<IComparable, int>(42);
object? optional = Explicit.Box((int?)null);
```

Nullable values preserve normal boxing semantics: a value boxes its underlying value, while an empty nullable produces `null`. The bundled analyzer reports other boxing conversions as the informational diagnostic `TTA201` and provides a code fix that rewrites them to `Explicit.Box`.

```csharp
using TedToolkit.Annotations.Documentations;

public sealed class Node
{
    public Node? Next { get; set; }

    public int Value { get; set; }

    [Const(ConstDepth.DEPTH0_OR_GREATER)]
    public void Inspect([Const] Node node)
    {
        // node = new Node();       // TTA300: parameter depth 0
        // node.Value = 1;          // TTA300: parameter depth 1
        // node.Next!.Value = 1;    // TTA300: parameter depth 2
        // Value = 1;               // TTA300: method depth 0
    }
}
```

`out` parameters cannot receive `ConstAttribute`, because the method creates their value rather than accepting a value from the caller:

```csharp
public void Create([Const] out Node node) // TTA301
{
    node = new Node();
}
```

### Document concurrency, retries, and ownership

```csharp
[ThreadSafety("Concurrent reads are supported; writes require external synchronization.")]
public sealed class Cache
{
    [Idempotent]
    public void Clear() { }

    public void Attach([Ownership(OwnershipKind.TRANSFERRED)] Stream stream) { }

    public void Enqueue(
        [CallbackLifetime(CallbackLifetimeKind.DEFERRED)] Func<Task> work) { }
}
```

`ThreadSafetyAttribute` describes whether concurrent calls are safe and what synchronization they require. `ThreadAffinityAttribute` instead describes where code must run. `MayBlockAttribute` describes whether a synchronous operation can block its caller and why.

## Ownership contracts

`OwnershipAttribute` documents who is responsible for ultimately releasing an `IDisposable` or `IAsyncDisposable`. It is valid only on disposable fields, properties, parameters, and return values; the analyzer reports `TTA010` as an error when the annotated type implements neither disposal contract.

`OwnershipAttribute` always requires an `OwnershipKind` argument. In other words, the attribute constructor has no optional kind. `OwnershipKind.UNCHANGED` has the underlying enum value `0` and is therefore the CLR default enum value, but it is not a universal analyzer default for every API boundary:

| Kind | Meaning |
| --- | --- |
| `OwnershipKind.TRANSFERRED` | The receiver becomes responsible for disposing or transferring the resource. |
| `OwnershipKind.UNCHANGED` | The receiver only borrows the resource and must not dispose it. |

When `OwnershipAttribute` is absent, the analyzer applies boundary-specific ownership conventions. `OwnershipFlow` is optional when the attribute is present and selects which direction its explicit kind describes. The complete default relationship is:

| Boundary or source | Default flow | Default ownership kind when unannotated | Effect |
| --- | --- | --- | --- |
| Object creation | — | `TRANSFERRED` | The code receiving the new disposable owns it. |
| Method return value | `OUTPUT` | `TRANSFERRED` | The caller owns the returned disposable. |
| Property getter | `OUTPUT` | `UNCHANGED` | The caller borrows the returned property value. |
| Ordinary or `in` parameter | `INPUT` | `UNCHANGED` | The member borrows the caller's resource. |
| `out` parameter | `OUTPUT` | `TRANSFERRED` | The caller owns the resource assigned by the member. |
| `ref` parameter | none | none | Both directions are possible; declare `INPUT` and/or `OUTPUT` explicitly. |
| Property setter | `INPUT` | `UNCHANGED` | The setter borrows the assigned value unless an `INPUT` + `TRANSFERRED` contract says it takes ownership. |
| Field | `DEFAULT` | none | A field is storage, not an API boundary. Mark it explicitly or let constructor assignment inference determine ownership. |

For a property annotation, omitted `flow` means `OUTPUT`, so it describes the getter. Use an explicit `INPUT` annotation to describe the setter. For a `ref` parameter, both flow and kind must be stated for each direction that participates in ownership transfer.

For example, a factory transfers a new resource to its caller, while a shared resource is returned as borrowed:

```csharp
public sealed class ResourceFactory
{
    [return: Ownership(OwnershipKind.TRANSFERRED)]
    public Stream Create() => new MemoryStream();

    [return: Ownership(OwnershipKind.UNCHANGED)]
    public Stream Shared => _shared;

    private readonly Stream _shared = new MemoryStream();
}
```

A property can receive an owned resource yet expose only a borrowed reference:

```csharp
[Ownership(OwnershipKind.TRANSFERRED, OwnershipFlow.INPUT)]
[Ownership(OwnershipKind.UNCHANGED, OwnershipFlow.OUTPUT)]
public Stream Current { get; set; }
```

Fields describe the object's internal responsibility. Mark a field explicitly when its ownership cannot be inferred:

```csharp
public sealed class Session : IDisposable
{
    [Ownership(OwnershipKind.TRANSFERRED)]
    private readonly Stream _stream;

    public Session([Ownership(OwnershipKind.TRANSFERRED)] Stream stream)
        => _stream = stream;

    public void Dispose() => _stream.Dispose();
}
```

The analyzer also infers field ownership when a constructor assigns an `INPUT` + `TRANSFERRED` parameter to an instance disposable field. A type with owned members must implement the compatible synchronous or asynchronous disposal contract (`TTA008`) and must release or transfer every owned field or property from its disposal method (`TTA009`). A member marked `UNCHANGED` is borrowed and must not be disposed by its receiver (`TTA006`). Static fields are outside an individual instance's lifetime.

Conflicting ownership kinds for the same effective flow, unsupported flow directions, and a missing explicit flow on `ref` parameters report `TTA014`.

Asynchronous resources are tracked through `await using` and observed `DisposeAsync` calls. The returned `ValueTask` must be awaited, configured and awaited, or returned to the caller; otherwise the analyzer reports `TTA013`.

```csharp
public sealed class AsyncSession : IAsyncDisposable
{
    [Ownership(OwnershipKind.TRANSFERRED)]
    private readonly IAsyncDisposable _resource;

    public AsyncSession([Ownership(OwnershipKind.TRANSFERRED)] IAsyncDisposable resource)
        => _resource = resource;

    public ValueTask DisposeAsync() => _resource.DisposeAsync();
}
```

```csharp
[ThreadAffinity("Must be called from the UI thread.")]
public void UpdateView() { }

[MayBlock("Performs synchronous disk I/O.")]
public void Flush() { }
```

### Document parameters and return values

Attributes can describe the contract at the point where it matters most: a parameter or return value.

```csharp
using TedToolkit.Annotations.Documentations;

public sealed class Inventory
{
    [return: Postcondition("The result is non-negative.")]
    public int Reserve([Precondition("quantity is greater than zero.")] int quantity)
    {
        return quantity;
    }
}
```

### Capture assumptions and observable effects

```csharp
using TedToolkit.Annotations.Documentations;

public sealed class SessionService
{
    [Assumption("The caller has authenticated the request.")]
    [SideEffect("Revokes all refresh tokens for the user.")]
    [BehaviorCase("The user has no active sessions", "Completes without changes.", hasUnitTest: true)]
    public void SignOutEverywhere(Guid userId) { }
}
```

## Maintenance annotations

Apply these annotations to constructors or methods. Every annotation requires a concise reason and can optionally state the condition for removal with `RemoveWhen`.

```csharp
using TedToolkit.Annotations.Maintenances;

public sealed class MaintenanceExamples
{
    [Workaround("Serializer drops required members in version 4.2.",
        RemoveWhen = "Serializer 4.3 is the minimum supported version")]
    public void Serialize() { }

    [TemporaryImplementation("Use the legacy client until OAuth flow is available.",
        RemoveWhen = "OAuthClient is production-ready")]
    public void Connect() { }

    [TechnicalDebt(TechnicalDebtKind.Design,
        "Keep the parser and transport coupled until the protocol stabilizes.",
        RemoveWhen = "Protocol version 2 is released")]
    public void ProcessRequest() { }

    [CleanupRequired("Merge the duplicate validation branches.",
        RemoveWhen = "The legacy request format is removed")]
    public void Validate() { }
}
```

| Attribute | Use it when |
| --- | --- |
| `WorkaroundAttribute` | Code compensates for an external defect or limitation. |
| `TemporaryImplementationAttribute` | The implementation is intentionally incomplete and will be replaced. |
| `TechnicalDebtAttribute` | An intentional trade-off should be repaid; use its kind to classify the affected area. |
| `CleanupRequiredAttribute` | Correct code should later be simplified, removed, or reorganized. |

`MaintenanceAttribute` is the shared abstract base and is not applied directly. Do not use maintenance annotations for API deprecation; use `System.ObsoleteAttribute` instead.

### Keep maintenance context out of normal builds

Maintenance attributes are emitted only when `ANNOTATIONS_MAINTENANCE` is defined. Add it to the projects or build configuration where tooling needs to inspect this context:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ANNOTATIONS_MAINTENANCE</DefineConstants>
</PropertyGroup>
```
