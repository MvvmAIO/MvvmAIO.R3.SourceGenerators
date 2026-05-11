using System.Threading.Tasks;
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
}
