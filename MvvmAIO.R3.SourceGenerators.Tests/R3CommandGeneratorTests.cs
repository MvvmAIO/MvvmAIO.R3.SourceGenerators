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
    public void Generates_command_property_for_void_method_with_parameter()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command]
                private void Delete(int id)
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("ReactiveCommand<int>", snapshot);
        Assert.Contains("DeleteCommand", snapshot);
        Assert.DoesNotContain("R3SG", snapshot.Split("Generated Sources:")[0]);
    }

    [Fact]
    public void Generates_command_property_for_async_task_method()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command]
                private System.Threading.Tasks.Task SaveAsync()
                {
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("ReactiveCommand", snapshot);
        Assert.Contains("SaveAsyncCommand", snapshot);
        Assert.Contains("ValueTask", snapshot);
        Assert.DoesNotContain("R3SG", snapshot.Split("Generated Sources:")[0]);
    }

    [Fact]
    public void Generates_command_property_for_async_task_of_T_method()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command]
                private System.Threading.Tasks.Task<string> FetchData(int pageIndex)
                {
                    return System.Threading.Tasks.Task.FromResult("data");
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("ReactiveCommand<int, string>", snapshot);
        Assert.Contains("FetchDataCommand", snapshot);
        Assert.DoesNotContain("R3SG", snapshot.Split("Generated Sources:")[0]);
    }

    [Fact]
    public void Supports_custom_command_name()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command(CommandName = "Submit")]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("Submit", snapshot);
        Assert.DoesNotContain("SaveCommand", snapshot);
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
