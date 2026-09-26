// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Extensions.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Collections;
namespace AvalonDock;

internal static class Extensions
{
    public static bool Contains(this IEnumerable collection, object item)
    {
        foreach (object? value in collection)
        {
            if (value == item)
            {
                return true;
            }
        }

        return false;
    }

    public static V? GetValueOrDefault<V>(this WeakReference? wr) where V : class
    {
        return wr == null || !wr.IsAlive ? default : wr.Target as V;
    }
}
