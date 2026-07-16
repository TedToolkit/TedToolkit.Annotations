# Project instructions

## Documentation language

Write all user-facing project documentation in English, including every `README.md`. Preserve code, commands, identifiers, package names, and URLs exactly as written.

## Warning policy

- `CA1813` is intentionally suppressed project-wide: public annotation attributes are extensibility points and must not be made `sealed` merely to satisfy the analyzer.
- `RCS1046` is intentionally suppressed in every `*.Tests` project: asynchronous test methods use behavior-focused names and must not be renamed just to add an `Async` suffix.
- Do not remove either suppression or change Attribute/test method declarations to satisfy these diagnostics without explicit approval.
