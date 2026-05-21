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

    [Fact]
    public void Generates_command_with_canexecute_member()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                private readonly R3.Observable<bool> _canSave = new R3.Observable<bool>(true);

                [MvvmAIO.R3.R3Command(CanExecute = nameof(_canSave))]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("ReactiveCommand", snapshot);
        Assert.Contains("SaveCommand", snapshot);
        Assert.Contains("_canSave", snapshot);
        Assert.DoesNotContain("R3SG", snapshot.Split("Generated Sources:")[0]);
    }

    [Fact]
    public void Generates_command_with_canexecute_and_parameter()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                private readonly R3.Observable<bool> _canDelete = new R3.Observable<bool>(true);

                [MvvmAIO.R3.R3Command(CanExecute = nameof(_canDelete))]
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
        Assert.Contains("_canDelete", snapshot);
        Assert.DoesNotContain("R3SG", snapshot.Split("Generated Sources:")[0]);
    }

    [Fact]
    public void Reports_diagnostic_when_canexecute_member_is_missing()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command(CanExecute = nameof(_missing))]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("R3SG1002", snapshot);
        Assert.DoesNotContain("SaveCommand", snapshot);
    }

    [Fact]
    public void Reports_diagnostic_when_canexecute_member_type_is_invalid()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                private readonly bool _canSave = true;

                [MvvmAIO.R3.R3Command(CanExecute = nameof(_canSave))]
                private void Save()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("R3SG1003", snapshot);
        Assert.DoesNotContain("SaveCommand", snapshot);
    }

    [Fact]
    public void Reports_diagnostic_when_command_property_names_collide()
    {
        const string source = """
            namespace Demo;

            public partial class ShellViewModel
            {
                [MvvmAIO.R3.R3Command(CommandName = "Run")]
                private void Save()
                {
                }

                [MvvmAIO.R3.R3Command(CommandName = "Run")]
                private void SaveAgain()
                {
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: new IIncrementalGenerator[] { new R3CommandGenerator() });
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("R3SG1004", snapshot);
        Assert.DoesNotContain("SaveCommand", snapshot);
    }
}
