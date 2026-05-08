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
namespace R3.ObservableEvents
{
internal static partial class ObservableEventsBootstrapExtensions
{
    public static NullEvents ObservableEvents<T>(this T source)
    {
        return default;
    }
}
}
""";

    public const string NullEvents = """
namespace R3.ObservableEvents
{
internal struct NullEvents
{
}
}
""";
}
