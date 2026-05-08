using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MvvmAIO.R3.SourceGenerators.Models;

internal sealed partial class HierarchyInfo
{
    public HierarchyInfo(string filenameHint, string metadataName, string @namespace, ImmutableArray<TypeInfo> hierarchy)
    {
        FilenameHint = filenameHint;
        MetadataName = metadataName;
        Namespace = @namespace;
        Hierarchy = hierarchy;
    }

    public string FilenameHint { get; }

    public string MetadataName { get; }

    public string Namespace { get; }

    public ImmutableArray<TypeInfo> Hierarchy { get; }

    public static HierarchyInfo From(INamedTypeSymbol typeSymbol)
    {
        var hierarchy = new List<TypeInfo>();

        for (INamedTypeSymbol? parent = typeSymbol; parent is not null; parent = parent.ContainingType)
        {
            hierarchy.Add(new TypeInfo(
                parent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                parent.TypeKind,
                parent.IsRecord));
        }

        return new HierarchyInfo(
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty).Replace('<', '_').Replace('>', '_').Replace('.', '_'),
            typeSymbol.MetadataName,
            typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.ToDisplayString(),
            hierarchy.ToImmutableArray());
    }
}
