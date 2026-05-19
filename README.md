# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Stability

This project is in an **early stage**. Until **1.0.0** is published, **breaking changes** may occur without a long deprecation window (API surface, generated code shape, namespaces, and package layout can all change). When upgrading, review GitHub releases or commit history on the repository.

## Features

- **Observable events:** `FromEvents()`, `FromEventHandlers()`, and `FromRoutedEvents()` / `FromRoutedEventHandlers()` for routed CLR events. WPF routed events are enabled when the consuming project sets **`UseWPF`** to true; Avalonia routed events are detected from `Avalonia.Interactivity.RoutedEvent<TEventArgs>` fields. Entry extensions live in namespace **`R3.SourceGenerators`**; emitted streams call **`R3.Observable`** from the Cysharp [**R3**](https://github.com/Cysharp/R3) package. Per-event surfaces are **properties** on generated event interfaces (ReactiveMarbles-style chaining).
- **Interface-based codegen (`FromEvents` / `FromEventHandlers`):** each discovered type gets an internal event interface (e.g. `IButtonEvents`) and a `sealed` implementation class. Interface inheritance mirrors the source type hierarchy so IntelliSense stays readable. See [docs/design-interface-based-event-generation.md](docs/design-interface-based-event-generation.md).
- **Generic constraints:** `source.FromEvents()` inside `where T : Base, I1, I2` resolves to a combined interface that inherits all constraint event interfaces—no manual casts between constraints.
- **Interface events:** `INotifyPropertyChanged` and other interface types are supported—events declared on interfaces and their base interfaces are collected automatically.
- **`[R3Command]`** (`MvvmAIO.R3` namespace): generates `ReactiveCommand` members on **partial** declaring types. Supports `CanExecute` binding to `Observable<bool>` or `IObservable<bool>`.
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
- Add `using R3.SourceGenerators;` to resolve `FromEvents` / `FromEventHandlers` / routed entry extensions. Generated interfaces and implementation types are `internal` in that namespace; your code uses the extension entry points and Cysharp `R3` types in generated bodies.
- `FromEvents()` / `FromEventHandlers()` return an **event interface** (not a concrete wrapper class). The compiler picks the most specific extension overload for the receiver type.

### FromEvents / FromEventHandlers (interface-based)

```csharp
public class Button : Control
{
    public event EventHandler<RoutedEventArgs>? Click;
}

// Extension returns IButtonEvents; implementation is internal.
var clicks = button.FromEvents().Click;
```

**Type hierarchy** — interfaces mirror base classes and implemented interfaces:

```csharp
public class BaseSource { public event Action? BaseChanged; }
public interface INotify { event EventHandler? Notified; }
public class DerivedSource : BaseSource, INotify
{
    public event Action<int>? DerivedChanged;
    public event EventHandler? Notified;
}

DerivedSource d = new();
_ = d.FromEvents().BaseChanged;    // from IBaseSourceEvents
_ = d.FromEvents().Notified;       // from INotifyEvents
_ = d.FromEvents().DerivedChanged; // from IDerivedSourceEvents
```

**Generic constraints** — one entry point for all constraint types:

```csharp
public static void Run<T>(T source)
    where T : BaseSource, IFirst, ISecond
{
    _ = source.FromEvents().BaseChanged;
    _ = source.FromEvents().FirstChanged;
    _ = source.FromEvents().SecondChanged;
}
```

### Routed events (Avalonia / WPF)

Routed and attached routed entry points still use the classic per-type wrapper shape today.

```csharp
var clicks = button.FromRoutedEvents().Click;
var clickHandlers = button.FromRoutedEventHandlers().Click;
```

Avalonia input scenarios that need handled events or explicit routing strategies:

```csharp
var pointerPressed = control
    .FromRoutedEvents(
        Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
        handledEventsToo: true)
    .PointerPressed;
```

Avalonia **attached** routed events:

```csharp
var childButtonClicks = parent.FromAttachedRoutedEvent(
    Avalonia.Controls.Button.ClickEvent,
    Avalonia.Interactivity.RoutingStrategies.Bubble,
    handledEventsToo: true);
```

## [R3Command] Usage

```csharp
public partial class ShellViewModel
{
    private readonly Observable<bool> _canSave = new Observable<bool>(true);

    [R3Command(CanExecute = nameof(_canSave))]
    private async Task Save()
    {
        // Execute logic here
    }
}
```

## Interface events

Events declared on interfaces are supported, including inherited interface events:

```csharp
public interface INotifySomething
{
    event EventHandler<EventArgs>? SomethingChanged;
}

public interface INotifyMore : INotifySomething
{
    event Action? MoreChanged;
}

public static void Run(INotifyMore s)
{
    _ = s.FromEvents().SomethingChanged;
    _ = s.FromEvents().MoreChanged;
}
```

## Design documentation

- [Interface-based event generation](docs/design-interface-based-event-generation.md) — naming, hierarchy algorithm, generic constraints, file layout.

## Samples

Runnable demos live in the sibling [MvvmAIO.R3.SourceGenerators.Samples](https://github.com/MvvmAIO/MvvmAIO.R3.SourceGenerators.Samples) repository (WPF + Avalonia).
