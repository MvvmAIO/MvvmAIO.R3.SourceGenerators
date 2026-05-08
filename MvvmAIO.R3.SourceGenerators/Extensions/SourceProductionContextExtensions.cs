using MvvmAIO.R3.SourceGenerators.Models;

namespace MvvmAIO.R3.SourceGenerators.Extensions;

internal static class SourceProductionContextExtensions
{
    public static void ReportDiagnostics<T>(this Microsoft.CodeAnalysis.SourceProductionContext context, in Result<T> result)
        where T : class
    {
        foreach (var item in result.Diagnostics)
        {
            context.ReportDiagnostic(item.ToDiagnostic());
        }
    }
}
