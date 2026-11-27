// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/ReferenceEqualityComparer.cs
using System.Collections;
using System.Collections.Generic;

namespace AvalonDock;

public sealed class ReferenceEqualityComparer : IEqualityComparer, IEqualityComparer<object?>
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static ReferenceEqualityComparer Default { get; } = new ReferenceEqualityComparer();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object? obj) => obj is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
