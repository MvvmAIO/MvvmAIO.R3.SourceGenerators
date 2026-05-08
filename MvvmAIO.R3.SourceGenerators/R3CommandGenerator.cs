using System.Linq;
using System.Collections.Immutable;
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

// [Generator(LanguageNames.CSharp)]
public sealed class R3CommandGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "MvvmAIO.R3.R3CommandAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("MvvmAIO.R3.R3CommandAttribute.g.cs", SourceText.From(GeneratorSources.R3CommandAttribute, Encoding.UTF8));
        });

        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is MethodDeclarationSyntax { Parent: TypeDeclarationSyntax },
            static (syntaxContext, _) => (IMethodSymbol)syntaxContext.TargetSymbol);

        context.RegisterSourceOutput(targets, static (spc, method) =>
        {
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

            var compilationUnit = BuildCompilationUnit(containingType, method, info);
            spc.AddSource(
                $"{containingType.GetSafeHintName()}.{info.PropertyName}.R3Command.g.cs",
                SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
        });
    }

    private static bool TryBuildCommandInfo(IMethodSymbol method, out CommandInfo info)
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

        info = new CommandInfo(commandName, parameterType, outputType, isTask || isValueTask, taskOfT || valueTaskOfT);
        return true;
    }

    private static CompilationUnitSyntax BuildCompilationUnit(INamedTypeSymbol type, IMethodSymbol method, CommandInfo info)
    {
        var methodName = method.Name;
        var fieldName = "_" + char.ToLowerInvariant(info.PropertyName[0]) + info.PropertyName.Substring(1);

        string commandType;
        string constructorExpr;
        if (info.IsAsyncWithoutResult)
        {
            commandType = info.ParameterType is null ? "global::R3.ReactiveCommand" : $"global::R3.ReactiveCommand<{info.ParameterType}>";
            if (info.ParameterType is null)
            {
                constructorExpr = $"new global::R3.ReactiveCommand((_, __) => new global::System.Threading.Tasks.ValueTask({methodName}()))";
            }
            else
            {
                constructorExpr = $"new global::R3.ReactiveCommand<{info.ParameterType}>((x, __) => new global::System.Threading.Tasks.ValueTask({methodName}(x)))";
            }
        }
        else if (info.IsAsyncWithResult)
        {
            commandType = $"global::R3.ReactiveCommand<{info.ParameterType}, {info.OutputType}>";
            constructorExpr = $"new global::R3.ReactiveCommand<{info.ParameterType}, {info.OutputType}>(async (x, __) => await {methodName}(x))";
        }
        else if (info.OutputType is not null)
        {
            commandType = $"global::R3.ReactiveCommand<{info.ParameterType}, {info.OutputType}>";
            constructorExpr = $"new global::R3.ReactiveCommand<{info.ParameterType}, {info.OutputType}>(x => {methodName}(x))";
        }
        else
        {
            commandType = info.ParameterType is null ? "global::R3.ReactiveCommand" : $"global::R3.ReactiveCommand<{info.ParameterType}>";
            if (info.ParameterType is null)
            {
                constructorExpr = $"new global::R3.ReactiveCommand(_ => {methodName}())";
            }
            else
            {
                constructorExpr = $"new global::R3.ReactiveCommand<{info.ParameterType}>(x => {methodName}(x))";
            }
        }

        var fieldDeclaration = FieldDeclaration(
                VariableDeclaration(ParseTypeName(commandType + "?"))
                    .AddVariables(
                        VariableDeclarator(Identifier(fieldName))))
            .AddModifiers(Token(SyntaxKind.PrivateKeyword));

        var propertyDeclaration = PropertyDeclaration(ParseTypeName(commandType), Identifier(info.PropertyName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithExpressionBody(
                        ArrowExpressionClause(ParseExpression($"{fieldName} ??= {constructorExpr}")))
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
    private readonly struct CommandInfo
    {
        public CommandInfo(string propertyName, string? parameterType, string? outputType, bool isAsyncWithoutResult, bool isAsyncWithResult)
        {
            PropertyName = propertyName;
            ParameterType = parameterType;
            OutputType = outputType;
            IsAsyncWithoutResult = isAsyncWithoutResult;
            IsAsyncWithResult = isAsyncWithResult;
        }

        public string PropertyName { get; }

        public string? ParameterType { get; }

        public string? OutputType { get; }

        public bool IsAsyncWithoutResult { get; }

        public bool IsAsyncWithResult { get; }
    }
}
