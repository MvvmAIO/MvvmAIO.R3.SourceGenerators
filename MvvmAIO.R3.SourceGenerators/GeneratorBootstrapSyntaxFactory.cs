using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MvvmAIO.R3.SourceGenerators;

/// <summary>Post-initialization bootstrap sources built with SyntaxFactory.</summary>
internal static class GeneratorBootstrapSyntaxFactory
{
    private const string BootstrapNamespace = "R3.SourceGenerators";
    private static readonly NameSyntax BootstrapNamespaceName = ParseName(BootstrapNamespace);
    private static readonly TypeSyntax NullEventsType = QualifiedName(BootstrapNamespaceName, IdentifierName("NullEvents"));

    public static CompilationUnitSyntax CreateR3CommandAttributeCompilationUnit()
    {
        var attributeClass = ClassDeclaration("R3CommandAttribute")
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword))
            .AddBaseListTypes(SimpleBaseType(ParseTypeName("global::System.Attribute")))
            .AddAttributeLists(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(ParseName("global::System.AttributeUsage"))
                            .WithArgumentList(
                                AttributeArgumentList(
                                    SingletonSeparatedList(
                                        AttributeArgument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                ParseName("global::System"),
                                                IdentifierName("AttributeTargets.Method")))))))))
            .AddMembers(
                PropertyDeclaration(NullableType(PredefinedType(Token(SyntaxKind.StringKeyword))), Identifier("CommandName"))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddAccessorListAccessors(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))),
                PropertyDeclaration(NullableType(PredefinedType(Token(SyntaxKind.StringKeyword))), Identifier("CanExecute"))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddAccessorListAccessors(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))));

        return CompilationUnit()
            .AddUsings(UsingDirective(ParseName("global::System")))
            .AddMembers(
                NamespaceDeclaration(ParseName("MvvmAIO.R3"))
                    .AddMembers(attributeClass));
    }

    public static CompilationUnitSyntax CreateNullEventsCompilationUnit() =>
        CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(
                        StructDeclaration("NullEvents")
                            .AddModifiers(Token(SyntaxKind.InternalKeyword))
                            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())));

    public static CompilationUnitSyntax CreateObservableEventsBootstrapExtensionsCompilationUnit(bool includeStatics) =>
        CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(CreateBootstrapExtensionsClass(includeStatics)));

    public static CompilationUnitSyntax CreateObservableEventsStaticsShellCompilationUnit() =>
        CompilationUnit()
            .AddMembers(
                FileScopedNamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(
                        ClassDeclaration("ObservableEventsStatics")
                            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword))));

    private static ClassDeclarationSyntax CreateBootstrapExtensionsClass(bool includeStatics)
    {
        var methods = new List<MemberDeclarationSyntax>
        {
            CreateNullReturningExtension("FromEvents"),
            CreateNullReturningExtension("FromEventHandlers"),
            CreateNullReturningExtension("FromRoutedEvents"),
            CreateNullReturningExtension(
                "FromRoutedEvents",
                Parameter(Identifier("routes")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension("FromRoutedEventHandlers"),
            CreateNullReturningExtension(
                "FromRoutedEventHandlers",
                Parameter(Identifier("routes")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension(
                "FromAttachedRoutedEvent",
                Parameter(Identifier("routedEvent")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("routes"))
                    .WithType(ParseTypeName("global::System.Object"))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension(
                "FromAttachedRoutedEventHandler",
                Parameter(Identifier("routedEvent")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("routes"))
                    .WithType(ParseTypeName("global::System.Object"))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
        };

        if (includeStatics)
        {
            methods.Add(CreateObservableEventsStaticsExtension());
        }

        return ClassDeclaration("ObservableEventsBootstrapExtensions")
            .AddModifiers(
                Token(SyntaxKind.InternalKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword))
            .AddMembers(methods.ToArray());
    }

    // Note: the bootstrap fallback intentionally takes <c>this object?</c> instead of a generic
    // <c><![CDATA[<T>(this T)]]></c> form (ReactiveMarbles-style). A generic stub would share its
    // erased signature <c>FromEvents<T>(T)</c> with the generic-constrained extensions emitted for
    // <c>where T : Base, IFirst, ISecond</c> scenarios (MvvmAIO-only). Per the C# specification,
    // type parameter constraints are not part of method signatures (CS0111) and are not a
    // tiebreaker in overload resolution (CS0121), so the two forms would collide both at
    // declaration time and at every call site inside a constrained generic method.
    private static MethodDeclarationSyntax CreateNullReturningExtension(
        string methodName,
        params ParameterSyntax[] extraParameters)
    {
        var parameters = new List<ParameterSyntax>
        {
            Parameter(Identifier("source"))
                .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
                .AddModifiers(Token(SyntaxKind.ThisKeyword)),
        };
        parameters.AddRange(extraParameters);

        return MethodDeclaration(NullEventsType, Identifier(methodName))
            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(parameters.ToArray())
            .WithBody(
                Block(
                    ReturnStatement(
                        DefaultExpression(NullEventsType))));
    }

    private static MethodDeclarationSyntax CreateObservableEventsStaticsExtension()
    {
        return MethodDeclaration(NullEventsType, Identifier("ObservableEventsStatics"))
            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(TypeParameter("T"))))
            .AddParameterListParameters(
                Parameter(Identifier("source"))
                    .WithType(NullableType(IdentifierName("T")))
                    .AddModifiers(Token(SyntaxKind.ThisKeyword)))
            .WithBody(
                Block(
                    ReturnStatement(
                        DefaultExpression(NullEventsType))));
    }

    private static AttributeListSyntax CreateEditorBrowsableNeverAttributeList() =>
        AttributeList(
            SingletonSeparatedList(
                Attribute(ParseName("global::System.ComponentModel.EditorBrowsable"))
                    .WithArgumentList(
                        AttributeArgumentList(
                            SingletonSeparatedList(
                                AttributeArgument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParseName("global::System.ComponentModel.EditorBrowsableState"),
                                        IdentifierName("Never"))))))));
}
