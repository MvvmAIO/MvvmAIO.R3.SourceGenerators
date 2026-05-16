# MvvmAIO.R3.SourceGenerators

Roslyn source generators for R3-based MVVM workflows.

## Stability

This project is in an **early stage**. Until **1.0.0** is published, **breaking changes** may occur without a long deprecation window (API surface, generated code shape, namespaces, and package layout can all change). When upgrading, review GitHub releases or commit history on the repository.

## Features

- **Observable events:** `FromEvents()`, `FromEventHandlers()`, and `FromRoutedEvents()` / `FromRoutedEventHandlers()` for routed CLR events. WPF routed events are enabled when the consuming project sets **`UseWPF`** to true; Avalonia routed events are detected from `Avalonia.Interactivity.RoutedEvent<TEventArgs>` fields. Entry extensions and generated wrappers live in namespace **`R3.SourceGenerators`**; emitted streams call **`R3.Observable`** from the Cysharp [**R3**](https://github.com/Cysharp/R3) package. Per-event surfaces are **properties** on the generated wrapper (ReactiveMarbles-style chaining).
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
- Add `using R3.SourceGenerators;` to resolve `FromEvents` / `FromEventHandlers` / routed entry extensions. Generated wrappers are `internal` in that namespace; your code uses the public extension entry points and the Cysharp `R3` types in generated bodies.
- Avalonia routed events can use normal CLR wrapper subscriptions:

```csharp
var clicks = button.FromRoutedEvents().Click;
var clickHandlers = button.FromRoutedEventHandlers().Click;
```

- Avalonia input scenarios that need handled events or explicit routing strategies can pass routing options:

```csharp
var pointerPressed = control
    .FromRoutedEvents(
        Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
        handledEventsToo: true)
    .PointerPressed;
```

- Avalonia **attached** routed events are supported as single-event subscriptions (pass the owning type’s `RoutedEvent<T>` field; optional `routes` / `handledEventsToo` match `AddHandler`):

```csharp
var childButtonClicks = parent.FromAttachedRoutedEvent(
    Avalonia.Controls.Button.ClickEvent,
    Avalonia.Interactivity.RoutingStrategies.Bubble,
    handledEventsToo: true);

var childButtonClickHandlers = parent.FromAttachedRoutedEventHandler(
    Avalonia.Controls.Button.ClickEvent,
    routes: Avalonia.Interactivity.RoutingStrategies.Bubble,
    handledEventsToo: false);
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

## Interface Events

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

// Usage
public static void Run(INotifyMore s)
{
    _ = s.FromEvents().SomethingChanged;
    _ = s.FromEvents().MoreChanged;
}
```
