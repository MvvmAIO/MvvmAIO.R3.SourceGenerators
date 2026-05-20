using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MvvmAIO.R3.SourceGenerators;

internal static class R3CommandSyntaxFactory
{
    private static readonly NameSyntax R3Name = ParseName("global::R3");
    private static readonly NameSyntax ValueTaskName = ParseName("global::System.Threading.Tasks.ValueTask");

    public static TypeSyntax ReactiveCommandType(TypeSyntax? parameterType = null, TypeSyntax? outputType = null)
    {
        if (parameterType is null && outputType is null)
        {
            return QualifiedName(R3Name, IdentifierName("ReactiveCommand"));
        }

        var typeArgs = outputType is null
            ? SingletonSeparatedList(parameterType!)
            : SeparatedList([parameterType!, outputType]);

        return QualifiedName(
            R3Name,
            GenericName(Identifier("ReactiveCommand"))
                .WithTypeArgumentList(TypeArgumentList(typeArgs)));
    }

    public static TypeSyntax NullableReactiveCommandType(TypeSyntax? parameterType = null, TypeSyntax? outputType = null) =>
        NullableType(ReactiveCommandType(parameterType, outputType));

    public static ObjectCreationExpressionSyntax CreateReactiveCommand(
        R3CommandInfo info,
        string methodName)
    {
        var parameterType = info.ParameterType is null ? null : ParseTypeName(info.ParameterType);
        var outputType = info.OutputType is null ? null : ParseTypeName(info.OutputType);
        var commandType = ReactiveCommandType(parameterType, outputType);
        var handler = CreateHandlerExpression(info, methodName);
        return ObjectCreationExpression(commandType)
            .WithArgumentList(ArgumentList(CreateConstructorArguments(handler, info.CanExecuteMemberName)));
    }

    public static ExpressionSyntax CreatePropertyInitializerExpression(
        string fieldName,
        R3CommandInfo info,
        string methodName) =>
        AssignmentExpression(
            SyntaxKind.CoalesceAssignmentExpression,
            IdentifierName(fieldName),
            CreateReactiveCommand(info, methodName));

    private static SeparatedSyntaxList<ArgumentSyntax> CreateConstructorArguments(
        ExpressionSyntax handler,
        string? canExecuteMemberName)
    {
        var args = new[] { Argument(handler) };
        if (string.IsNullOrWhiteSpace(canExecuteMemberName))
        {
            return SingletonSeparatedList(args[0]);
        }

        return SeparatedList([args[0], Argument(IdentifierName(canExecuteMemberName!))]);
    }

    private static ExpressionSyntax CreateHandlerExpression(R3CommandInfo info, string methodName)
    {
        if (info.IsAsyncWithoutResult)
        {
            return info.ParameterType is null
                ? CreateAsyncWithoutResultHandler(methodName)
                : CreateAsyncWithoutResultHandler(methodName, "x");
        }

        if (info.IsAsyncWithResult)
        {
            return (ParenthesizedLambdaExpressionSyntax)CreateAsyncWithResultHandler(methodName);
        }

        return info.ParameterType is null
            ? CreateSyncHandler(methodName)
            : CreateSyncHandler(methodName, "x");
    }

    private static ParenthesizedLambdaExpressionSyntax CreateAsyncWithoutResultHandler(
        string methodName,
        string? parameterName = null)
    {
        var invoke = parameterName is null
            ? InvocationExpression(IdentifierName(methodName))
            : InvocationExpression(
                IdentifierName(methodName),
                ArgumentList(SingletonSeparatedList(Argument(IdentifierName(parameterName)))));

        var valueTaskCreation = ObjectCreationExpression(ValueTaskName)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(invoke))));

        if (parameterName is null)
        {
            return ParenthesizedLambdaExpression(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                    [
                        Parameter(Identifier("_")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                        Parameter(Identifier("__")),
                    ])),
                valueTaskCreation);
        }

        return ParenthesizedLambdaExpression(
            ParameterList(
                SeparatedList<ParameterSyntax>(
                [
                    Parameter(Identifier(parameterName)),
                    Parameter(Identifier("__")),
                ])),
            valueTaskCreation);
    }

    private static ExpressionSyntax CreateAsyncWithResultHandler(string methodName) =>
        ParseExpression($"async (x, __) => await {methodName}(x)");

    private static ExpressionSyntax CreateSyncHandler(string methodName, string? parameterName = null)
    {
        var invoke = parameterName is null
            ? InvocationExpression(IdentifierName(methodName))
            : InvocationExpression(
                IdentifierName(methodName),
                ArgumentList(SingletonSeparatedList(Argument(IdentifierName(parameterName)))));

        return parameterName is null
            ? SimpleLambdaExpression(Parameter(Identifier("_")), invoke)
            : SimpleLambdaExpression(Parameter(Identifier(parameterName)), invoke);
    }
}

internal readonly struct R3CommandInfo
{
    public R3CommandInfo(
        string propertyName,
        string? parameterType,
        string? outputType,
        bool isAsyncWithoutResult,
        bool isAsyncWithResult,
        string? canExecuteMemberName)
    {
        PropertyName = propertyName;
        ParameterType = parameterType;
        OutputType = outputType;
        IsAsyncWithoutResult = isAsyncWithoutResult;
        IsAsyncWithResult = isAsyncWithResult;
        CanExecuteMemberName = canExecuteMemberName;
    }

    public string PropertyName { get; }

    public string? ParameterType { get; }

    public string? OutputType { get; }

    public bool IsAsyncWithoutResult { get; }

    public bool IsAsyncWithResult { get; }

    public string? CanExecuteMemberName { get; }

    public bool HasCanExecute => !string.IsNullOrWhiteSpace(CanExecuteMemberName);
}
