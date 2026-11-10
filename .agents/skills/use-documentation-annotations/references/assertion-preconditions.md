# Document Assertion Preconditions

Confirm the consumer references `TedToolkit.Annotations.Assertions`. Build the consumer to inspect the attributes generated from its `IAssertionItem<T>` implementations.

- Select the assertion that matches the actual method boundary and put its generated `*PreconditionAttribute` on the parameter. For example, use `[BeGreaterThanPrecondition(0)] int limit` for a strict positive limit.
- Use attribute-compatible argument types when the generated signature exposes them. Check the generated generic and string forms before choosing how to represent a generic assertion value.
- Enforce the same precondition in the method body. Use the analyzer's code fix where it supplies the appropriate assertion, then confirm argument values and exception behavior.
- Build with the analyzer and test a value on each side of the boundary, including the exact boundary for strict comparisons.

```csharp
public void SetLimit([BeGreaterThanPrecondition(0)] int limit)
{
}
```

The annotation documents a precondition for consumers. The analyzer and method-body assertion enforce it. Check both the boundary value and a value that satisfies the predicate.
