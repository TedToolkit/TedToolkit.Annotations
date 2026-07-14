# TedToolkit.Annotations

Lightweight attributes for making contracts, behavior, and planned maintenance visible in C# source. Documentation annotations describe code behavior. `BehaviorCaseAttribute` is emitted only when `ANNOTATIONS_BEHAVIOR_CASE` is defined; maintenance annotations are emitted only when `ANNOTATIONS_MAINTENANCE` is defined. These source-oriented annotations are intended for source readers and analyzers rather than runtime reflection.

## Installation

```shell
dotnet add package TedToolkit.Annotations
```

## Documentation annotations

Use documentation annotations when important behavior would otherwise remain implicit in implementation comments. They do not validate code or change runtime behavior. Define `ANNOTATIONS_BEHAVIOR_CASE` when tooling needs individual behavior cases in assembly metadata.

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
| `TransfersOwnershipAttribute` | The receiving member assumes ownership of an annotated parameter. |
| `CallbackLifetimeAttribute` | A callback parameter is invoked immediately, retained for deferred invocation, or retained as a subscription. |
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

### Document concurrency, retries, and ownership

```csharp
[ThreadSafety("Concurrent reads are supported; writes require external synchronization.")]
public sealed class Cache
{
    [Idempotent]
    public void Clear() { }

    public void Attach([TransfersOwnership] Stream stream) { }

    public void Enqueue(
        [CallbackLifetime(CallbackLifetimeKind.DEFERRED)] Func<Task> work) { }
}
```

`ThreadSafetyAttribute` describes whether concurrent calls are safe and what synchronization they require. `ThreadAffinityAttribute` instead describes where code must run. `MayBlockAttribute` describes whether a synchronous operation can block its caller and why.

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
