// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Themes/GenericTheme.cs
namespace AvalonDock.Themes;

/// <summary>Uses the library's default styles and native WinUI theme resources.</summary>
public class GenericTheme : Theme
{
    /// <inheritdoc/>
    public override Uri GetResourceUri()
    {
        return new Uri("ms-appx:///WinUI.AvalonDock/Themes/Generic.xaml");
    }
}
