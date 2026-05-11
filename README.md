# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Stability

This project is in an **early stage**. Until **1.0.0** is published, **breaking changes** may occur without a long deprecation window (API surface, generated code shape, namespaces, and package layout can all change). When upgrading, review GitHub releases or commit history on the repository.

## Features

- **Observable events:** `FromEvents()`, `FromEventHandlers()`, and (when the consuming project sets **`UseWPF`** to true) `FromRoutedEvents()` / `FromRoutedEventHandlers()` for WPF routed CLR events. Entry extensions and generated wrappers live in namespace **`R3.SourceGenerators`**; emitted streams call **`R3.Observable`** from the Cysharp [**R3**](https://github.com/Cysharp/R3) package. Per-event surfaces are **properties** on the generated wrapper (ReactiveMarbles-style chaining).
- **`[R3Command]`** (`MvvmAIO.R3` namespace): generates `ReactiveCommand` members on **partial** declaring types.
- **Multi-Roslyn:** the NuGet package ships analyzer builds for **Roslyn 4.3 / 4.12 / 5.x** toolchains; MSBuild selects the matching folder under `analyzers/dotnet/`.
- Analyzer-style package layout for smooth NuGet installation.
- Generated payloads and delegate types preserve **nullable reference type (NRT)** annotations from the original event signatures (aligned with `#nullable`-enabled consuming projects).

## Installation

Latest:

```bash
dotnet add package MvvmAIO.R3.SourceGenerators
```

## Notes

- The generator is distributed as an analyzer package (`DevelopmentDependency`).
- Add `using R3.SourceGenerators;` to resolve `FromEvents` / `FromEventHandlers` / routed entry extensions. Generated wrappers are `internal` in that namespace; your code uses the public extension entry points and the Cysharp `R3` types in generated bodies.
