// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Converters/ConverterCreater.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
namespace AvalonDock.Converters;

internal static class ConverterCreater
{
    private static readonly Dictionary<Type, object> ConverterMap = new();

    public static T Get<T>()
        where T : new()
    {
        if (!ConverterMap.ContainsKey(typeof(T)))
        {
            ConverterMap.Add(typeof(T), new T());
        }

        return (T)ConverterMap[typeof(T)];
    }
}
