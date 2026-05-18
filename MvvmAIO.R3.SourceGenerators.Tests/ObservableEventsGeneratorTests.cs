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

    [Fact]
    public void Generates_FromEvents_wrapper_for_interface_type()
    {
        const string source = """
            namespace Demo;

            public interface INotifySomething
            {
                event System.EventHandler<System.EventArgs>? SomethingChanged;
            }

            public interface INotifyMore : INotifySomething
            {
                event System.Action? MoreChanged;
            }

            public static class Usage
            {
                public static void Run(INotifyMore s)
                {
                    _ = s.FromEvents().SomethingChanged;
                    _ = s.FromEvents().MoreChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("SomethingChanged", snapshot);
        Assert.Contains("MoreChanged", snapshot);
    }

    [Fact]
    public void Generates_FromEventHandlers_wrapper_for_interface_type()
    {
        const string source = """
            namespace Demo;

            public interface INotifyPropertyChanged
            {
                event System.EventHandler? PropertyChanged;
            }

            public static class Usage
            {
                public static void Run(INotifyPropertyChanged s)
                {
                    _ = s.FromEventHandlers().PropertyChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("PropertyChanged", snapshot);
        Assert.Contains("FromEventHandler", snapshot);
    }

    [Fact]
    public void Generates_FromEvents_wrapper_for_generic_class()
    {
        const string source = """
            namespace Demo;

            public class GenericSource<T>
            {
                public event System.Action<T>? ValueChanged;
            }

            public static class Usage
            {
                public static void Run(GenericSource<string> s)
                {
                    _ = s.FromEvents().ValueChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("ValueChanged", snapshot);
    }

    [Fact]
    public void Generates_FromEvents_wrapper_for_generic_constraints()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.Action? BaseChanged;
            }

            public interface IFirst
            {
                event System.EventHandler<System.EventArgs>? FirstChanged;
            }

            public interface ISecond
            {
                event System.Action<int>? SecondChanged;
            }

            public static class Usage
            {
                public static void Run<TSource>(TSource source)
                    where TSource : BaseSource, IFirst, ISecond
                {
                    _ = source.FromEvents().BaseChanged;
                    _ = source.FromEvents().FirstChanged;
                    _ = source.FromEvents().SecondChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("where TSource : global::Demo.BaseSource, global::Demo.IFirst, global::Demo.ISecond", snapshot);
        Assert.Contains("((global::Demo.BaseSource)_sender).BaseChanged", snapshot);
        Assert.Contains("((global::Demo.IFirst)_sender).FirstChanged", snapshot);
        Assert.Contains("((global::Demo.ISecond)_sender).SecondChanged", snapshot);
    }

    [Fact]
    public void Generates_FromEventHandlers_wrapper_for_generic_constraints()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.EventHandler<System.EventArgs>? BaseChanged;
            }

            public interface IFirst
            {
                event System.EventHandler<System.EventArgs>? FirstChanged;
            }

            public static class Usage
            {
                public static void Run<TSource>(TSource source)
                    where TSource : BaseSource, IFirst
                {
                    _ = source.FromEventHandlers().BaseChanged;
                    _ = source.FromEventHandlers().FirstChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new ObservableEventsGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("where TSource : global::Demo.BaseSource, global::Demo.IFirst", snapshot);
        Assert.Contains("global::R3.Observable.FromEventHandler<global::System.EventArgs>", snapshot);
        Assert.Contains("((global::Demo.BaseSource)_sender).BaseChanged", snapshot);
        Assert.Contains("((global::Demo.IFirst)_sender).FirstChanged", snapshot);
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
