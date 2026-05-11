namespace MvvmAIO.R3.SourceGenerators;

internal static class GeneratorSources
{
    public const string R3CommandAttribute = """
using System;

namespace MvvmAIO.R3
{
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class R3CommandAttribute : Attribute
{
    public string? CommandName { get; set; }
}
} 
""";

    public const string ObservableEventsBootstrapExtensions = """
#nullable enable
namespace R3.SourceGenerators
{
internal static partial class ObservableEventsBootstrapExtensions
{
    public static NullEvents FromEvents(this object? source)
    {
        return default;
    }

    public static NullEvents FromEventHandlers(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEvents(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEvents(this object? source, object? routes, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromRoutedEventHandlers(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEventHandlers(this object? source, object? routes, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromAttachedRoutedEvent(this object? source, object? routedEvent, object? routes = null, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromAttachedRoutedEventHandler(this object? source, object? routedEvent, object? routes = null, bool handledEventsToo = false)
    {
        return default;
    }

    /// <summary>
    /// Dummy receiver for grouping static observable events (<c>ObservableEventsStatics</c> codegen).
    /// Call <see cref=""ObservableEventsStatics{T}(T?)"/>: for example <c>((MyType?)null).ObservableEventsStatics()</c>.
    /// </summary>
    public static NullEvents ObservableEventsStatics<T>(this T? source)
    {
        return default;
    }
}
}
""";

    /// <summary>Bootstrap emitted when static ObservableEvents generation is disabled (<c>OBS_*</c> / <c>ObservableEventsStatics</c> not emitted).</summary>
    public const string ObservableEventsBootstrapExtensionsInstanceOnly = """
#nullable enable
namespace R3.SourceGenerators
{
internal static partial class ObservableEventsBootstrapExtensions
{
    public static NullEvents FromEvents(this object? source)
    {
        return default;
    }

    public static NullEvents FromEventHandlers(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEvents(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEvents(this object? source, object? routes, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromRoutedEventHandlers(this object? source)
    {
        return default;
    }

    public static NullEvents FromRoutedEventHandlers(this object? source, object? routes, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromAttachedRoutedEvent(this object? source, object? routedEvent, object? routes = null, bool handledEventsToo = false)
    {
        return default;
    }

    public static NullEvents FromAttachedRoutedEventHandler(this object? source, object? routedEvent, object? routes = null, bool handledEventsToo = false)
    {
        return default;
    }
}
}
""";

    public const string NullEvents = """
#nullable enable
namespace R3.SourceGenerators
{
internal struct NullEvents
{
}
}
""";

    /// <summary>
    /// Empty partial so static call sites can reference <c>ObservableEventsStatics</c> before
    /// per-type codegen appends nested <c>OBS_*</c> types (cold compile).
    /// </summary>
    public const string ObservableEventsStaticsShell = """
#nullable enable
namespace R3.SourceGenerators;

public static partial class ObservableEventsStatics
{
}
""";
}
