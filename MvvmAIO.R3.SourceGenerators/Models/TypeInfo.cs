using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MvvmAIO.R3.SourceGenerators.Models;

internal sealed class TypeInfo
{
    public TypeInfo(string qualifiedName, TypeKind kind, bool isRecord)
    {
        QualifiedName = qualifiedName;
        Kind = kind;
        IsRecord = isRecord;
    }

    public string QualifiedName { get; }

    public TypeKind Kind { get; }

    public bool IsRecord { get; }

    public TypeDeclarationSyntax GetSyntax()
    {
        return Kind switch
        {
            TypeKind.Struct => StructDeclaration(QualifiedName),
            TypeKind.Interface => InterfaceDeclaration(QualifiedName),
            TypeKind.Class when IsRecord =>
                RecordDeclaration(Token(SyntaxKind.RecordKeyword), QualifiedName)
                .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)),
            _ => ClassDeclaration(QualifiedName),
        };
    }
}
