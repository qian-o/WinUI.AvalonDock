// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Themes/Theme.cs
using Microsoft.UI.Xaml;

namespace AvalonDock.Themes;

/// <summary>Describes the resource dictionary used by docking controls.</summary>
public abstract class Theme : DependencyObject, Core.IThemeInfo
{
    /// <summary>Initializes a theme.</summary>
    public Theme()
    {
    }

    /// <summary>Gets the theme name derived from its class name.</summary>
    public virtual string Name => GetType().Name.Replace("Theme", string.Empty);

    /// <inheritdoc/>
    Uri? Core.IThemeInfo.ResourceUri => GetResourceUri();

    /// <summary>Gets the resource dictionary URI for this theme.</summary>
    public abstract Uri? GetResourceUri();
}
