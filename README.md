# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

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

## Releases

Tags follow **v\<SemVer\>** and match the NuGet package version when published.

### v0.3.0

- **Breaking:** entry extension renamed from **`ObservableEvents()`** to **`FromEvents()`** (instance: `source.FromEvents()`, static: `ObservableEventsStatics.OBS_* .FromEvents()`). Per-type generated file hint is now `*.FromEvents.g.cs`.

### v0.2.0

- **Breaking:** generated ObservableEvents shim and wrappers now live in namespace **`R3`** (previously `R3.ObservableEvents`). Update `using` directives and any fully-qualified metadata references accordingly.

### v0.1.7

- **ObservableEvents (static):** post-init empty `ObservableEventsStatics` partial, syntax-only discovery of `ObservableEventsStatics.OBS_* .FromEvents()` when the method symbol is not bound yet (cold compile), skip instance extension/wrapper for `static` declaring types, and `internal` static entry `FromEvents()` to match internal wrapper return type (CS0050).

### v0.1.6

- **ObservableEvents:** emitted `Observable<T>`, delegate type arguments, and related type strings include NRT `?` where the source event does (closes [#1](https://github.com/MvvmAIO/MvvmAIO.R3.SourceGenerators/issues/1)).
- **Generated sources:** shim and per-type generated files begin with `#nullable enable` so analyzer-produced code stays valid alongside NRT punctuation (CS8669).

## Notes

- The generator is distributed as an analyzer package.
- Generated helper types for observable events are placed under the `R3` namespace (alongside the Cysharp R3 package types in consuming projects).
