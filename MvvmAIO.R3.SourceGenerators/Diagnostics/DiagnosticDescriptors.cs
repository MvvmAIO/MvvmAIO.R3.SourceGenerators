using Microsoft.CodeAnalysis;

namespace MvvmAIO.R3.SourceGenerators.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string Category = "MvvmAIO.R3.SourceGenerators";

    public static readonly DiagnosticDescriptor NonPartialType = new(
        id: "R3SG0001",
        title: "Containing type must be partial",
        messageFormat: "The type '{0}' must be declared partial for source generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidCommandMethodSignature = new(
        id: "R3SG1001",
        title: "Unsupported command method signature",
        messageFormat: "The method '{0}' signature is not supported by [R3Command].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEventDelegate = new(
        id: "R3SG2001",
        title: "Unsupported event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for observable generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
