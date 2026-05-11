using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace MvvmAIO.R3.SourceGenerators.Tests;

public sealed class R3CommandGeneratorTests
{
    [Fact]
    public Task Generates_command_property_for_void_method_without_parameter()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public Task Reports_diagnostic_when_containing_type_is_not_partial()
    {
        const string source = """
            namespace Demo;

            public class ShellViewModel
            {
                [MvvmAIO.R3.R3Command]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
