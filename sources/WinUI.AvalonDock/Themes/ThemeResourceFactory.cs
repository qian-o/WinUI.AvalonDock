using Microsoft.UI.Xaml;

namespace AvalonDock.Themes;

internal static class ThemeResourceFactory
{
    internal static ResourceDictionary? Create(Theme? theme) => theme is DictionaryTheme { ThemeResourceDictionary: { } resources } ? Copy(resources)
        : theme?.GetResourceUri() is { } uri ? new ResourceDictionary { Source = uri } : null;

    internal static ResourceDictionary Copy(ResourceDictionary original)
    {
        // WinUI ResourceDictionary has a single owner. Separate XamlRoots need separate
        // containers; share individual resources as normal resource lookup does.
        ResourceDictionary result = original.Source != null ? new ResourceDictionary { Source = original.Source } : new ResourceDictionary();
        foreach (KeyValuePair<object, object> pair in original)
        {
            result[pair.Key] = pair.Value;
        }

        result.MergedDictionaries.Clear();
        result.ThemeDictionaries.Clear();
        foreach (ResourceDictionary? dictionary in original.MergedDictionaries)
        {
            result.MergedDictionaries.Add(Copy(dictionary));
        }

        foreach (KeyValuePair<object, object> pair in original.ThemeDictionaries)
        {
            result.ThemeDictionaries[pair.Key] = pair.Value is ResourceDictionary dictionary ? Copy(dictionary) : pair.Value;
        }

        return result;
    }
}
