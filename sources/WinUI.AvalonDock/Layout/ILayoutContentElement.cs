// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutContentElement.cs

using Microsoft.UI.Xaml;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout element that holds a WPF UI content element
/// </summary>
public interface ILayoutContentElement
{
    /// <summary>
    /// Gets the root WPF FrameworkElement hosted by this layout element
    /// </summary>
    FrameworkElement Content
    {
        get;
    }
}
