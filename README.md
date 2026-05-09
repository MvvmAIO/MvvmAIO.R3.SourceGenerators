# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Features

- `ObservableEvents` source generation for event-to-observable wrappers (method-per-event pipelines under `R3.ObservableEvents`).
- Analyzer-style package layout for smooth NuGet installation.
- Generated payloads and delegate types preserve **nullable reference type (NRT)** annotations from the original event signatures (aligned with `#nullable`-enabled consuming projects).

## Installation

Latest:

```bash
dotnet add package MvvmAIO.R3.SourceGenerators
```

Pin a version (example **0.1.6**):

```bash
dotnet add package MvvmAIO.R3.SourceGenerators --version 0.1.6
```

## Releases

Tags follow **v\<SemVer\>** and match the NuGet package version when published.

### v0.1.6

- **ObservableEvents:** emitted `Observable<T>`, delegate type arguments, and related type strings include NRT `?` where the source event does (closes [#1](https://github.com/MvvmAIO/MvvmAIO.R3.SourceGenerators/issues/1)).
- **Generated sources:** shim and per-type generated files begin with `#nullable enable` so analyzer-produced code stays valid alongside NRT punctuation (CS8669).

## Notes

- The generator is distributed as an analyzer package.
- Generated helper types for observable events are placed under `R3.ObservableEvents`.
