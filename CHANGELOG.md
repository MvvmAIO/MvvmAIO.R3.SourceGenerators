# Changelog

All notable changes to **MvvmAIO.R3.SourceGenerators** are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.6.0] - 2026-05-21

### Summary

This release completes **P2 product work** on top of the **0.5.x SyntaxFactory migration**. Generator output is built with Roslyn `SyntaxFactory` instead of string templates; **call-site APIs and generated member shapes are intended to stay compatible** with 0.5.x. Review the notes below if you depend on exact `.g.cs` text or obsolete wrapper type names.

### Added

- **R3Command diagnostics**
  - `R3SG1002` — `CanExecute` member not found on the declaring type
  - `R3SG1003` — `CanExecute` must be `R3.Observable<bool>` or `System.IObservable<bool>`
  - `R3SG1004` — duplicate generated command property name (use distinct `CommandName` values)
- **Routed events — interface-based codegen** — `FromRoutedEvents()` / `FromRoutedEventHandlers()` now return internal routed event interfaces (e.g. `IButtonRoutedEvents`) and implementations, aligned with `FromEvents()` / `FromEventHandlers()`.
- **CI / tests** — Roslyn 4.3.1 / 4.12 / 5.0 matrix scenarios, NuGet package layout smoke tests, pack validation in the Nuke `Ci` target.

### Changed

- **Internal codegen only (SyntaxFactory)** — observable events and `[R3Command]` emission use syntax trees (`ObservableEventsSyntaxFactory`, `R3CommandSyntaxFactory`, `GeneratorBootstrapSyntaxFactory`). No intentional change to extension method names, namespaces, or property chaining ergonomics.
- **Avalonia routed** — parameterless `FromRoutedEvents()` / `FromRoutedEventHandlers()` resolve to the typed interface overload (default routes: `Direct | Bubble`, `handledEventsToo: false`). Overloads with explicit `routes` / `handledEventsToo` are unchanged.

### Fixed

- (Carried from **0.5.3**) Parameterless `async Task` / `ValueTask` `[R3Command]` handlers no longer produce invalid mixed explicit/implicit lambda parameters (**CS0748**).

### Compatibility

| Area | User impact |
|------|-------------|
| `FromEvents()` / `FromEventHandlers()` | Same extension entry points; returns event interfaces as in 0.5.0+. |
| `FromRoutedEvents()` / `FromRoutedEventHandlers()` | **Return type** changes from per-type wrapper classes to routed event interfaces; **usage** (`source.FromRoutedEvents().Click`) stays the same. |
| `[R3Command]` | Same attributes and `ReactiveCommand` shapes; stricter **compile-time** errors for invalid `CanExecute` or duplicate command names. |
| Attached routed (`FromAttachedRoutedEvent*`) | Unchanged (`Observable<T>` extensions). |
| Static `ObservableEventsStatics` | Still disabled (`StaticObservableEventsGenerationEnabled = false`). |

### Upgrade checklist

1. Bump the package reference to **0.6.0**.
2. Rebuild; fix new **R3SG1002–1004** diagnostics if present.
3. If you referenced generated **wrapper type names** directly (unusual), switch to the extension + interface properties pattern documented in the README.
4. No change required for normal `using R3.SourceGenerators;` + chaining call sites.

---

## [0.5.3] - 2026-05

### Fixed

- `[R3Command]` on parameterless `async Task` / `ValueTask` methods: generated handler lambda no longer mixes `_` and `object __` parameter styles (**CS0748**).

---

## [0.5.2] - 2026-05

### Changed

- **Internal only:** migrated generator output from string concatenation to **Roslyn SyntaxFactory** (see repository rule `source-generator-syntax-first`). Intended **no user-facing API break**; snapshot tests updated for formatting-only differences in `.g.cs` files.

### Fixed

- Deterministic, formatter-friendly generated source structure.

---

## [0.5.0] - 2026-05

### Added

- **Interface-based** `FromEvents()` / `FromEventHandlers()` generation (hierarchy mirrors source types; generic constraint combined interfaces).
- Initial **SyntaxFactory** adoption for the interface pipeline.

### Changed

- Breaking vs **0.4.x**: `FromEvents()` returns `I{T}Events` interfaces instead of `{Type}FromEventObservable` wrapper classes.
