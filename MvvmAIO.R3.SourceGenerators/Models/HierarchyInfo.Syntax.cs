using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MvvmAIO.R3.SourceGenerators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MvvmAIO.R3.SourceGenerators.Models;

internal sealed partial class HierarchyInfo
{
    public CompilationUnitSyntax GetCompilationUnit(ImmutableArray<MemberDeclarationSyntax> memberDeclarations)
    {
        TypeDeclarationSyntax currentType = Hierarchy[0].GetSyntax()
            .AddModifiers(Token(SyntaxKind.PartialKeyword))
            .AddMembers(memberDeclarations.ToArray());

        for (var i = 1; i < Hierarchy.Length; i++)
        {
            currentType = Hierarchy[i].GetSyntax()
                .AddModifiers(Token(SyntaxKind.PartialKeyword))
                .AddMembers(currentType);
        }

        var trivia = GeneratedSourceHeader.LeadingTrivia();

        if (string.IsNullOrEmpty(Namespace))
        {
            return CompilationUnit()
                .AddMembers(currentType.WithLeadingTrivia(trivia))
                .NormalizeWhitespace();
        }

        return CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(ParseName(Namespace))
                    .WithLeadingTrivia(trivia)
                    .AddMembers(currentType))
            .NormalizeWhitespace();
    }
}
