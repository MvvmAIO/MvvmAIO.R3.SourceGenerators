# AGENTS.md

Guidance for AI agents working in **MvvmAIO.R3.SourceGenerators** and related workspace folders.

**This file is the single source of truth** for project conventions (formerly in `.cursor/rules/`). Update conventions here only; do not recreate Cursor rule files unless the user asks.

## What this project is

Roslyn **source generators** for [R3](https://github.com/Cysharp/R3) MVVM apps:

| Generator | User-facing API | Output |
|-----------|-----------------|--------|
| `ObservableEventsGenerator` | `FromEvents()`, `FromEventHandlers()`, `FromRoutedEvents()`, `FromRoutedEventHandlers()`, attached routed helpers | Internal event interfaces + `sealed` impls in `R3.SourceGenerators`; streams are **properties** on those interfaces |
| `R3CommandGenerator` | `[MvvmAIO.R3.R3Command]` on **partial** types | `ReactiveCommand` / `ReactiveCommand<T>` / `ReactiveCommand<T,TResult>` properties |

NuGet package: **`MvvmAIO.R3.SourceGenerators`** (DevelopmentDependency analyzer). Current package version in repo: see `MvvmAIO.R3.SourceGenerators.Package/MvvmAIO.R3.SourceGenerators.Package.csproj`.

Pre-1.0: breaking changes are still possible; see [CHANGELOG.md](CHANGELOG.md) and [README.md](README.md).

## Workspace layout

This Cursor workspace may include multiple roots:

| Path | Role |
|------|------|
| `MvvmAIO.R3.SourceGenerators/` | **Primary git repo** (`MvvmAIO/MvvmAIO.R3.SourceGenerators` on GitHub); **read [AGENTS.md](AGENTS.md)** in this folder first |
| `../R3.SourceGenerators.Samples/MvvmAIO.R3.SourceGenerators.Samples/` | Separate git repo; WPF + Avalonia demos consuming the NuGet package |
| `../R3.SourceGenerators.Docs/` | Separate git repo; **VitePress** consumer docs ([GitHub Pages](https://mvvmaio.github.io/R3.SourceGenerators.Docs/)) |
| `../ObservableEvents-1.3.1/` | Local **reference only** (ReactiveMarbles ObservableEvents 1.3.x); not part of product CI |

Do not commit `MvvmAIO.R3.SourceGenerators/.Temp/` (scratch; gitignored).

## Solution and projects

- Primary solution: **`MvvmAIO.R3.SourceGenerators.slnx`** (see [Solution format](#solution-format-prefer-slnx-over-sln) below).
- Shared generator sources: `MvvmAIO.R3.SourceGenerators/` (`.shproj` linked by Roslyn variant projects).
- Analyzer builds: `MvvmAIO.R3.SourceGenerators.Roslyn4031`, `.Roslyn4120`, `.Roslyn5000` (CodeAnalysis 4.3.1 / 4.12 / 5.0).
- Pack: `MvvmAIO.R3.SourceGenerators.Package/`.
- Tests: `MvvmAIO.R3.SourceGenerators.Tests/` (Verify snapshots + harness).

## Source map (where to edit)

| Area | Files |
|------|--------|
| Event discovery & interface pipeline | `ObservableEventsGenerator.cs` (~2.6k lines): `EmitInterfaceBasedSources`, `ExpandForInterfaces`, `CreateEventImplClass`, collection from syntax |
| Event syntax emission | `ObservableEventsSyntaxFactory.cs` |
| Post-init bootstrap (`NullEvents`, `object?` stubs) | `GeneratorBootstrapSyntaxFactory.cs` — stubs use `[EditorBrowsable(Never)]` since v0.6.1 |
| Commands | `R3CommandGenerator.cs`, `R3CommandSyntaxFactory.cs` |
| Diagnostics IDs | `Diagnostics/DiagnosticDescriptors.cs` |
| Emit header / `ToSource` | `GeneratedSourceHeader.cs` |
| Partial-type placement for commands | `Models/HierarchyInfo*.cs` |
| CI / pack / publish | `build/Program.cs` (Nuke), `.github/workflows/dotnet.yml`, `nuget-publish.yml` |

## Architecture (Observable events)

1. **Post-init** registers bootstrap extensions returning `NullEvents` for unresolved call sites (`this object?`).
2. **Incremental** pass collects invocation targets from user syntax.
3. **Interface pipeline** (default for instance events + routed):
   - Build `EventInterfaceDescriptor` hierarchy (exclusive events per type; names like `IButtonEvents`, `IButtonRoutedEvents`).
   - Emit `EventInterfaces.{kind}.g.cs` + per-type `{Type}.{kind}.g.cs` (extension + `*Impl`).
4. **Attached routed** (Avalonia): separate extensions returning `Observable<T>` — not the interface model.
5. **Static `ObservableEventsStatics` / `OBS_*`**: disabled (`StaticObservableEventsGenerationEnabled = false`).

**WPF routed:** requires consumer `UseWPF=true`. **Avalonia routed:** detected via `Avalonia.Interactivity.RoutedEvent` metadata; parameterless overload uses default `Direct | Bubble` + `handledEventsToo: false`; overload with `routes` / `handledEventsToo` uses `AddHandler`.

**Do not** add `FromEvents<T>(this T)` bootstrap: it collides with generic-constraint combined extensions (CS0111 / CS0121). See [CHANGELOG.md](CHANGELOG.md) 0.6.1 notes.

Design detail: [docs/design-interface-based-event-generation.md](docs/design-interface-based-event-generation.md) (may lag package version slightly — align with CHANGELOG when editing).

## Architecture (R3Command)

- Attribute: `MvvmAIO.R3.R3CommandAttribute` (post-init).
- Validates: partial type, method signature matrix, optional `CanExecute` member (`Observable<bool>` / `IObservable<bool>`), duplicate property names.
- Signature matrix and diagnostics: [README.md](README.md) § `[R3Command]`.

## Project conventions

### Source Generator Codegen Style

When implementing or modifying C# source generators in this repository:

- Prefer Roslyn Syntax APIs (`SyntaxFactory`, `CompilationUnitSyntax`, `TypeDeclarationSyntax`, etc.) to construct generated code.
- Avoid direct string concatenation/interpolation for full source output whenever a Syntax-based approach is practical.
- Use string literals only for small constants or unavoidable metadata snippets, not as the primary code generation mechanism.
- Keep generated output deterministic and formatter-friendly by composing syntax nodes first, then emitting text.

```csharp
// ❌ Avoid: primary generation by concatenating source strings
var source = "public partial class Foo { public int Bar { get; set; } }";
context.AddSource("Foo.g.cs", SourceText.From(source, Encoding.UTF8));
```

```csharp
// ✅ Prefer: build syntax tree and emit
CompilationUnitSyntax unit = SyntaxFactory.CompilationUnit()
    .AddMembers(classDeclarationSyntaxNode);
context.AddSource("Foo.g.cs", unit.NormalizeWhitespace().ToFullString());
```

In this repo, prefer emitting via `GeneratedSourceHeader.ToSource(CompilationUnitSyntax)` after composing syntax trees.

### Temporary files and scratch projects

**Where they go**

- Place **temporary projects** (e.g. a throwaway `dotnet new` app to test a generator), **one-off experiments**, and **other local-only disposable files** under the repository root directory **`.Temp/`** (not next to production projects unless they truly belong in the solution).

**Source control and tooling**

- **Git** — The `.Temp/` directory is listed in `.gitignore` and must **not** be committed.
- **Cursor** — `.Temp/` is listed in `.cursorignore` so scratch content stays out of default codebase indexing and reduces noise in AI context.

**Cleanup**

- Safe to delete all or part of `.Temp/` at any time; nothing in CI or the product build should depend on it.

For this product git root, that is `MvvmAIO.R3.SourceGenerators/.Temp/`.

### Solution format: prefer SLNX over SLN

**When creating or extending a solution**

- Use **`.slnx`** (XML solution format) as the **primary** solution for this repository.
- Prefer **`dotnet new slnx`** (or Visual Studio’s SLNX flow) when **creating a new solution** from scratch.
- When **adding or removing projects**, edit **the existing root `.slnx`** in this repository (or add another `.slnx` here if the workflow calls for it), not a legacy `.sln`.

**Avoid unless the user asks**

- Do **not** create a new **`.sln`** as the default way to represent the product solution. Classic `.sln` is harder to merge and duplicates the graph already in `.slnx`.

**Exceptions**

- A **secondary** `.sln` is acceptable only when the user **explicitly** asks for it (e.g. tooling that cannot consume `.slnx`). Document why it exists if you add one.

### Git / GitHub workflow

For substantive changes that go through GitHub:

1. **Issue** — Open (or reference) a GitHub issue describing the problem or feature before implementation when starting new work.
2. **Pull request** — Implement on a branch and open a PR that targets `master`, with the PR body linking the issue (e.g. `Closes #NN` or `Fixes #NN`).
3. **Merge** — When merging into `master`, use **Squash and merge** only (do not use merge commit or rebase merge for routine PRs unless the repo owner explicitly overrides).

CI should be green and the change set reviewed before merge.

### Additional conventions (agents)

- **Minimal diffs** — match existing naming and patterns; do not refactor unrelated generator paths.
- **Determinism** — order members/events (e.g. by name); keep output stable for Verify tests.
- **Commits / push** — do not commit or push unless the user explicitly asks.

## Diagnostics (quick reference)

| Id | Severity | Topic |
|----|----------|--------|
| R3SG0001 | Error | Containing type not `partial` |
| R3SG1001 | Error | Unsupported `[R3Command]` signature |
| R3SG1002 | Error | `CanExecute` member missing |
| R3SG1003 | Error | `CanExecute` wrong type |
| R3SG1004 | Error | Duplicate command property name |
| R3SG2001 | Warning | Unsupported event delegate (`FromEvents`) |
| R3SG2002 | Warning | Unsupported delegate for `FromEventHandlers` |

New diagnostics: update `DiagnosticDescriptors.cs`, tests in `*GeneratorTests.cs`, and **CHANGELOG / README** when user-visible.

## Build, test, CI

From `MvvmAIO.R3.SourceGenerators/`:

```bash
# Full local CI (Nuke)
dotnet run --project build/_build.csproj -- --target Ci

# Faster iteration
dotnet build MvvmAIO.R3.SourceGenerators.slnx
dotnet test MvvmAIO.R3.SourceGenerators.Tests/MvvmAIO.R3.SourceGenerators.Tests.csproj
```

Nuke `Ci` = `Restore` → `CompilePackage` → `Pack` → `Compile` → `ValidatePackage` → `UnitTestRoslynMatrix` → `UnitTest` (excludes package + matrix tests from default unit run).

- **Roslyn matrix:** `RoslynMatrixCoreTests` × 4031 / 4120 / 5000.
- **Package smoke:** `PackageIntegrationTests` (needs `artifacts/package` from Pack).
- **Snapshots:** Verify (`*.verified.txt`); update with `VERIFY` / accept tool when intentional output changes.

GitHub Actions: `dotnet.yml` (CI on `master`), `nuget-publish.yml` on tag `v*` → Nuke `Publish`.

## Release checklist (when user requests a version)

1. Bump `<Version>` in `MvvmAIO.R3.SourceGenerators.Package.csproj`.
2. Add dated section in [CHANGELOG.md](CHANGELOG.md); clear `[Unreleased]` if used.
3. Update [README.md](README.md) / design doc if behavior or public surface changed; sync **[R3.SourceGenerators.Docs](https://github.com/MvvmAIO/R3.SourceGenerators.Docs)** (canonical site: https://mvvmaio.github.io/R3.SourceGenerators.Docs/) — **both** `docs/` and `docs/zh/`:

   | Docs path | Update when |
   |-----------|-------------|
   | `diagnostics/reference.md` | Any **R3SG** ID or message text changes |
   | `generators/observable-events.md` | Event / routed API or observable codegen behavior |
   | `generators/r3-command.md` | `[R3Command]` signatures, attributes, or command diagnostics |
   | `getting-started.md` | Install steps or prerequisites change |
   | `changelog.md` | Each dated release (summary + link to this repo CHANGELOG) |
4. Run `Ci` locally.
5. Commit; push `master`; tag `vX.Y.Z`; push tag (triggers NuGet publish).
6. Optional: `gh release create vX.Y.Z` with notes from CHANGELOG.
7. Bump Samples repo package reference if demos should track the release.

## Testing changes

| Change type | What to run |
|-------------|-------------|
| Observable events / syntax output | `ObservableEventsGeneratorTests` + accept Verify snapshots |
| R3Command | `R3CommandGeneratorTests` |
| Pack layout / analyzer paths | `PackageIntegrationTests` after Pack |
| Roslyn API usage | matrix tests + all three Roslyn projects build |

Harness: `GeneratorTestHarness.Run` + `ToSnapshot`; references include `R3` and platform assemblies via `TRUSTED_PLATFORM_ASSEMBLIES`.

## Common agent tasks

- **New event entry or routed behavior:** start in `ObservableEventsGenerator.cs` collection + `EmitInterfaceBasedSources`; add syntax in `ObservableEventsSyntaxFactory.cs`; extend Avalonia/WPF helpers only if needed.
- **IntelliSense / bootstrap:** `GeneratorBootstrapSyntaxFactory.cs` — preserve `object?` stubs for constraint overload coexistence; prefer `[EditorBrowsable]` over new generic bootstrap signatures.
- **New command shape or diagnostic:** `R3CommandGenerator.cs` + descriptors + README matrix + tests.
- **Docs-only (generator repo):** README, CHANGELOG, `docs/design-interface-based-event-generation.md` — keep version/status in sync with package version.
- **Consumer docs site:** [R3.SourceGenerators.Docs](https://github.com/MvvmAIO/R3.SourceGenerators.Docs) (VitePress, Node 22). Canonical URL: https://mvvmaio.github.io/R3.SourceGenerators.Docs/ — update English `docs/` and 简体中文 `docs/zh/` (especially `diagnostics/reference.md` and generator pages) when **R3SG** or public API changes.

## Out of scope unless asked

- Enabling static `OBS_*` generation.
- Migrating attached routed events to the interface model.
- Force-push `master`, amending pushed commits, or changing git config (see [Git / GitHub workflow](#git--github-workflow)).
- Committing `.Temp/` (see [Temporary files](#temporary-files-and-scratch-projects)), secrets, or Samples’ local experiments without user direction.

## Human references

- Upstream style reference: ReactiveMarbles ObservableEvents (`ObservableEvents-1.3.1/` in workspace) uses `IObservable` + `Events()`; this repo uses **R3** + interface properties.
- Samples: `R3.SourceGenerators.Samples` (WPF `UseWPF`), `R3.SourceGenerators.Samples.Avalonia`.
