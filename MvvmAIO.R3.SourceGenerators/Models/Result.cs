using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MvvmAIO.R3.SourceGenerators.Models;

internal readonly struct Result<T> : IEquatable<Result<T>>
    where T : class
{
    public Result(T? value, ImmutableArray<DiagnosticInfo> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T? Value { get; }

    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    public bool HasBlockingDiagnostics => Diagnostics.Any(static x => x.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

    public bool Equals(Result<T> other) => Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}
