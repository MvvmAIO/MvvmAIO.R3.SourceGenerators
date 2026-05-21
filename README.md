# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Stability

This project is in an **early stage**. Until **1.0.0** is published, **breaking changes** may occur without a long deprecation window (API surface, generated code shape, namespaces, and package layout can all change). When upgrading, review [CHANGELOG.md](CHANGELOG.md) or GitHub releases.

## Features

- **Observable events** — `FromEvents()`, `FromEventHandlers()`, and routed `FromRoutedEvents()` / `FromRoutedEventHandlers()` (WPF when **`UseWPF`** is true; Avalonia when `RoutedEvent<T>` metadata is present). Entry extensions live in **`R3.SourceGenerators`**; streams use **`R3.Observable`** from [R3](https://github.com/Cysharp/R3). Per-event surfaces are **properties** on generated internal interfaces (ReactiveMarbles-style chaining).
- **Interface-based codegen** — event interfaces (e.g. `IButtonEvents`, `IButtonRoutedEvents`) and `sealed` implementations mirror the source type hierarchy. See [docs/design-interface-based-event-generation.md](docs/design-interface-based-event-generation.md).
- **Generic constraints** — `source.FromEvents()` inside `where T : Base, I1, I2` resolves to a combined interface inheriting all constraint event interfaces.
- **`[R3Command]`** (`MvvmAIO.R3`) — generates `ReactiveCommand` properties on **partial** types, with optional `CanExecute` and `CommandName`.
- **Multi-Roslyn** — analyzer builds for Roslyn **4.3 / 4.12 / 5.x**; MSBuild selects the matching folder under `analyzers/dotnet/`.
- **NRT** — generated code preserves nullable annotations from original event signatures.

## Installation

```bash
dotnet add package MvvmAIO.R3.SourceGenerators
```

The package is a **DevelopmentDependency** analyzer. Add `using R3.SourceGenerators;` for extension methods. Generated interfaces and implementations are `internal` in that namespace.

## Quick start

### FromEvents / FromEventHandlers

```csharp
public class Button : Control
{
    public event EventHandler<RoutedEventArgs>? Click;
}

// Returns IButtonEvents (internal interface).
var clicks = button.FromEvents().Click;
```

**Hierarchy** — one entry point, events from bases and interfaces:

```csharp
public class BaseSource { public event Action? BaseChanged; }
public interface INotify { event EventHandler? Notified; }
public class DerivedSource : BaseSource, INotify
{
    public event Action<int>? DerivedChanged;
    public event EventHandler? Notified { add; remove; }
}

DerivedSource d = new();
_ = d.FromEvents().BaseChanged;
_ = d.FromEvents().Notified;
_ = d.FromEvents().DerivedChanged;
```

**Generic constraints:**

```csharp
static void Run<T>(T source) where T : BaseSource, IFirst, ISecond
{
    _ = source.FromEvents().BaseChanged;
    _ = source.FromEvents().FirstChanged;
    _ = source.FromEvents().SecondChanged;
}
```

### Routed events (WPF / Avalonia)

Same **interface + property** model as `FromEvents` (e.g. `IButtonRoutedEvents`). Parameterless calls use default Avalonia routes (`Direct | Bubble`, `handledEventsToo: false`).

```csharp
var clicks = button.FromRoutedEvents().Click;
var clickHandlers = button.FromRoutedEventHandlers().Click;
```

Explicit routing (Avalonia):

```csharp
var pointerPressed = control
    .FromRoutedEvents(
        Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
        handledEventsToo: true)
    .PointerPressed;
```

**Attached** routed events (still `Observable<T>` extensions, not the interface model):

```csharp
var childClicks = panel.FromAttachedRoutedEvent(
    Avalonia.Controls.Button.ClickEvent,
    Avalonia.Interactivity.RoutingStrategies.Bubble,
    handledEventsToo: true);
```

### Interface-only event sources

```csharp
public interface INotifyMore : INotifySomething
{
    event Action? MoreChanged;
}

static void Run(INotifyMore s)
{
    _ = s.FromEvents().SomethingChanged;
    _ = s.FromEvents().MoreChanged;
}
```

## [R3Command]

Apply to **instance methods** on a **partial** class or struct. The generator adds a public `{Name}Command` property (or `CommandName`) backed by `ReactiveCommand`.

**Minimal example:**

```csharp
public partial class ShellViewModel
{
    private readonly Observable<bool> _canSave = new(true);

    [R3Command(CanExecute = nameof(_canSave))]
    private async Task Save() { /* ... */ }
}
```

### Method signature matrix

| Parameters | Return type | Generated property type | Notes |
|------------|-------------|-------------------------|--------|
| none | `void` | `ReactiveCommand` | Sync handler |
| one (`T`) | `void` | `ReactiveCommand<T>` | Sync with argument |
| none | `Task` | `ReactiveCommand` | Async; wrapped as `ValueTask` in handler |
| one (`T`) | `Task` | `ReactiveCommand<T>` | Async with argument |
| none | `ValueTask` | `ReactiveCommand` | Same as `Task` without result |
| one (`T`) | `ValueTask` | `ReactiveCommand<T>` | |
| one (`T`) | `Task<TResult>` | `ReactiveCommand<T, TResult>` | Result type from `TResult` |
| one (`T`) | `ValueTask<TResult>` | `ReactiveCommand<T, TResult>` | |
| none | `Task<TResult>` / `ValueTask<TResult>` | — | **Not supported** (`R3SG1001`) |
| two or more parameters | any | — | **Not supported** (`R3SG1001`) |
| `static` method | any | — | **Not supported** (`R3SG1001`) |

### Attribute members

| Attribute property | Effect |
|--------------------|--------|
| *(none)* | Property name `{MethodName}Command` |
| `CommandName = "Submit"` | Property name `Submit` |
| `CanExecute = nameof(_canSave)` | Passes `_canSave` into `ReactiveCommand` ctor; must exist on the same partial type and be `Observable<bool>` or `IObservable<bool>` |

### Diagnostics

| Id | When |
|----|------|
| `R3SG0001` | Declaring type is not `partial` |
| `R3SG1001` | Method signature not in the matrix above |
| `R3SG1002` | `CanExecute` member name not found |
| `R3SG1003` | `CanExecute` type is not `Observable<bool>` / `IObservable<bool>` |
| `R3SG1004` | Two methods would generate the same command property name |

Event-related warnings (`R3SG2001`, `R3SG2002`) apply to unsupported event delegate shapes for `FromEvents` / `FromEventHandlers`.

## Design documentation

- [Interface-based event generation](docs/design-interface-based-event-generation.md) — naming, hierarchy, routed events, file layout.
- [CHANGELOG.md](CHANGELOG.md) — release notes and upgrade compatibility (including SyntaxFactory internal migration).

## Samples

Runnable demos: [MvvmAIO.R3.SourceGenerators.Samples](https://github.com/MvvmAIO/MvvmAIO.R3.SourceGenerators.Samples) (WPF + Avalonia).
