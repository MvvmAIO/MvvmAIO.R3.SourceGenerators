using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using MvvmAIO.R3.SourceGenerators.Diagnostics;
using MvvmAIO.R3.SourceGenerators.Extensions;

namespace MvvmAIO.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class ObservableEventsGenerator : IIncrementalGenerator
{
    private const string BootstrapExtensionsMetadataName = "R3.SourceGenerators.ObservableEventsBootstrapExtensions";
    private const string GeneratedNamespace = "R3.SourceGenerators";

    /// <summary>
    /// Entry name: instance <c>source.FromEvents()</c> (extension method under <c>R3.SourceGenerators</c>); static <c>ObservableEventsStatics.OBS_* .FromEvents</c> (property). Per-event streams are properties (ReactiveMarbles-style).
    /// </summary>
    private const string FromEventsEntryMethodName = "FromEvents";

    /// <summary>
    /// Entry name: instance <c>source.FromEventHandlers()</c> (uses <c>R3.Observable.FromEventHandler</c>).
    /// </summary>
    private const string FromEventHandlersEntryMethodName = "FromEventHandlers";

    /// <summary>
    /// Entry name: instance <c>source.FromRoutedEvents()</c> — WPF (<c>UseWPF</c>) only; emits streams for CLR instance events backed by a <c>RoutedEvent</c> field (<c>{EventName}Event</c>).
    /// </summary>
    private const string FromRoutedEventsEntryMethodName = "FromRoutedEvents";

    /// <summary>
    /// Entry name: instance <c>source.FromRoutedEventHandlers()</c> — WPF only; same routed-event filter as <see cref="FromRoutedEventsEntryMethodName"/> with <c>R3.Observable.FromEventHandler</c>.
    /// </summary>
    private const string FromRoutedEventHandlersEntryMethodName = "FromRoutedEventHandlers";

    private const string FromAttachedRoutedEventEntryMethodName = "FromAttachedRoutedEvent";
    private const string FromAttachedRoutedEventHandlerEntryMethodName = "FromAttachedRoutedEventHandler";

    /// <summary>
    /// When <see langword="false"/>, no <c>ObservableEventsStatics</c> / <c>OBS_*</c> / static-event wrappers are emitted and static <c>FromEvents</c> member accesses are not discovered.
    /// </summary>
    private const bool StaticObservableEventsGenerationEnabled = false;

    private enum ObservableEventsEntryKind
    {
        FromEvents,
        FromEventHandlers,
        FromRoutedEvents,
        FromRoutedEventHandlers,
        FromAttachedRoutedEvent,
        FromAttachedRoutedEventHandler,
    }

    /// <summary>
    /// Same as <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>, plus NRT <c>?</c> so emitted types match delegate/event signatures (<c>IncludeNullableReferenceTypeModifier</c>, <c>1 &lt;&lt; 6</c>; see dotnet/roslyn <c>SymbolDisplayMiscellaneousOptions</c> — not always present on older netstandard2 reference assemblies, so bitmask is spelled out).
    /// </summary>
    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    private static string QualifiedType(ITypeSymbol type) => type.ToDisplayString(FullyQualifiedNullableFormat);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "R3.SourceGenerators.ObservableEventsBootstrapExtensions.g.cs",
                SourceText.From(
                    StaticObservableEventsGenerationEnabled
                        ? GeneratorSources.ObservableEventsBootstrapExtensions
                        : GeneratorSources.ObservableEventsBootstrapExtensionsInstanceOnly,
                    Encoding.UTF8));
            ctx.AddSource("R3.SourceGenerators.NullEvents.g.cs", SourceText.From(GeneratorSources.NullEvents, Encoding.UTF8));
        });
        RegisterObservableEventsStaticsShellPostInit(context);

        var observableEventsCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => IsObservableEventsInstanceEntryInvocation(syntax)
                || (StaticObservableEventsGenerationEnabled && IsStaticFromEventsEntryMemberAccess(syntax)),
            static (syntaxContext, _) => syntaxContext.Node);

        var inputs = observableEventsCandidates.Collect()
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (triple, _) =>
            {
                var candidates = triple.Left.Left;
                var compilation = triple.Left.Right;
                var analyzerConfig = triple.Right;
                var useWpf = analyzerConfig.GlobalOptions.TryGetValue("build_property.UseWPF", out var useWpfRaw)
                    && (string.Equals(useWpfRaw, "true", System.StringComparison.OrdinalIgnoreCase)
                        || (bool.TryParse(useWpfRaw, out var useWpfBool) && useWpfBool));
                return (Candidates: candidates, Compilation: compilation, UseWpf: useWpf);
            });

        context.RegisterSourceOutput(inputs, static (spc, input) =>
        {
            var targets = CollectObservableEventTargets(input.Compilation, input.Candidates, input.UseWpf);
            foreach (var type in targets.FromEventsTypes)
            {
                var source = GenerateObservableSourceForType(type, input.Compilation, spc, ObservableEventsEntryKind.FromEvents, input.UseWpf);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{type.GetSafeHintName()}.FromEvents.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var type in targets.FromEventHandlersTypes)
            {
                var source = GenerateObservableSourceForType(type, input.Compilation, spc, ObservableEventsEntryKind.FromEventHandlers, input.UseWpf);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{type.GetSafeHintName()}.FromEventHandlers.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var type in targets.FromRoutedEventsTypes)
            {
                var source = GenerateObservableSourceForType(type, input.Compilation, spc, ObservableEventsEntryKind.FromRoutedEvents, input.UseWpf);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{type.GetSafeHintName()}.FromRoutedEvents.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var type in targets.FromRoutedEventHandlersTypes)
            {
                var source = GenerateObservableSourceForType(type, input.Compilation, spc, ObservableEventsEntryKind.FromRoutedEventHandlers, input.UseWpf);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{type.GetSafeHintName()}.FromRoutedEventHandlers.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var target in targets.FromAttachedRoutedEventsTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.FromAttachedRoutedEvent);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{target.ReceiverType.GetSafeHintName()}.FromAttachedRoutedEvent.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var target in targets.FromAttachedRoutedEventHandlersTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.FromAttachedRoutedEventHandler);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{target.ReceiverType.GetSafeHintName()}.FromAttachedRoutedEventHandler.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        });
    }

    private static void RegisterObservableEventsStaticsShellPostInit(IncrementalGeneratorInitializationContext context)
    {
#pragma warning disable CS0162 // Unreachable when StaticObservableEventsGenerationEnabled is compile-time false
        if (!StaticObservableEventsGenerationEnabled)
        {
            return;
        }

        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(
                "R3.SourceGenerators.ObservableEventsStatics.g.cs",
                SourceText.From(GeneratorSources.ObservableEventsStaticsShell, Encoding.UTF8)));
#pragma warning restore CS0162
    }

    private static bool IsObservableEventsInstanceEntryInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: not GenericNameSyntax,
                    Name.Identifier.ValueText: var methodName,
                },
            })
        {
            return false;
        }

        return methodName is FromEventsEntryMethodName
            or FromEventHandlersEntryMethodName
            or FromRoutedEventsEntryMethodName
            or FromRoutedEventHandlersEntryMethodName
            or FromAttachedRoutedEventEntryMethodName
            or FromAttachedRoutedEventHandlerEntryMethodName;
    }

    /// <summary>
    /// Matches <c>ObservableEventsStatics.OBS_<em>StableHint</em>.FromEvents</c> (static entry property), not <c>receiver.FromEvents()</c>.
    /// </summary>
    private static bool IsStaticFromEventsEntryMemberAccess(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax ma)
        {
            return false;
        }

        if (!string.Equals(ma.Name.Identifier.ValueText, FromEventsEntryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        // Exclude instance extension call shape: source.FromEvents()
        if (ma.Parent is InvocationExpressionSyntax inv && ReferenceEquals(inv.Expression, ma))
        {
            return false;
        }

        if (ma.Expression is not MemberAccessExpressionSyntax obsAccess)
        {
            return false;
        }

        if (obsAccess.Expression is not IdentifierNameSyntax outerId
            || !string.Equals(outerId.Identifier.ValueText, "ObservableEventsStatics", System.StringComparison.Ordinal))
        {
            return false;
        }

        return obsAccess.Name switch
        {
            SimpleNameSyntax sn => sn.Identifier.ValueText.StartsWith("OBS_", System.StringComparison.Ordinal),
            _ => false,
        };
    }

    private readonly struct ObservableEventTargetSets
    {
        public ObservableEventTargetSets(
            ImmutableArray<INamedTypeSymbol> fromEventsTypes,
            ImmutableArray<INamedTypeSymbol> fromEventHandlersTypes,
            ImmutableArray<INamedTypeSymbol> fromRoutedEventsTypes,
            ImmutableArray<INamedTypeSymbol> fromRoutedEventHandlersTypes,
            ImmutableArray<AttachedRoutedEventTarget> fromAttachedRoutedEventsTypes,
            ImmutableArray<AttachedRoutedEventTarget> fromAttachedRoutedEventHandlersTypes)
        {
            FromEventsTypes = fromEventsTypes;
            FromEventHandlersTypes = fromEventHandlersTypes;
            FromRoutedEventsTypes = fromRoutedEventsTypes;
            FromRoutedEventHandlersTypes = fromRoutedEventHandlersTypes;
            FromAttachedRoutedEventsTypes = fromAttachedRoutedEventsTypes;
            FromAttachedRoutedEventHandlersTypes = fromAttachedRoutedEventHandlersTypes;
        }

        public ImmutableArray<INamedTypeSymbol> FromEventsTypes { get; }
        public ImmutableArray<INamedTypeSymbol> FromEventHandlersTypes { get; }
        public ImmutableArray<INamedTypeSymbol> FromRoutedEventsTypes { get; }
        public ImmutableArray<INamedTypeSymbol> FromRoutedEventHandlersTypes { get; }
        public ImmutableArray<AttachedRoutedEventTarget> FromAttachedRoutedEventsTypes { get; }
        public ImmutableArray<AttachedRoutedEventTarget> FromAttachedRoutedEventHandlersTypes { get; }
    }

    private readonly struct AttachedRoutedEventTarget
    {
        public AttachedRoutedEventTarget(INamedTypeSymbol receiverType)
        {
            ReceiverType = receiverType;
        }

        public INamedTypeSymbol ReceiverType { get; }
    }

    private static ObservableEventTargetSets CollectObservableEventTargets(
        Compilation compilation,
        ImmutableArray<SyntaxNode> candidates,
        bool useWpf)
    {
        var bootstrapType = compilation.GetTypeByMetadataName(BootstrapExtensionsMetadataName);
        if (bootstrapType is null)
        {
            return new ObservableEventTargetSets(
                ImmutableArray<INamedTypeSymbol>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                ImmutableArray<AttachedRoutedEventTarget>.Empty,
                ImmutableArray<AttachedRoutedEventTarget>.Empty);
        }

        var fromEvents = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fromHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fromRoutedEvents = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fromRoutedHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fromAttachedRoutedEvents = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fromAttachedRoutedHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var useAvalonia = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1") is not null
            || compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent") is not null;

        foreach (var candidate in candidates)
        {
            if (candidate is InvocationExpressionSyntax invocation)
            {
                var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
                if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.Name == FromEventsEntryMethodName
                        && TryGetBootstrapObservableEventsExtensionTarget(
                            invocation,
                            semanticModel,
                            methodSymbol,
                            bootstrapType,
                            FromEventsEntryMethodName,
                            out var fromEventsTarget))
                    {
                        if (fromEventsTarget.IsGenericType)
                        {
                            fromEventsTarget = fromEventsTarget.OriginalDefinition;
                        }

                        fromEvents.Add(fromEventsTarget);
                    }
                    else if (methodSymbol.Name == FromEventHandlersEntryMethodName
                             && TryGetBootstrapObservableEventsExtensionTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 FromEventHandlersEntryMethodName,
                                 out var handlerTarget))
                    {
                        if (handlerTarget.IsGenericType)
                        {
                            handlerTarget = handlerTarget.OriginalDefinition;
                        }

                        fromHandlers.Add(handlerTarget);
                    }
                    else if ((useWpf || useAvalonia)
                             && methodSymbol.Name == FromRoutedEventsEntryMethodName
                             && TryGetBootstrapObservableEventsExtensionTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 FromRoutedEventsEntryMethodName,
                                 out var routedEventsTarget))
                    {
                        if (routedEventsTarget.IsGenericType)
                        {
                            routedEventsTarget = routedEventsTarget.OriginalDefinition;
                        }

                        fromRoutedEvents.Add(routedEventsTarget);
                    }
                    else if ((useWpf || useAvalonia)
                             && methodSymbol.Name == FromRoutedEventHandlersEntryMethodName
                             && TryGetBootstrapObservableEventsExtensionTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 FromRoutedEventHandlersEntryMethodName,
                                 out var routedHandlersTarget))
                    {
                        if (routedHandlersTarget.IsGenericType)
                        {
                            routedHandlersTarget = routedHandlersTarget.OriginalDefinition;
                        }

                        fromRoutedHandlers.Add(routedHandlersTarget);
                    }
                    else if (methodSymbol.Name == FromAttachedRoutedEventEntryMethodName
                             && TryGetBootstrapAttachedRoutedEventTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 FromAttachedRoutedEventEntryMethodName,
                                 out var attachedEventsReceiver))
                    {
                        if (attachedEventsReceiver.IsGenericType)
                        {
                            attachedEventsReceiver = attachedEventsReceiver.OriginalDefinition;
                        }

                        fromAttachedRoutedEvents.Add(attachedEventsReceiver);
                    }
                    else if (methodSymbol.Name == FromAttachedRoutedEventHandlerEntryMethodName
                             && TryGetBootstrapAttachedRoutedEventTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 FromAttachedRoutedEventHandlerEntryMethodName,
                                 out var attachedHandlersReceiver))
                    {
                        if (attachedHandlersReceiver.IsGenericType)
                        {
                            attachedHandlersReceiver = attachedHandlersReceiver.OriginalDefinition;
                        }

                        fromAttachedRoutedHandlers.Add(attachedHandlersReceiver);
                    }
                }

                continue;
            }

            if (StaticObservableEventsGenerationEnabled
                && candidate is MemberAccessExpressionSyntax staticFromEvents
                && IsStaticFromEventsEntryMemberAccess(staticFromEvents))
            {
                var semanticModel = compilation.GetSemanticModel(staticFromEvents.SyntaxTree);
                if (semanticModel.GetSymbolInfo(staticFromEvents).Symbol is { } staticSymbol
                    && TryGetTypeFromObservableEventsStaticsNested(staticSymbol, bootstrapType, compilation, out var staticTarget))
                {
                    if (staticTarget.IsGenericType)
                    {
                        staticTarget = staticTarget.OriginalDefinition;
                    }

                    fromEvents.Add(staticTarget);
                    continue;
                }

                // Cold compile: static entry property not bound until nested type exists.
                if (TryGetStaticObservableEventsTargetFromMemberAccess(compilation, staticFromEvents, out var syntaxOnlyStatic))
                {
                    if (syntaxOnlyStatic.IsGenericType)
                    {
                        syntaxOnlyStatic = syntaxOnlyStatic.OriginalDefinition;
                    }

                    fromEvents.Add(syntaxOnlyStatic);
                }
            }
        }

        static ImmutableArray<INamedTypeSymbol> Order(System.Collections.Generic.HashSet<INamedTypeSymbol> set) =>
            set
                .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .ToImmutableArray();

        static ImmutableArray<AttachedRoutedEventTarget> OrderAttached(System.Collections.Generic.HashSet<INamedTypeSymbol> set) =>
            set
                .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .Select(static t => new AttachedRoutedEventTarget(t))
                .ToImmutableArray();

        return new ObservableEventTargetSets(
            Order(fromEvents),
            Order(fromHandlers),
            Order(fromRoutedEvents),
            Order(fromRoutedHandlers),
            OrderAttached(fromAttachedRoutedEvents),
            OrderAttached(fromAttachedRoutedHandlers));
    }

    /// <summary>
    /// When semantic binding cannot resolve the static entry property yet, recover the declaring type from
    /// <c>ObservableEventsStatics.OBS_<em>StableHint</em>.FromEvents</c> syntax so generation still runs.
    /// </summary>
    private static bool TryGetStaticObservableEventsTargetFromMemberAccess(
        Compilation compilation,
        MemberAccessExpressionSyntax fromEventsAccess,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;
        if (fromEventsAccess.Expression is not MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "ObservableEventsStatics" },
                Name: SimpleNameSyntax staticHintNameSyntax,
            })
        {
            return false;
        }

        if (!string.Equals(fromEventsAccess.Name.Identifier.ValueText, FromEventsEntryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        var nestedId = staticHintNameSyntax.Identifier.ValueText;
        if (!nestedId.StartsWith("OBS_", System.StringComparison.Ordinal))
        {
            return false;
        }

        var hintStem = nestedId.Substring(4);
        return TryResolveNamedTypeByStableHint(compilation, hintStem, out namedType);
    }

    private static bool TryGetBootstrapObservableEventsExtensionTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        string entryMethodName,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;

        if (!string.Equals(methodSymbol.Name, entryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        static INamedTypeSymbol? FindBootstrapDeclaringType(IMethodSymbol m)
        {
            var decl = m.ReducedFrom ?? m;
            return decl.ContainingType?.OriginalDefinition as INamedTypeSymbol;
        }

        if (FindBootstrapDeclaringType(methodSymbol) is not { } declaring
            || !SymbolEqualityComparer.Default.Equals(declaring, bootstrapType.OriginalDefinition))
        {
            return false;
        }

        if (methodSymbol.TypeArguments.Length == 1 && methodSymbol.TypeArguments[0] is INamedTypeSymbol fromArgs)
        {
            namedType = fromArgs;
            return true;
        }

        // Reduced extension inference: FromEvents() on explicit receiver without TypeArguments surfaced on symbol.
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver })
        {
            if (semanticModel.GetTypeInfo(receiver).Type is INamedTypeSymbol receiverNamed)
            {
                namedType = receiverNamed;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBootstrapAttachedRoutedEventTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        string entryMethodName,
        out INamedTypeSymbol receiverType)
    {
        receiverType = null!;

        if (!string.Equals(methodSymbol.Name, entryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        var declaration = methodSymbol.ReducedFrom ?? methodSymbol;
        if (declaration.ContainingType?.OriginalDefinition is not { } declaring
            || !SymbolEqualityComparer.Default.Equals(declaring, bootstrapType.OriginalDefinition))
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver })
        {
            return false;
        }

        if (semanticModel.GetTypeInfo(receiver).Type is INamedTypeSymbol receiverNamed)
        {
            receiverType = receiverNamed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses <c>ObservableEventsStatics.OBS_<em>StableHint</em>.FromEvents</c> from semantic model (expects the static entry property on the nested partial class).
    /// </summary>
    private static bool TryGetTypeFromObservableEventsStaticsNested(
        ISymbol symbol,
        INamedTypeSymbol bootstrapType,
        Compilation compilation,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;

        if (symbol.Name != FromEventsEntryMethodName)
        {
            return false;
        }

        if (symbol is IMethodSymbol methodSymbol)
        {
            if (methodSymbol.ReducedFrom is not null || methodSymbol.IsExtensionMethod)
            {
                return false;
            }
        }
        else if (symbol is not IPropertySymbol)
        {
            return false;
        }

        var nested = symbol.ContainingType;
        var outer = nested?.ContainingType;
        if (nested is null || outer is null)
        {
            return false;
        }

        if (!string.Equals(outer.Name, "ObservableEventsStatics", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(outer.ContainingNamespace, bootstrapType.ContainingNamespace))
        {
            return false;
        }

        if (!nested.Name.StartsWith("OBS_", System.StringComparison.Ordinal))
        {
            return false;
        }

        var hintStem = nested.Name.Substring(4);
        return TryResolveNamedTypeByStableHint(compilation, hintStem, out namedType);
    }

    /// <remarks>
    /// Must match identifiers produced via <see cref="INamedTypeSymbolExtensions.GetSafeHintName"/>.
    /// </remarks>
    private static bool TryResolveNamedTypeByStableHint(Compilation compilation, string hintStem, out INamedTypeSymbol namedType)
    {
        namedType = null!;
        foreach (var candidate in EnumerateNamedTypesIncludingNested(compilation.GlobalNamespace))
        {
            if (!string.Equals(candidate.GetSafeHintName(), hintStem, System.StringComparison.Ordinal))
            {
                continue;
            }

            namedType = candidate;
            return true;
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypesIncludingNested(INamespaceSymbol root)
    {
        foreach (var member in root.GetNamespaceMembers())
        {
            foreach (var t in EnumerateNamedTypesIncludingNested(member))
            {
                yield return t;
            }
        }

        foreach (var named in root.GetTypeMembers())
        {
            foreach (var t in EnumerateSelfAndNestedNamedTypes(named))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateSelfAndNestedNamedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in EnumerateSelfAndNestedNamedTypes(nested))
            {
                yield return t;
            }
        }
    }

    /// <summary>
    /// Public instance events declared on <paramref name="type"/> and its non-generic class base types (excluding <see cref="object"/>),
    /// with derived declarations taking precedence over the same event name on a base type.
    /// When <paramref name="type"/> is an interface, events declared on the interface and all its base interfaces are collected.
    /// </summary>
    private static IEnumerable<IEventSymbol> GetPublicInstanceEventsFromTypeAndBases(INamedTypeSymbol type)
    {
        var byName = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);

        if (type.TypeKind == TypeKind.Interface)
        {
            // Collect events from the interface itself and all base interfaces.
            foreach (var iface in new[] { type }.Concat(type.AllInterfaces))
            {
                foreach (var evt in iface.GetMembers().OfType<IEventSymbol>()
                             .Where(static e => !e.IsStatic))
                {
                    if (!byName.ContainsKey(evt.Name))
                    {
                        byName[evt.Name] = evt;
                    }
                }
            }
        }
        else
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.SpecialType == SpecialType.System_Object)
                {
                    break;
                }

                if (current.TypeKind != TypeKind.Class)
                {
                    continue;
                }

                foreach (var evt in current.GetMembers().OfType<IEventSymbol>()
                             .Where(static e => e is { IsStatic: false, DeclaredAccessibility: Accessibility.Public }))
                {
                    if (!byName.ContainsKey(evt.Name))
                    {
                        byName[evt.Name] = evt;
                    }
                }
            }
        }

        return byName.Values.OrderBy(static e => e.Name, System.StringComparer.Ordinal);
    }

    private static string GenerateObservableSourceForType(
        INamedTypeSymbol type,
        Compilation compilation,
        SourceProductionContext context,
        ObservableEventsEntryKind entryKind,
        bool useWpf)
    {
        var unit = SyntaxFactory.CompilationUnit()
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")));

        var members = new List<MemberDeclarationSyntax>();
        if (!type.IsStatic)
        {
            members.Add(CreateExtensionsClass(type, entryKind));
            members.Add(CreateWrapperClass(type, compilation, context, entryKind, useWpf));
            if (entryKind is ObservableEventsEntryKind.FromRoutedEvents or ObservableEventsEntryKind.FromRoutedEventHandlers
                && HasAvaloniaRoutedClrEvents(type, compilation))
            {
                members.Add(CreateAvaloniaRoutedExtensionsClass(type, entryKind));
                members.Add(CreateAvaloniaRoutedWrapperClass(type, compilation, context, entryKind));
            }
        }

        // Static OBS_* codegen stays paired with instance FromEvents only (same lookup semantics today).
        if (StaticObservableEventsGenerationEnabled
            && entryKind == ObservableEventsEntryKind.FromEvents
            && HasPublicStaticObservableEvents(type))
        {
            members.Add(CreateObservableEventsStaticsEntry(type));
            members.Add(CreateStaticWrapperClass(type, context));
        }

        if (members.Count == 0)
        {
            return string.Empty;
        }

        var nsMember = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(GeneratedNamespace))
            .AddMembers(members.ToArray());
        unit = unit.AddMembers(nsMember);

        // Analyzer-generated translation units require an explicit `#nullable` directive before NRT punctuation (CS8669).
        return "#nullable enable\n\n" + unit.NormalizeWhitespace().ToFullString();
    }

    private static string GenerateAttachedRoutedEventSourceForTarget(
        AttachedRoutedEventTarget target,
        ObservableEventsEntryKind entryKind)
    {
        var receiverType = QualifiedType(target.ReceiverType);
        var observableMethod = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
            ? FromAttachedRoutedEventEntryMethodName
            : FromAttachedRoutedEventHandlerEntryMethodName;
        var returnType = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
            ? "global::R3.Observable<TEventArgs>"
            : "global::R3.Observable<(object? sender, TEventArgs e)>";
        var expression = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
            ? "global::R3.Observable.FromEvent<global::System.EventHandler<TEventArgs>, TEventArgs>(h => (sender, e) => h(e), h => source.AddHandler(routedEvent, h, routes, handledEventsToo), h => source.RemoveHandler(routedEvent, h), default)"
            : "global::R3.Observable.FromEventHandler<TEventArgs>(h => source.AddHandler(routedEvent, h, routes, handledEventsToo), h => source.RemoveHandler(routedEvent, h), default)";

        var source = $$"""
            #nullable enable

            using R3;

            namespace R3.SourceGenerators;

            internal static partial class ObservableEventsBootstrapExtensions
            {
                public static {{returnType}} {{observableMethod}}<TEventArgs>(
                    this {{receiverType}} source,
                    global::Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    global::Avalonia.Interactivity.RoutingStrategies routes = global::Avalonia.Interactivity.RoutingStrategies.Direct | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
                    bool handledEventsToo = false)
                    where TEventArgs : global::Avalonia.Interactivity.RoutedEventArgs
                    => {{expression}};
            }
            """;

        return source;
    }

    private static ClassDeclarationSyntax CreateExtensionsClass(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
    {
        // Must match post-init class name in GeneratorSources.ObservableEventsBootstrapExtensions*:
        // concrete FromEvents(this T) merges into the same partial as FromEvents(this object?) so the IDE
        // resolves the specific overload (wrapper with event properties), not the NullEvents fallback.
        var entryMethod = entryKind switch
        {
            ObservableEventsEntryKind.FromEvents => CreateFromEventsMethod(type),
            ObservableEventsEntryKind.FromEventHandlers => CreateFromEventHandlersMethod(type),
            ObservableEventsEntryKind.FromRoutedEvents => CreateFromRoutedEventsMethod(type),
            ObservableEventsEntryKind.FromRoutedEventHandlers => CreateFromRoutedEventHandlersMethod(type),
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };
        return SyntaxFactory.ClassDeclaration("ObservableEventsBootstrapExtensions")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(entryMethod);
    }

    private static MethodDeclarationSyntax CreateFromEventsMethod(INamedTypeSymbol type)
    {
        var typeName = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var wrapperNameBase = GetWrapperName(type, ObservableEventsEntryKind.FromEvents);
        var returnTypeName = type.IsGenericType
            ? $"{wrapperNameBase}<{string.Join(", ", type.TypeParameters.Select(tp => tp.Name))}>"
            : wrapperNameBase;
        var returnType = SyntaxFactory.ParseTypeName(returnTypeName);

        var method = SyntaxFactory.MethodDeclaration(returnType, FromEventsEntryMethodName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("source"))
                    .WithType(typeName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.ObjectCreationExpression(returnType)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("source")))))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        // Add type parameters if the original type is generic
        if (type.IsGenericType)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(tp =>
                        SyntaxFactory.TypeParameter(tp.Name))));
            method = method.WithTypeParameterList(typeParameters);
        }

        return method;
    }

    private static MethodDeclarationSyntax CreateFromEventHandlersMethod(INamedTypeSymbol type)
    {
        var typeName = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var wrapperNameBase = GetWrapperName(type, ObservableEventsEntryKind.FromEventHandlers);
        var returnTypeName = type.IsGenericType
            ? $"{wrapperNameBase}<{string.Join(", ", type.TypeParameters.Select(tp => tp.Name))}>"
            : wrapperNameBase;
        var returnType = SyntaxFactory.ParseTypeName(returnTypeName);

        var method = SyntaxFactory.MethodDeclaration(returnType, FromEventHandlersEntryMethodName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("source"))
                    .WithType(typeName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.ObjectCreationExpression(returnType)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("source")))))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        if (type.IsGenericType)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(tp =>
                        SyntaxFactory.TypeParameter(tp.Name))));
            method = method.WithTypeParameterList(typeParameters);
        }

        return method;
    }

    private static MethodDeclarationSyntax CreateFromRoutedEventsMethod(INamedTypeSymbol type)
    {
        var typeName = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var wrapperNameBase = GetWrapperName(type, ObservableEventsEntryKind.FromRoutedEvents);
        var returnTypeName = type.IsGenericType
            ? $"{wrapperNameBase}<{string.Join(", ", type.TypeParameters.Select(tp => tp.Name))}>"
            : wrapperNameBase;
        var returnType = SyntaxFactory.ParseTypeName(returnTypeName);

        var method = SyntaxFactory.MethodDeclaration(returnType, FromRoutedEventsEntryMethodName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("source"))
                    .WithType(typeName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.ObjectCreationExpression(returnType)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("source")))))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        if (type.IsGenericType)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(tp =>
                        SyntaxFactory.TypeParameter(tp.Name))));
            method = method.WithTypeParameterList(typeParameters);
        }

        return method;
    }

    private static MethodDeclarationSyntax CreateFromRoutedEventHandlersMethod(INamedTypeSymbol type)
    {
        var typeName = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var wrapperNameBase = GetWrapperName(type, ObservableEventsEntryKind.FromRoutedEventHandlers);
        var returnTypeName = type.IsGenericType
            ? $"{wrapperNameBase}<{string.Join(", ", type.TypeParameters.Select(tp => tp.Name))}>"
            : wrapperNameBase;
        var returnType = SyntaxFactory.ParseTypeName(returnTypeName);

        var method = SyntaxFactory.MethodDeclaration(returnType, FromRoutedEventHandlersEntryMethodName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("source"))
                    .WithType(typeName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword)))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.ObjectCreationExpression(returnType)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("source")))))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        if (type.IsGenericType)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(tp =>
                        SyntaxFactory.TypeParameter(tp.Name))));
            method = method.WithTypeParameterList(typeParameters);
        }

        return method;
    }

    private static ClassDeclarationSyntax CreateAvaloniaRoutedExtensionsClass(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
    {
        var methodName = entryKind == ObservableEventsEntryKind.FromRoutedEvents
            ? FromRoutedEventsEntryMethodName
            : FromRoutedEventHandlersEntryMethodName;
        var wrapperName = GetAvaloniaRoutedWrapperName(type, entryKind);
        var methodSource = $$"""
            public static {{wrapperName}} {{methodName}}(
                this {{QualifiedType(type)}} source,
                global::Avalonia.Interactivity.RoutingStrategies routes,
                bool handledEventsToo = false)
                => new {{wrapperName}}(source, routes, handledEventsToo);
            """;

        return SyntaxFactory.ClassDeclaration("ObservableEventsBootstrapExtensions")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(SyntaxFactory.ParseMemberDeclaration(methodSource)!);
    }

    private static ClassDeclarationSyntax CreateWrapperClass(
        INamedTypeSymbol type,
        Compilation compilation,
        SourceProductionContext context,
        ObservableEventsEntryKind entryKind,
        bool useWpf)
    {
        var wrapperName = GetWrapperName(type, entryKind);
        var classDeclaration = SyntaxFactory.ClassDeclaration(wrapperName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));

        // Add type parameters if the original type is generic
        if (type.IsGenericType)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(tp =>
                        SyntaxFactory.TypeParameter(tp.Name))));
            classDeclaration = classDeclaration.WithTypeParameterList(typeParameters);
        }

        var senderType = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(senderType)
                    .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var ctor = SyntaxFactory.ConstructorDeclaration(wrapperName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender"))
                    .WithType(senderType))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.ParseStatement("_sender = sender;")));

        var members = new List<MemberDeclarationSyntax> { field, ctor };
        foreach (var evt in GetPublicInstanceEventsFromTypeAndBases(type))
        {
            if (entryKind is ObservableEventsEntryKind.FromRoutedEvents or ObservableEventsEntryKind.FromRoutedEventHandlers)
            {
                if (!IsRoutedClrEvent(evt, compilation, useWpf))
                {
                    continue;
                }
            }

            var eventTarget = $"_sender.{evt.Name}";
            if (entryKind is ObservableEventsEntryKind.FromEvents or ObservableEventsEntryKind.FromRoutedEvents)
            {
                if (TryCreateEventObservableProperty(evt, eventTarget, context, out var eventProperty))
                {
                    members.Add(eventProperty);
                }
            }
            else if (TryCreateEventHandlerObservableProperty(evt, eventTarget, compilation, context, out var handlerProperty))
            {
                members.Add(handlerProperty);
            }
        }

        return classDeclaration.AddMembers(members.ToArray());
    }

    private static ClassDeclarationSyntax CreateAvaloniaRoutedWrapperClass(
        INamedTypeSymbol type,
        Compilation compilation,
        SourceProductionContext context,
        ObservableEventsEntryKind entryKind)
    {
        var wrapperName = GetAvaloniaRoutedWrapperName(type, entryKind);
        var members = new List<string>
        {
            $"private readonly {QualifiedType(type)} _sender;",
            "private readonly global::Avalonia.Interactivity.RoutingStrategies _routes;",
            "private readonly bool _handledEventsToo;",
            $$"""
            internal {{wrapperName}}({{QualifiedType(type)}} sender, global::Avalonia.Interactivity.RoutingStrategies routes, bool handledEventsToo)
            {
                _sender = sender;
                _routes = routes;
                _handledEventsToo = handledEventsToo;
            }
            """,
        };

        foreach (var evt in GetPublicInstanceEventsFromTypeAndBases(type))
        {
            if (!TryGetAvaloniaRoutedClrEventField(evt, compilation, out var routedEventField, out var eventArgsType))
            {
                continue;
            }

            var propertySource = entryKind == ObservableEventsEntryKind.FromRoutedEvents
                ? CreateAvaloniaRoutedEventObservablePropertySource(evt, routedEventField, eventArgsType, context)
                : CreateAvaloniaRoutedEventHandlerObservablePropertySource(evt, routedEventField, eventArgsType, context);
            if (!string.IsNullOrWhiteSpace(propertySource))
            {
                members.Add(propertySource);
            }
        }

        var classSource = $$"""
            internal class {{wrapperName}}
            {
            {{IndentMembers(members)}}
            }
            """;

        return (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(classSource)!;
    }

    private static string IndentMembers(IEnumerable<string> members)
    {
        return string.Join(
            "\n\n",
            members.Select(static member => string.Join(
                "\n",
                member.Trim().Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None)
                    .Select(static line => "    " + line))));
    }

    private static bool IsRoutedClrEvent(IEventSymbol evt, Compilation compilation, bool includeWpf)
    {
        var fieldName = evt.Name + "Event";
        for (var current = evt.ContainingType; current is not null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object)
            {
                break;
            }

            foreach (var member in current.GetMembers(fieldName))
            {
                if (member is not IFieldSymbol field || !field.IsStatic || field.IsImplicitlyDeclared)
                {
                    continue;
                }

                var fieldType = field.Type.WithNullableAnnotation(NullableAnnotation.None);
                if ((includeWpf && IsWpfRoutedEventType(fieldType, compilation))
                    || IsAvaloniaRoutedEventType(fieldType, compilation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasAvaloniaRoutedClrEvents(INamedTypeSymbol type, Compilation compilation)
    {
        return GetPublicInstanceEventsFromTypeAndBases(type)
            .Any(evt => TryGetAvaloniaRoutedClrEventField(evt, compilation, out _, out _));
    }

    private static bool TryGetAvaloniaRoutedClrEventField(
        IEventSymbol evt,
        Compilation compilation,
        out IFieldSymbol routedEventField,
        out INamedTypeSymbol eventArgsType)
    {
        routedEventField = null!;
        eventArgsType = null!;

        var routedEventType = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
        if (routedEventType is null)
        {
            return false;
        }

        var fieldName = evt.Name + "Event";
        for (var current = evt.ContainingType; current is not null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object)
            {
                break;
            }

            foreach (var member in current.GetMembers(fieldName))
            {
                if (member is not IFieldSymbol field
                    || !field.IsStatic
                    || field.IsImplicitlyDeclared
                    || field.Type is not INamedTypeSymbol fieldType
                    || !SymbolEqualityComparer.Default.Equals(fieldType.OriginalDefinition, routedEventType)
                    || fieldType.TypeArguments.Length != 1
                    || fieldType.TypeArguments[0] is not INamedTypeSymbol argsType)
                {
                    continue;
                }

                routedEventField = field;
                eventArgsType = argsType;
                return true;
            }
        }

        return false;
    }

    private static string CreateAvaloniaRoutedEventObservablePropertySource(
        IEventSymbol evt,
        IFieldSymbol routedEventField,
        INamedTypeSymbol eventArgsType,
        SourceProductionContext context)
    {
        var eventArgs = QualifiedType(eventArgsType);
        var eventField = $"{QualifiedType(routedEventField.ContainingType)}.{routedEventField.Name}";
        var eventCref = $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
        return $$"""
            /// <summary>
            /// <inheritdoc cref="{{eventCref}}" />
            /// </summary>
            public global::R3.Observable<{{eventArgs}}> {{evt.Name}} => global::R3.Observable.FromEvent<global::System.EventHandler<{{eventArgs}}>, {{eventArgs}}>(h => (sender, e) => h(e), h => _sender.AddHandler({{eventField}}, h, _routes, _handledEventsToo), h => _sender.RemoveHandler({{eventField}}, h), default);
            """;
    }

    private static string CreateAvaloniaRoutedEventHandlerObservablePropertySource(
        IEventSymbol evt,
        IFieldSymbol routedEventField,
        INamedTypeSymbol eventArgsType,
        SourceProductionContext context)
    {
        var eventArgs = QualifiedType(eventArgsType);
        var eventField = $"{QualifiedType(routedEventField.ContainingType)}.{routedEventField.Name}";
        var eventCref = $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
        return $$"""
            /// <summary>
            /// <inheritdoc cref="{{eventCref}}" />
            /// </summary>
            public global::R3.Observable<(object? sender, {{eventArgs}} e)> {{evt.Name}} => global::R3.Observable.FromEventHandler<{{eventArgs}}>(h => _sender.AddHandler({{eventField}}, h, _routes, _handledEventsToo), h => _sender.RemoveHandler({{eventField}}, h), default);
            """;
    }

    private static bool IsWpfRoutedEventType(ITypeSymbol type, Compilation compilation)
    {
        var routedEventType = compilation.GetTypeByMetadataName("System.Windows.RoutedEvent");
        return routedEventType is not null
            && SymbolEqualityComparer.Default.Equals(type.WithNullableAnnotation(NullableAnnotation.None), routedEventType);
    }

    private static bool IsAvaloniaRoutedEventType(ITypeSymbol type, Compilation compilation)
    {
        var nonGeneric = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent");
        if (nonGeneric is not null
            && SymbolEqualityComparer.Default.Equals(type.WithNullableAnnotation(NullableAnnotation.None), nonGeneric))
        {
            return true;
        }

        var generic = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
        return generic is not null
            && type is INamedTypeSymbol named
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, generic);
    }

    private static bool HasPublicStaticObservableEvents(INamedTypeSymbol type)
        => type.GetMembers().OfType<IEventSymbol>().Any(static e =>
            e is { IsStatic: true, DeclaredAccessibility: Accessibility.Public });

    private static ClassDeclarationSyntax CreateObservableEventsStaticsEntry(INamedTypeSymbol type)
    {
        var nestedName = GetStaticObservableEventsNestedId(type);
        var returnType = SyntaxFactory.ParseTypeName(GetStaticWrapperName(type));

        var wrapperInstance = SyntaxFactory.ObjectCreationExpression(returnType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var observableEntry = SyntaxFactory.PropertyDeclaration(returnType, FromEventsEntryMethodName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(wrapperInstance))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        var nested = SyntaxFactory.ClassDeclaration(nestedName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(observableEntry);

        return SyntaxFactory.ClassDeclaration("ObservableEventsStatics")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(nested);
    }

    private static ClassDeclarationSyntax CreateStaticWrapperClass(INamedTypeSymbol type, SourceProductionContext context)
    {
        var ctor = SyntaxFactory.ConstructorDeclaration(GetStaticWrapperName(type))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .WithBody(SyntaxFactory.Block());

        var members = new List<MemberDeclarationSyntax> { ctor };

        foreach (var evt in type.GetMembers().OfType<IEventSymbol>()
                     .Where(static e => e is { IsStatic: true, DeclaredAccessibility: Accessibility.Public }))
        {
            var eventTarget =
                $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
            if (TryCreateEventObservableProperty(evt, eventTarget, context, out var eventProperty))
            {
                members.Add(eventProperty);
            }
        }

        return SyntaxFactory.ClassDeclaration(GetStaticWrapperName(type))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddMembers(members.ToArray());
    }

    private static bool TryCreateEventObservableProperty(
        IEventSymbol evt,
        string eventAccessorExpression,
        SourceProductionContext context,
        out PropertyDeclarationSyntax property)
    {
        property = null!;
        if (evt.Type is not INamedTypeSymbol delegateType || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke)
        {
            ReportInvalidDelegate(evt, context);
            return false;
        }

        if (!invoke.ReturnsVoid)
        {
            ReportInvalidDelegate(evt, context);
            return false;
        }

        var returnTypeStr = GetObservableReturnType(invoke.Parameters);
        var expressionText = BuildFromEventObservableExpression(delegateType, invoke.Parameters, eventAccessorExpression);
        if (expressionText is null)
        {
            ReportInvalidDelegate(evt, context);
            return false;
        }

        var eventCref = $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
        var docXml = SyntaxFactory.ParseLeadingTrivia(
            $"/// <summary>\n" +
            $"/// <inheritdoc cref=\"{eventCref}\" />\n" +
            $"/// </summary>\n");
        var parsedExpression = SyntaxFactory.ParseExpression(expressionText);
        property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(returnTypeStr), evt.Name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithLeadingTrivia(docXml)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(parsedExpression))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        return true;
    }

    private static bool TryCreateEventHandlerObservableProperty(
        IEventSymbol evt,
        string eventAccessorExpression,
        Compilation compilation,
        SourceProductionContext context,
        out PropertyDeclarationSyntax property)
    {
        property = null!;
        if (evt.Type is not INamedTypeSymbol delegateType || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke)
        {
            ReportInvalidFromEventHandlersDelegate(evt, context);
            return false;
        }

        if (!invoke.ReturnsVoid)
        {
            ReportInvalidFromEventHandlersDelegate(evt, context);
            return false;
        }

        string expressionText;
        string returnTypeStr;

        if (IsClassicSystemEventHandler(delegateType, compilation, out var genericEventArgs))
        {
            expressionText = genericEventArgs is null
                ? $"global::R3.Observable.FromEventHandler(h => {eventAccessorExpression} += h, h => {eventAccessorExpression} -= h, default)"
                : $"global::R3.Observable.FromEventHandler<{QualifiedType(genericEventArgs)}>(h => {eventAccessorExpression} += h, h => {eventAccessorExpression} -= h, default)";

            returnTypeStr = genericEventArgs is null
                ? "global::R3.Observable<(object? sender, global::System.EventArgs e)>"
                : $"global::R3.Observable<(object? sender, {QualifiedType(genericEventArgs)} e)>";
        }
        else if (IsLegacySenderReceiverDelegate(delegateType, invoke, compilation))
        {
            expressionText = BuildSenderArgsTupleFromEventExpression(delegateType, invoke.Parameters, eventAccessorExpression);
            returnTypeStr = GetFromEventHandlersSenderReceiverReturnType(invoke.Parameters);
        }
        else
        {
            ReportInvalidFromEventHandlersDelegate(evt, context);
            return false;
        }

        var eventCref = $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
        var docXml = SyntaxFactory.ParseLeadingTrivia(
            $"/// <summary>\n" +
            $"/// <inheritdoc cref=\"{eventCref}\" />\n" +
            $"/// </summary>\n");
        property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(returnTypeStr), evt.Name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithLeadingTrivia(docXml)
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(expressionText)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        return true;
    }

    /// <summary>
    /// Custom <c>void (object, TSecond)</c> delegate excluding <c>System.EventHandler</c> / <c>System.EventHandler&lt;T&gt;</c> (those use <c>Observable.FromEventHandler</c>), implemented with <c>R3.Observable.FromEvent</c>.
    /// </summary>
    private static bool IsLegacySenderReceiverDelegate(INamedTypeSymbol delegateType, IMethodSymbol invoke, Compilation compilation)
    {
        if (invoke.Parameters.Length != 2)
        {
            return false;
        }

        if (invoke.Parameters[0].RefKind != RefKind.None || invoke.Parameters[1].RefKind != RefKind.None)
        {
            return false;
        }

        if (!IsDeclaredObject(invoke.Parameters[0].Type, compilation))
        {
            return false;
        }

        if (IsClassicSystemEventHandler(delegateType, compilation, out _))
        {
            return false;
        }

        return true;
    }

    private static bool IsDeclaredObject(ITypeSymbol type, Compilation compilation)
    {
        return SymbolEqualityComparer.Default.Equals(
            type.WithNullableAnnotation(NullableAnnotation.None),
            compilation.GetSpecialType(SpecialType.System_Object));
    }

    /// <remarks>Second generic argument to FromEvent uses both parameter types so the observable element is <c>(object, TSecond)</c>, surfaced as named <c>(object? sender, TSecond e)</c> on the property.</remarks>
    private static string BuildSenderArgsTupleFromEventExpression(
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters,
        string eventAccessorExpression)
    {
        var eventType = QualifiedType(delegateType);
        var p0 = QualifiedType(parameters[0].Type);
        var p1 = QualifiedType(parameters[1].Type);
        return $"global::R3.Observable.FromEvent<{eventType}, ({p0}, {p1})>(h => (sender, e) => h((sender, e)), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, default)";
    }

    private static string GetFromEventHandlersSenderReceiverReturnType(ImmutableArray<IParameterSymbol> parameters)
    {
        var first = QualifiedType(parameters[0].Type);
        var second = QualifiedType(parameters[1].Type);
        return $"global::R3.Observable<({first} sender, {second} e)>";
    }

    /// <returns><see langword="null"/> for non-generic <c>System.EventHandler</c>; otherwise the generic event-args type.</returns>
    private static bool IsClassicSystemEventHandler(INamedTypeSymbol delegateType, Compilation compilation, out INamedTypeSymbol? genericEventArgs)
    {
        genericEventArgs = null;
        var nonGeneric = compilation.GetTypeByMetadataName("System.EventHandler");
        var genericDef = compilation.GetTypeByMetadataName("System.EventHandler`1");
        if (nonGeneric is null || genericDef is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, nonGeneric))
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, genericDef)
            && delegateType.TypeArguments.Length == 1
            && delegateType.TypeArguments[0] is INamedTypeSymbol tArg)
        {
            genericEventArgs = tArg;
            return true;
        }

        return false;
    }

    private static string? BuildFromEventObservableExpression(
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters,
        string eventAccessorExpression)
    {
        var eventType = QualifiedType(delegateType);
        if (parameters.Length == 0)
        {
            return
                $"global::R3.Observable.FromEvent<{eventType}, global::R3.Unit>(h => () => h(global::R3.Unit.Default), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, default)";
        }

        if (parameters.Length == 1)
        {
            var t1 = QualifiedType(parameters[0].Type);
            return
                $"global::R3.Observable.FromEvent<{eventType}, {t1}>(h => arg1 => h(arg1), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, default)";
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var eventArgsType = QualifiedType(parameters[1].Type);
            return
                $"global::R3.Observable.FromEvent<{eventType}, {eventArgsType}>(h => (sender, e) => h(e), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, default)";
        }

        var tupleType = $"({string.Join(", ", parameters.Select(static p => QualifiedType(p.Type)))})";
        var tupleArgs = string.Join(", ", parameters.Select((_, i) => $"arg{i + 1}"));
        return
            $"global::R3.Observable.FromEvent<{eventType}, {tupleType}>(h => ({tupleArgs}) => h(({tupleArgs})), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, default)";
    }

    private static string GetObservableReturnType(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
        {
            return "global::R3.Observable<global::R3.Unit>";
        }

        if (parameters.Length == 1)
        {
            var t1 = QualifiedType(parameters[0].Type);
            return $"global::R3.Observable<{t1}>";
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var eventArgsType = QualifiedType(parameters[1].Type);
            return $"global::R3.Observable<{eventArgsType}>";
        }

        var tupleType = $"({string.Join(", ", parameters.Select(static p => QualifiedType(p.Type)))})";
        return $"global::R3.Observable<{tupleType}>";
    }

    private static string GetStaticObservableEventsNestedId(INamedTypeSymbol type) => $"OBS_{type.GetSafeHintName()}";

    private static string GetStaticWrapperName(INamedTypeSymbol type) =>
        $"{GetTypeUniqueIdentifier(type)}StaticFromEventObservable";

    private static string GetWrapperName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.FromEvents => $"{GetTypeUniqueIdentifier(type)}FromEventObservable",
            ObservableEventsEntryKind.FromEventHandlers => $"{GetTypeUniqueIdentifier(type)}FromEventHandlerObservable",
            ObservableEventsEntryKind.FromRoutedEvents => $"{GetTypeUniqueIdentifier(type)}FromRoutedEventObservable",
            ObservableEventsEntryKind.FromRoutedEventHandlers => $"{GetTypeUniqueIdentifier(type)}FromRoutedEventHandlerObservable",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static string GetAvaloniaRoutedWrapperName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.FromRoutedEvents => $"{GetTypeUniqueIdentifier(type)}FromAvaloniaRoutedEventObservable",
            ObservableEventsEntryKind.FromRoutedEventHandlers => $"{GetTypeUniqueIdentifier(type)}FromAvaloniaRoutedEventHandlerObservable",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static string GetTypeUniqueIdentifier(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');
    }

    private static void ReportInvalidDelegate(IEventSymbol evt, SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidEventDelegate,
            evt.Locations.FirstOrDefault(),
            evt.Name));
    }

    private static void ReportInvalidFromEventHandlersDelegate(IEventSymbol evt, SourceProductionContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidFromEventHandlersDelegate,
            evt.Locations.FirstOrDefault(),
            evt.Name));
    }
}
