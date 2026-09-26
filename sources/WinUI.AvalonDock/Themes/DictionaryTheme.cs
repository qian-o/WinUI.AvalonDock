// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Themes/DictionaryTheme.cs
using Microsoft.UI.Xaml;

namespace AvalonDock.Themes;

/// <summary>Describes a theme supplied as an existing resource dictionary.</summary>
public abstract class DictionaryTheme : Theme
{
    /// <summary>Initializes a dictionary theme without an assigned resource dictionary.</summary>
    public DictionaryTheme()
    {
    }

    /// <summary>Initializes a dictionary theme with an existing resource dictionary.</summary>
    /// <param name="themeResourceDictionary">The resource dictionary.</param>
    public DictionaryTheme(ResourceDictionary themeResourceDictionary)
    {
        ThemeResourceDictionary = themeResourceDictionary;
    }

    /// <summary>Gets the resource dictionary supplied to the constructor.</summary>
    public ResourceDictionary? ThemeResourceDictionary
    {
        get; private set;
    }

    /// <summary>Returns null because this theme supplies a dictionary instance instead of a URI.</summary>
    public override Uri? GetResourceUri()
    {
        return null;
    }
}
