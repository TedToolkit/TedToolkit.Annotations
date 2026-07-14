# TedToolkit.Annotations.Analyzer

Analyzer diagnostics are localized with embedded .NET resources.

## Add a language

1. Copy `Resources/DiagnosticResources.resx` to `Resources/DiagnosticResources.{culture}.resx`.
2. Translate every resource value while preserving each resource key and composite-format placeholder (for example, `{0}`).
3. Add or extend a TUnit test that requests the new `CultureInfo` and checks representative titles and formatted messages.

The neutral resource file is English and is the fallback for unavailable cultures. The SDK produces satellite assemblies automatically; hosts such as Visual Studio select them from their UI culture. No runtime localization dependency is required.
