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

    public static readonly DiagnosticDescriptor CanExecuteMemberNotFound = new(
        id: "R3SG1002",
        title: "CanExecute member not found",
        messageFormat: "The CanExecute member '{0}' was not found on type '{1}' for method '{2}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CanExecuteMemberTypeMismatch = new(
        id: "R3SG1003",
        title: "CanExecute member type mismatch",
        messageFormat: "The CanExecute member '{0}' on type '{1}' must be R3.Observable<bool> or System.IObservable<bool>, but has type '{2}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateCommandPropertyName = new(
        id: "R3SG1004",
        title: "Duplicate command property name",
        messageFormat: "The command property '{0}' would be generated for both '{1}' and '{2}' on type '{3}'. Use distinct CommandName values.",
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

    public static readonly DiagnosticDescriptor InvalidFromEventHandlersDelegate = new(
        id: "R3SG2002",
        title: "FromEventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for FromEventHandlers (needs System.EventHandler, System.EventHandler<T>, or void delegate with (object, T) parameters).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
