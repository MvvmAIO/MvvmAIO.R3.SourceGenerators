using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace MvvmAIO.R3.SourceGenerators.Tests;

public sealed class ObservableEventsGeneratorTests
{
    [Fact]
    public Task Generates_FromEvents_wrapper_for_action_event()
    {
        const string source = """
            namespace Demo;

            public class ClickSource
            {
                public event System.Action? Click;
            }

            public static class Usage
            {
                public static void Run(ClickSource s) => s.FromEvents();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Generates_Avalonia_routed_event_wrappers()
    {
        const string source = AvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.Button button)
                    {
                        _ = button.FromRoutedEvents().Click;
                        _ = button.FromRoutedEventHandlers(Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true).Click;
                    }
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("FromAvaloniaRoutedEventObservable", snapshot);
        Assert.Contains("FromAvaloniaRoutedEventHandlerObservable", snapshot);
        Assert.Contains("_sender.AddHandler(global::Avalonia.Controls.Button.ClickEvent, h, _routes, _handledEventsToo)", snapshot);
    }

    [Fact]
    public void Generates_Avalonia_attached_routed_event_extensions()
    {
        const string source = AvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.Panel panel)
                    {
                        _ = panel.FromAttachedRoutedEvent(Avalonia.Controls.Button.ClickEvent, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
                        _ = panel.FromAttachedRoutedEventHandler(Avalonia.Controls.Button.ClickEvent);
                    }
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("FromAttachedRoutedEvent<TEventArgs>", snapshot);
        Assert.Contains("FromAttachedRoutedEventHandler<TEventArgs>", snapshot);
        Assert.Contains("source.AddHandler(routedEvent, h, routes, handledEventsToo)", snapshot);
    }

    private const string AvaloniaStubs = """
        namespace Avalonia.Interactivity
        {
            [System.Flags]
            public enum RoutingStrategies
            {
                Direct = 1,
                Bubble = 2,
                Tunnel = 4,
            }

            public class RoutedEventArgs : System.EventArgs
            {
            }

            public class RoutedEvent<TEventArgs>
                where TEventArgs : RoutedEventArgs
            {
            }
        }

        namespace Avalonia.Controls
        {
            public class Control
            {
                public void AddHandler<TEventArgs>(
                    Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    System.EventHandler<TEventArgs>? handler,
                    Avalonia.Interactivity.RoutingStrategies routes = Avalonia.Interactivity.RoutingStrategies.Direct | Avalonia.Interactivity.RoutingStrategies.Bubble,
                    bool handledEventsToo = false)
                    where TEventArgs : Avalonia.Interactivity.RoutedEventArgs
                {
                }

                public void RemoveHandler<TEventArgs>(
                    Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    System.EventHandler<TEventArgs>? handler)
                    where TEventArgs : Avalonia.Interactivity.RoutedEventArgs
                {
                }
            }

            public class Panel : Control
            {
            }

            public class Button : Control
            {
                public static readonly Avalonia.Interactivity.RoutedEvent<Avalonia.Interactivity.RoutedEventArgs> ClickEvent = new();

                public event System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>? Click
                {
                    add => AddHandler(ClickEvent, value);
                    remove => RemoveHandler(ClickEvent, value);
                }
            }
        }

        """;
}
