using Microsoft.CodeAnalysis;
using MvvmAIO.R3.SourceGenerators.Tests.Infrastructure;
using Xunit;

namespace MvvmAIO.R3.SourceGenerators.Tests;

/// <summary>
/// Core generator scenarios executed for each Roslyn analyzer build (4031 / 4120 / 5000) via CI matrix properties.
/// </summary>
public sealed class RoslynMatrixCoreTests
{
    [Fact]
    public void Roslyn_build_generates_FromEvents_wrapper_for_action_event()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            CoreGeneratorScenarios.FromEventsActionEventSource,
            generators: [new ObservableEventsGenerator()]);

        CoreGeneratorScenarios.AssertFromEventsActionEvent(output);
    }

    [Fact]
    public void Roslyn_build_generates_R3Command_property_for_void_method_without_parameter()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            CoreGeneratorScenarios.R3CommandVoidNoParameterSource,
            generators: [new ObservableEventsGenerator(), new R3CommandGenerator()]);

        CoreGeneratorScenarios.AssertR3CommandVoidNoParameter(output);
    }

    [Fact]
    public void Roslyn_build_reports_diagnostic_when_containing_type_is_not_partial()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            CoreGeneratorScenarios.R3CommandNonPartialSource,
            generators: [new R3CommandGenerator()]);

        CoreGeneratorScenarios.AssertR3CommandNonPartialDiagnostic(output);
    }
}
