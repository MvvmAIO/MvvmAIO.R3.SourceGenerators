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
namespace R3.ObservableEvents
{
internal static partial class ObservableEventsBootstrapExtensions
{
    public static NullEvents ObservableEvents<T>(this T source)
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

    public const string NullEvents = """
#nullable enable
namespace R3.ObservableEvents
{
internal struct NullEvents
{
}
}
""";
}
