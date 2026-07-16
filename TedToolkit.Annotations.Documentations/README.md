# TedToolkit.Annotations.Documentations

Express API contracts, operational behavior, specifications, and design rationale as structured C# attributes. These annotations complement XML documentation; they do not change runtime behavior.

## Install

```shell
dotnet add package TedToolkit.Annotations.Documentations
```

The package includes an analyzer and a code fix that can generate XML `<exception>` documentation from typed preconditions.

## Use

Apply annotations to the API surface they describe:

```csharp
using TedToolkit.Annotations.Documentations;

[ThreadSafety(ThreadSafetyKind.THREAD_SAFE, "All state is protected by _gate.")]
public sealed class Counter
{
    [Precondition<ArgumentOutOfRangeException>("amount must be positive.")]
    [Postcondition("The returned value is greater than or equal to amount.")]
    [SideEffect(SideEffectKind.INSTANCE_STATE_MUTATION, "Increments the counter.")]
    public int Add(int amount) => 0;
}
```

The main groups are:

## Attribute reference

| Attribute | Effect | Use it when |
| --- | --- | --- |
| `Precondition` / `Precondition<TException>` | Records a condition callers must satisfy; the generic form identifies the exception thrown when it is violated. | A method or constructor requires a non-obvious argument, state, or environment condition. |
| `Postcondition` | Records a condition guaranteed after successful completion. | Callers need to rely on a return value or state guarantee not evident from the signature. |
| `Invariant` | Records a condition that remains true for an annotated type or member. | A persistent state rule must be preserved across maintenance. |
| `ThreadSafety` | Records thread-safety guarantees or synchronization requirements. | Concurrent use is supported, conditional, requires caller synchronization, or is unsupported. |
| `ThreadAffinity` | Records the required thread or synchronization context. | Code must run on a UI thread, a dispatcher, or another specific context. |
| `MayBlock` | Records that an operation can block and why. | An API can wait on I/O, locks, processes, user input, or another blocking source. |
| `SideEffect` | Records an observable effect, optionally with a standard category. | An API mutates state, performs I/O, publishes a notification, invokes a callback, or changes resource lifetime. |
| `Idempotent` | Records that repeated calls with the same effective input are safe. | Retrying an operation does not add a further observable effect. |
| `BehaviorCase` / `BehaviorCase<TException>` | Records an input condition and the expected result, with optional test coverage and exception type. | A method has important branches that should remain explicit and testable. |
| `StateTransition` | Records the required state before and guaranteed state after success. | An API advances an object, workflow, or resource through named states. |
| `DesignDecision` | Records a stable decision, its rationale, and rejected alternatives. | Future maintainers need to understand why an approach was chosen. |
| `DesignConstraint` | Records a boundary that must not be violated and why. | A design restriction preserves correctness, compatibility, security, or another essential property. |
| `Assumption` | Records an external fact or convention that the code relies on but does not verify. | Correctness depends on a protocol, deployment convention, or caller behavior outside the code's control. |

Use the enum overloads, such as `SideEffectKind` and `MayBlockKind`, when a standard category applies; provide the description that tells callers the actual condition or effect.

`Precondition<TException>` and `BehaviorCase<TException>` require `TException` to derive from `Exception`. The bundled code fix can generate XML `<exception>` documentation for typed preconditions.

## Conditional behavior cases

`BehaviorCase` annotations are always usable in source, but are emitted into metadata only when `ANNOTATIONS_BEHAVIOR_CASE` is defined:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ANNOTATIONS_BEHAVIOR_CASE</DefineConstants>
</PropertyGroup>
```

## Compatibility

Targets .NET 6.0 through .NET 10.0, .NET Framework 4.7.2 and 4.8, and .NET Standard 2.0 and 2.1.

## Related documentation

- [Repository overview](../README.md)
- [Assertion-backed preconditions](../TedToolkit.Annotations.Assertions/README.md)

## License

Licensed under the [GNU Lesser General Public License v3.0](../COPYING.LESSER).
