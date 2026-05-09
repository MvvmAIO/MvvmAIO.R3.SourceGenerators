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
    private const string BootstrapExtensionsMetadataName = "R3.ObservableEvents.ObservableEventsBootstrapExtensions";
    private const string GeneratedNamespace = "R3.ObservableEvents";

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
            ctx.AddSource("R3.ObservableEvents.ObservableEventsBootstrapExtensions.g.cs", SourceText.From(GeneratorSources.ObservableEventsBootstrapExtensions, Encoding.UTF8));
            ctx.AddSource("R3.ObservableEvents.NullEvents.g.cs", SourceText.From(GeneratorSources.NullEvents, Encoding.UTF8));
        });

        var candidateInvocations = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => IsCandidateInvocation(syntax) || IsStaticCandidateInvocation(syntax),
            static (syntaxContext, _) => (InvocationExpressionSyntax)syntaxContext.Node);

        var inputs = candidateInvocations.Collect()
            .Combine(context.CompilationProvider)
            .Select(static (combined, _) => (Candidates: combined.Left, Compilation: combined.Right));

        context.RegisterSourceOutput(inputs, static (spc, input) =>
        {
            var targetTypes = CollectTargetTypes(input.Compilation, input.Candidates);
            var allTypes = ExpandWithBaseTypes(targetTypes);
            var allTypeSet = new HashSet<INamedTypeSymbol>(allTypes, SymbolEqualityComparer.Default);
            foreach (var type in allTypes)
            {
                var source = GenerateObservableSourceForType(type, allTypeSet, spc);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{type.GetSafeHintName()}.ObservableEvents.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        });
    }

    private static bool IsCandidateInvocation(SyntaxNode node)
        => node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "ObservableEvents",
                Expression: not GenericNameSyntax,
            },
        };

    /// <summary>
    /// Matches <c>ObservableEventsStatics.OBS_<em>StableHint</em>.ObservableEvents()</c> for static wrappers.
    /// </summary>
    private static bool IsStaticCandidateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "ObservableEvents",
                    Expression: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "ObservableEventsStatics" },
                        Name: SimpleNameSyntax staticHintNameSyntax,
                    },
                },
            })
        {
            return false;
        }

        var nestedId = staticHintNameSyntax.Identifier.ValueText;
        return nestedId.StartsWith("OBS_", System.StringComparison.Ordinal);
    }

    private static INamedTypeSymbol[] CollectTargetTypes(Compilation compilation, System.Collections.Immutable.ImmutableArray<InvocationExpressionSyntax> invocations)
    {
        var bootstrapType = compilation.GetTypeByMetadataName(BootstrapExtensionsMetadataName);
        if (bootstrapType is null)
        {
            return [];
        }

        var set = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var invocation in invocations)
        {
            var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            if (methodSymbol.Name != "ObservableEvents")
            {
                continue;
            }

            if (TryGetBootstrapObservableEventsExtensionTarget(invocation, semanticModel, methodSymbol, bootstrapType, out var instanceTarget))
            {
                if (instanceTarget.IsGenericType)
                {
                    instanceTarget = instanceTarget.OriginalDefinition;
                }

                set.Add(instanceTarget);
                continue;
            }

            if (TryGetTypeFromObservableEventsStaticsNested(methodSymbol, bootstrapType, compilation, out var staticTarget))
            {
                if (staticTarget.IsGenericType)
                {
                    staticTarget = staticTarget.OriginalDefinition;
                }

                set.Add(staticTarget);
            }
        }

        return set.ToArray();
    }

    private static bool TryGetBootstrapObservableEventsExtensionTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;

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

        // Reduced extension inference: ObservableEvents() on explicit receiver without TypeArguments surfaced on symbol.
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

    /// <summary>
    /// Parses <c>ObservableEventsStatics.OBS_<em>StableHint</em>.ObservableEvents()</c> against generated code.
    /// </summary>
    private static bool TryGetTypeFromObservableEventsStaticsNested(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        Compilation compilation,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;

        if (methodSymbol.ReducedFrom is not null || methodSymbol.IsExtensionMethod || methodSymbol.Name != "ObservableEvents")
        {
            return false;
        }

        var nested = methodSymbol.ContainingType;
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

    private static INamedTypeSymbol[] ExpandWithBaseTypes(INamedTypeSymbol[] targetTypes)
    {
        var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var target in targetTypes)
        {
            for (var current = target; current is not null; current = current.BaseType)
            {
                if (current.SpecialType == SpecialType.System_Object)
                {
                    break;
                }

                if (current.TypeKind != TypeKind.Class || current.IsGenericType)
                {
                    continue;
                }

                set.Add(current);
            }
        }

        return set
            .OrderBy(GetInheritanceDepth)
            .ThenBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .ToArray();
    }

    private static string GenerateObservableSourceForType(
        INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> allTypes,
        SourceProductionContext context)
    {
        var unit = SyntaxFactory.CompilationUnit()
            .AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading")));

        var members = new List<MemberDeclarationSyntax>
        {
            CreateExtensionsClass(type),
            CreateWrapperClass(type, allTypes, context)
        };

        if (HasPublicStaticObservableEvents(type))
        {
            members.Add(CreateObservableEventsStaticsEntry(type));
            members.Add(CreateStaticWrapperClass(type, context));
        }

        var nsMember = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(GeneratedNamespace))
            .AddMembers(members.ToArray());
        unit = unit.AddMembers(nsMember);

        // Analyzer-generated translation units require an explicit `#nullable` directive before NRT punctuation (CS8669).
        return "#nullable enable\n\n" + unit.NormalizeWhitespace().ToFullString();
    }

    private static ClassDeclarationSyntax CreateExtensionsClass(INamedTypeSymbol type)
    {
        return SyntaxFactory.ClassDeclaration("FromEventObservableExtensions")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(CreateFromEventsMethod(type));
    }

    private static MethodDeclarationSyntax CreateFromEventsMethod(INamedTypeSymbol type)
    {
        var typeName = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var returnType = SyntaxFactory.ParseTypeName(GetWrapperName(type));

        return SyntaxFactory.MethodDeclaration(returnType, "ObservableEvents")
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
    }

    private static ClassDeclarationSyntax CreateWrapperClass(
        INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> allTypes,
        SourceProductionContext context)
    {
        var classDeclaration = SyntaxFactory.ClassDeclaration(GetWrapperName(type))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));

        if (type.BaseType is { } baseType && allTypes.Contains(baseType))
        {
            classDeclaration = classDeclaration.WithBaseList(
                SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.ParseTypeName(GetWrapperName(baseType))))));
        }

        var senderType = SyntaxFactory.ParseTypeName(QualifiedType(type));
        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(senderType)
                    .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var ctor = SyntaxFactory.ConstructorDeclaration(GetWrapperName(type))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender"))
                    .WithType(senderType))
            .WithInitializer(
                type.BaseType is { } ctorBaseType && allTypes.Contains(ctorBaseType)
                    ? SyntaxFactory.ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("sender")))))
                    : null)
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.ParseStatement("_sender = sender;")));

        var members = new List<MemberDeclarationSyntax> { field, ctor };
        foreach (var evt in type.GetMembers().OfType<IEventSymbol>().Where(static e => !e.IsStatic && e.DeclaredAccessibility == Accessibility.Public))
        {
            var eventTarget = $"_sender.{evt.Name}";
            if (TryCreateEventMethod(evt, eventTarget, context, out var eventMethod))
            {
                members.Add(eventMethod);
            }
        }

        return classDeclaration.AddMembers(members.ToArray());
    }

    private static bool HasPublicStaticObservableEvents(INamedTypeSymbol type)
        => type.GetMembers().OfType<IEventSymbol>().Any(static e =>
            e is { IsStatic: true, DeclaredAccessibility: Accessibility.Public });

    private static ClassDeclarationSyntax CreateObservableEventsStaticsEntry(INamedTypeSymbol type)
    {
        var nestedName = GetStaticObservableEventsNestedId(type);
        var returnType = SyntaxFactory.ParseTypeName(GetStaticWrapperName(type));

        var observableMethod = SyntaxFactory.MethodDeclaration(returnType, "ObservableEvents")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.ObjectCreationExpression(returnType)
                        .WithArgumentList(SyntaxFactory.ArgumentList())))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        var nested = SyntaxFactory.ClassDeclaration(nestedName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddMembers(observableMethod);

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
            if (TryCreateEventMethod(evt, eventTarget, context, out var eventMethod))
            {
                members.Add(eventMethod);
            }
        }

        return SyntaxFactory.ClassDeclaration(GetStaticWrapperName(type))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddMembers(members.ToArray());
    }

    private static bool TryCreateEventMethod(
        IEventSymbol evt,
        string eventAccessorExpression,
        SourceProductionContext context,
        out MethodDeclarationSyntax method)
    {
        method = null!;
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

        var returnType = GetObservableReturnType(invoke.Parameters);
        var eventCref = $"{QualifiedType(evt.ContainingType)}.{evt.Name}";
        var docXml = SyntaxFactory.ParseLeadingTrivia(
            $"/// <summary>\n" +
            $"/// <inheritdoc cref=\"{eventCref}\" />\n" +
            $"/// </summary>\n");
        var methodDeclaration = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(returnType),
                evt.Name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                    .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))))
            // Apply XML doc AFTER modifiers exist; trivia before ModifierList ends up below `public`
            .WithLeadingTrivia(docXml);

        var bodyStatement = BuildFromEventStatement(delegateType, invoke.Parameters, eventAccessorExpression);
        if (bodyStatement is null)
        {
            ReportInvalidDelegate(evt, context);
            return false;
        }

        method = methodDeclaration.WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(bodyStatement)));
        return true;
    }

    private static string? BuildFromEventStatement(
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters,
        string eventAccessorExpression)
    {
        var eventType = QualifiedType(delegateType);
        if (parameters.Length == 0)
        {
            return
                $"return global::R3.Observable.FromEvent<{eventType}, global::R3.Unit>(h => () => h(global::R3.Unit.Default), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, cancellationToken);";
        }

        if (parameters.Length == 1)
        {
            var t1 = QualifiedType(parameters[0].Type);
            return
                $"return global::R3.Observable.FromEvent<{eventType}, {t1}>(h => arg1 => h(arg1), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, cancellationToken);";
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var eventArgsType = QualifiedType(parameters[1].Type);
            return
                $"return global::R3.Observable.FromEvent<{eventType}, {eventArgsType}>(h => (sender, e) => h(e), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, cancellationToken);";
        }

        var tupleType = $"({string.Join(", ", parameters.Select(static p => QualifiedType(p.Type)))})";
        var tupleArgs = string.Join(", ", parameters.Select((_, i) => $"arg{i + 1}"));
        return
            $"return global::R3.Observable.FromEvent<{eventType}, {tupleType}>(h => ({tupleArgs}) => h(({tupleArgs})), e => {eventAccessorExpression} += e, e => {eventAccessorExpression} -= e, cancellationToken);";
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

    private static int GetInheritanceDepth(INamedTypeSymbol type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private static string GetStaticObservableEventsNestedId(INamedTypeSymbol type) => $"OBS_{type.GetSafeHintName()}";

    private static string GetStaticWrapperName(INamedTypeSymbol type) =>
        $"{GetTypeUniqueIdentifier(type)}StaticFromEventObservable";

    private static string GetWrapperName(INamedTypeSymbol type) => $"{GetTypeUniqueIdentifier(type)}FromEventObservable";

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
}
