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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("R3.ObservableEvents.ObservableEventsBootstrapExtensions.g.cs", SourceText.From(GeneratorSources.ObservableEventsBootstrapExtensions, Encoding.UTF8));
            ctx.AddSource("R3.ObservableEvents.NullEvents.g.cs", SourceText.From(GeneratorSources.NullEvents, Encoding.UTF8));
        });

        var candidateInvocations = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => IsCandidateInvocation(syntax),
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
            },
        };

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

            if (!SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, bootstrapType))
            {
                continue;
            }

            if (methodSymbol.TypeArguments.Length != 1 || methodSymbol.TypeArguments[0] is not INamedTypeSymbol namedType)
            {
                continue;
            }

            if (namedType.IsGenericType)
            {
                namedType = namedType.OriginalDefinition;
            }

            set.Add(namedType);
        }

        return set.ToArray();
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

        var nsMember = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(GeneratedNamespace))
            .AddMembers(members.ToArray());
        unit = unit.AddMembers(nsMember);

        return unit.NormalizeWhitespace().ToFullString();
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
        var typeName = SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
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

        var senderType = SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
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
            if (TryCreateEventMethod(evt, context, out var eventMethod))
            {
                members.Add(eventMethod);
            }
        }

        return classDeclaration.AddMembers(members.ToArray());
    }

    private static bool TryCreateEventMethod(IEventSymbol evt, SourceProductionContext context, out MethodDeclarationSyntax method)
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
        var eventCref = $"{evt.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{evt.Name}";
        var methodDeclaration = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(returnType),
                evt.Name)
            .WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(
                $"/// <summary>\n" +
                $"/// <inheritdoc cref=\"{eventCref}\"/>\n" +
                $"/// </summary>\n"))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                    .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))));

        var bodyStatement = BuildFromEventStatement(evt, delegateType, invoke.Parameters);
        if (bodyStatement is null)
        {
            ReportInvalidDelegate(evt, context);
            return false;
        }

        method = methodDeclaration.WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(bodyStatement)));
        return true;
    }

    private static string? BuildFromEventStatement(
        IEventSymbol evt,
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters)
    {
        var eventType = delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var eventName = evt.Name;
        if (parameters.Length == 0)
        {
            return
                $"return global::R3.Observable.FromEvent<{eventType}, global::R3.Unit>(h => () => h(global::R3.Unit.Default), e => _sender.{eventName} += e, e => _sender.{eventName} -= e, cancellationToken);";
        }

        if (parameters.Length == 1)
        {
            var t1 = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return
                $"return global::R3.Observable.FromEvent<{eventType}, {t1}>(h => arg1 => h(arg1), e => _sender.{eventName} += e, e => _sender.{eventName} -= e, cancellationToken);";
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var eventArgsType = parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return
                $"return global::R3.Observable.FromEvent<{eventType}, {eventArgsType}>(h => (sender, e) => h(e), e => _sender.{eventName} += e, e => _sender.{eventName} -= e, cancellationToken);";
        }

        var tupleType = $"({string.Join(", ", parameters.Select(static p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))})";
        var tupleArgs = string.Join(", ", parameters.Select((_, i) => $"arg{i + 1}"));
        return
            $"return global::R3.Observable.FromEvent<{eventType}, {tupleType}>(h => ({tupleArgs}) => h(({tupleArgs})), e => _sender.{eventName} += e, e => _sender.{eventName} -= e, cancellationToken);";
    }

    private static string GetObservableReturnType(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
        {
            return "global::R3.Observable<global::R3.Unit>";
        }

        if (parameters.Length == 1)
        {
            var t1 = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"global::R3.Observable<{t1}>";
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var eventArgsType = parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"global::R3.Observable<{eventArgsType}>";
        }

        var tupleType = $"({string.Join(", ", parameters.Select(static p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))})";
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
