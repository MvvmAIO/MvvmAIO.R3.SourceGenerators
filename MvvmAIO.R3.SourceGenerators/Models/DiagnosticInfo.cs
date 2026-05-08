using System;
using Microsoft.CodeAnalysis;

namespace MvvmAIO.R3.SourceGenerators.Models;

internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params object[] arguments)
    {
        Descriptor = descriptor;
        Location = location;
        Arguments = arguments ?? Array.Empty<object>();
    }

    public DiagnosticDescriptor Descriptor { get; }

    public Location? Location { get; }

    public object[] Arguments { get; }

    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location, Arguments);

    public bool Equals(DiagnosticInfo other)
        => Descriptor.Id == other.Descriptor.Id && Equals(Location, other.Location);

    public override bool Equals(object? obj) => obj is DiagnosticInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Descriptor.Id.GetHashCode();
            hash = (hash * 31) + (Location?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
