using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using MvvmAIO.R3.SourceGenerators.Diagnostics;
using MvvmAIO.R3.SourceGenerators.Extensions;
using MvvmAIO.R3.SourceGenerators.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MvvmAIO.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class R3CommandGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "MvvmAIO.R3.R3CommandAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "MvvmAIO.R3.R3CommandAttribute.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(GeneratorBootstrapSyntaxFactory.CreateR3CommandAttributeCompilationUnit()),
                    Encoding.UTF8));
        });

        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is MethodDeclarationSyntax { Parent: TypeDeclarationSyntax },
            static (syntaxContext, _) => (IMethodSymbol)syntaxContext.TargetSymbol);

        var targetsWithCompilation = targets.Combine(context.CompilationProvider);
        context.RegisterSourceOutput(targetsWithCompilation, (spc, pair) =>
        {
            var method = pair.Left;
            var compilation = pair.Right;
            var containingType = method.ContainingType;
            if (!containingType.IsPartial())
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NonPartialType,
                    containingType.Locations.FirstOrDefault(),
                    containingType.Name));
                return;
            }

            if (!TryBuildCommandInfo(method, out var info))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidCommandMethodSignature,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                return;
            }

            if (TryGetDuplicateCommandPropertyDiagnostic(method, info, out var duplicateDiagnostic))
            {
                spc.ReportDiagnostic(duplicateDiagnostic);
                return;
            }

            if (!string.IsNullOrWhiteSpace(info.CanExecuteMemberName)
                && !TryValidateCanExecuteMember(method, containingType, info.CanExecuteMemberName!, compilation, out var canExecuteDiagnostic))
            {
                spc.ReportDiagnostic(canExecuteDiagnostic);
                return;
            }

            var compilationUnit = BuildCompilationUnit(containingType, method, info);
            spc.AddSource(
                $"{containingType.GetSafeHintName()}.{info.PropertyName}.R3Command.g.cs",
                SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
        });
    }

    private static bool TryBuildCommandInfo(IMethodSymbol method, out R3CommandInfo info)
    {
        info = default;
        if (method.MethodKind != MethodKind.Ordinary || method.IsStatic)
        {
            return false;
        }

        var commandName = GetCommandName(method);
        var hasParameter = method.Parameters.Length == 1;
        if (method.Parameters.Length > 1)
        {
            return false;
        }

        var parameterType = hasParameter
            ? method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isTask = returnType == "global::System.Threading.Tasks.Task";
        var isValueTask = returnType == "global::System.Threading.Tasks.ValueTask";
        var taskOfT = method.ReturnType is INamedTypeSymbol ntTask
            && ntTask.IsGenericType
            && ntTask.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.Task<TResult>";
        var valueTaskOfT = method.ReturnType is INamedTypeSymbol ntValueTask
            && ntValueTask.IsGenericType
            && ntValueTask.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<TResult>";

        if (!(method.ReturnsVoid || isTask || isValueTask || taskOfT || valueTaskOfT))
        {
            return false;
        }

        if (!hasParameter && (taskOfT || valueTaskOfT))
        {
            return false;
        }

        var outputType = (taskOfT || valueTaskOfT)
            ? ((INamedTypeSymbol)method.ReturnType).TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;

        var canExecuteMemberName = GetCanExecuteMemberName(method);

        info = new R3CommandInfo(commandName, parameterType, outputType, isTask || isValueTask, taskOfT || valueTaskOfT, canExecuteMemberName);
        return true;
    }

    private static bool TryGetDuplicateCommandPropertyDiagnostic(
        IMethodSymbol method,
        R3CommandInfo info,
        out Diagnostic diagnostic)
    {
        diagnostic = null!;
        foreach (var other in method.ContainingType.GetMembers().OfType<IMethodSymbol>())
        {
            if (SymbolEqualityComparer.Default.Equals(other, method))
            {
                continue;
            }

            if (!HasR3CommandAttribute(other) || !TryBuildCommandInfo(other, out var otherInfo))
            {
                continue;
            }

            if (!string.Equals(otherInfo.PropertyName, info.PropertyName, System.StringComparison.Ordinal))
            {
                continue;
            }

            diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.DuplicateCommandPropertyName,
                method.Locations.FirstOrDefault() ?? other.Locations.FirstOrDefault(),
                info.PropertyName,
                other.Name,
                method.Name,
                method.ContainingType.Name);
            return true;
        }

        return false;
    }

    private static bool TryValidateCanExecuteMember(
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        string memberName,
        Compilation compilation,
        out Diagnostic diagnostic)
    {
        diagnostic = null!;
        var member = FindCanExecuteMember(containingType, memberName);
        var location = method.Locations.FirstOrDefault() ?? containingType.Locations.FirstOrDefault();

        if (member is null)
        {
            diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CanExecuteMemberNotFound,
                location,
                memberName,
                containingType.Name,
                method.Name);
            return false;
        }

        var memberType = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };

        if (memberType is null || !IsValidCanExecuteType(memberType, compilation))
        {
            diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CanExecuteMemberTypeMismatch,
                member.Locations.FirstOrDefault() ?? location,
                memberName,
                containingType.Name,
                memberType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "unknown");
            return false;
        }

        return true;
    }

    private static ISymbol? FindCanExecuteMember(INamedTypeSymbol containingType, string memberName)
    {
        foreach (var member in containingType.GetMembers(memberName))
        {
            if (member is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false })
            {
                return member;
            }
        }

        return null;
    }

    private static bool IsValidCanExecuteType(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol named || !named.IsGenericType || named.TypeArguments.Length != 1)
        {
            return false;
        }

        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        if (!SymbolEqualityComparer.Default.Equals(
                named.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.None),
                boolType))
        {
            return false;
        }

        var r3Observable = compilation.GetTypeByMetadataName("R3.Observable`1");
        var ioObservable = compilation.GetTypeByMetadataName("System.IObservable`1");
        return (r3Observable is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, r3Observable))
            || (ioObservable is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, ioObservable));
    }

    private static bool HasR3CommandAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == AttributeMetadataName);

    private static CompilationUnitSyntax BuildCompilationUnit(INamedTypeSymbol type, IMethodSymbol method, R3CommandInfo info)
    {
        var methodName = method.Name;
        var fieldName = "_" + char.ToLowerInvariant(info.PropertyName[0]) + info.PropertyName.Substring(1);
        var parameterType = info.ParameterType is null ? null : ParseTypeName(info.ParameterType);
        var outputType = info.OutputType is null ? null : ParseTypeName(info.OutputType);
        var commandType = R3CommandSyntaxFactory.ReactiveCommandType(parameterType, outputType);

        var fieldDeclaration = FieldDeclaration(
                VariableDeclaration(R3CommandSyntaxFactory.NullableReactiveCommandType(parameterType, outputType))
                    .AddVariables(VariableDeclarator(Identifier(fieldName))))
            .AddModifiers(Token(SyntaxKind.PrivateKeyword));

        var propertyDeclaration = PropertyDeclaration(commandType, Identifier(info.PropertyName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            R3CommandSyntaxFactory.CreatePropertyInitializerExpression(fieldName, info, methodName)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        var hierarchy = HierarchyInfo.From(type);
        return hierarchy.GetCompilationUnit(ImmutableArray.Create<MemberDeclarationSyntax>(fieldDeclaration, propertyDeclaration));
    }

    private static string GetCommandName(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AttributeMetadataName)
            {
                continue;
            }

            foreach (var arg in attribute.NamedArguments)
            {
                if (arg.Key == "CommandName" && arg.Value.Value is string value && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return method.Name + "Command";
    }

    private static string? GetCanExecuteMemberName(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AttributeMetadataName)
            {
                continue;
            }

            foreach (var arg in attribute.NamedArguments)
            {
                if (arg.Key == "CanExecute" && arg.Value.Value is string value && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

}
