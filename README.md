# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Stability

This project is in an **early stage**. Until **1.0.0** is published, **breaking changes** may occur without a long deprecation window (API surface, generated code shape, namespaces, and package layout can all change). Pin a specific package version in production apps and review release notes when upgrading.

## Features

- `ObservableEvents` source generation for event-to-observable wrappers (`FromEvents()` entry + method-per-event pipelines under the `R3` namespace).
- Analyzer-style package layout for smooth NuGet installation.
- Generated payloads and delegate types preserve **nullable reference type (NRT)** annotations from the original event signatures (aligned with `#nullable`-enabled consuming projects).

## Installation

Latest:

```bash
dotnet add package MvvmAIO.R3.SourceGenerators
```

Pin a version (example **0.3.0**):

```bash
dotnet add package MvvmAIO.R3.SourceGenerators --version 0.3.0
```

## Notes

- The generator is distributed as an analyzer package.
- Generated helper types for observable events are placed under the `R3` namespace (alongside the Cysharp R3 package types in consuming projects).
