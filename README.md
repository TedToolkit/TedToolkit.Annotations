# TedToolkit.Annotations

A small .NET library of attributes for making code intent explicit. It has two complementary uses:

- **Maintenance annotations** identify work that should be revisited: external workarounds, temporary implementations, technical debt, and required cleanup.
- **Documentation annotations** describe contracts and expected behavior: invariants, preconditions, postconditions, and individual behavior cases.

## Installation

```shell
dotnet add package TedToolkit.Annotations
```

## Maintenance annotations

Maintenance annotations apply to constructors and methods. They are marked with `Conditional("ANNOTATIONS_MAINTENANCE")`, so the attributes are emitted only in builds that define that symbol. This lets a project retain maintenance context in source without adding the metadata to its normal builds.

```csharp
using TedToolkit.Annotations.Maintenances;

public sealed class OrderImporter
{
    [Workaround("Supplier API returns duplicate records.",
        RemoveWhen = "Supplier API v3 is the minimum supported version")]
    public void Import() { }

    [TemporaryImplementation("Use polling until webhooks are available.",
        RemoveWhen = "Webhook delivery is enabled")]
    public void RefreshStatus() { }

    [TechnicalDebt(TechnicalDebtKind.Performance,
        "Load the full result set to preserve the existing ordering.",
        RemoveWhen = "The database query has a stable ordering")]
    public void GetOrders() { }

    [CleanupRequired("Consolidate duplicate validation paths.")]
    public void Validate() { }
}
```

`TechnicalDebtKind` categorizes debt as `Design`, `Compatibility`, `Performance`, or `Reliability`. All maintenance annotations accept a reason; `RemoveWhen` records the condition under which the annotation can be removed.

To emit maintenance attributes for reflection or analyzer tooling, define `ANNOTATIONS_MAINTENANCE` in the consuming project:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ANNOTATIONS_MAINTENANCE</DefineConstants>
</PropertyGroup>
```

## Documentation annotations

The documentation annotations record contracts and examples directly on types and members. They live in the `TedToolkit.Annotations.Documentations` namespace and do not validate code or change runtime behavior.

```csharp
using TedToolkit.Annotations.Documentations;

[Invariant("The balance is never negative.")]
public sealed class Account
{
    [Precondition("Amount is greater than zero.")]
    [Postcondition("The balance increases by the supplied amount.")]
    [BehaviorCase("Amount is 10", "Balance increases by 10", hasUnitTest: true)]
    public void Deposit(decimal amount) { }
}
```

### Document parameters and return values

```csharp
using TedToolkit.Annotations.Documentations;

public sealed class Inventory
{
    [return: Postcondition("The result is non-negative.")]
    public int Reserve(
        [Precondition<ArgumentOutOfRangeException>("Must be greater than zero.")]
        int quantity)
    {
        return quantity;
    }
}
```

`PreconditionAttribute` can optionally record the exception thrown when a condition is not met. Use `PreconditionAttribute<TException>` for a concise, type-safe C# 11+ form, or pass the type explicitly when supporting earlier language versions:

```csharp
[Precondition("Must be greater than zero.", typeof(ArgumentOutOfRangeException))]
int quantity
```

Use `AssumptionAttribute` for conditions controlled outside a member and `SideEffectAttribute` for observable state changes:

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

Use `IdempotentAttribute` when safely repeating an operation has no additional observable effect. Use `ThreadSafetyAttribute` to record concurrency guarantees or synchronization requirements. `TransfersOwnershipAttribute` is applied directly to a parameter when the receiving member becomes responsible for its lifetime:

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

- `InvariantAttribute` documents a condition that must hold for a class, struct, or interface.
- `PreconditionAttribute` documents a required state or input for a method, constructor, property, or parameter.
- `PostconditionAttribute` documents the state guaranteed after a method, constructor, property, or return value.
- `BehaviorCaseAttribute` documents a condition and expected result, optionally noting whether it has a unit test.
- `IdempotentAttribute` documents that repeating an operation has no additional observable effect.
- `ThreadSafetyAttribute` documents thread-safety guarantees or synchronization requirements.
- `TransfersOwnershipAttribute` documents a parameter whose ownership transfers to the receiving member.
- `CallbackLifetimeAttribute` documents whether a callback parameter is invoked immediately, retained for deferred invocation, or retained as a subscription.
- `MayBlockAttribute` documents an operation that can block the calling thread and the condition that causes it.
- `ThreadAffinityAttribute` documents a required thread or synchronization context.

`ThreadSafetyAttribute` answers whether concurrent calls are safe and what synchronization they require. `ThreadAffinityAttribute` answers where code must run. `MayBlockAttribute` answers whether synchronous code can block its caller and why:

```csharp
[ThreadAffinity("Must be called from the UI thread.")]
public void UpdateView() { }

[MayBlock("Performs synchronous disk I/O.")]
public void Flush() { }
```

## Supported frameworks

- .NET 6.0 through .NET 10.0
- .NET Framework 4.7.2 and 4.8
- .NET Standard 2.0 and 2.1

## License

Licensed under the [LGPL-3.0](COPYING.LESSER).
