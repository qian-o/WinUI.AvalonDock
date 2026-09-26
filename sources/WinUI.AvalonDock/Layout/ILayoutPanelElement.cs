// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutPanelElement.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout panel element.
/// </summary>
public interface ILayoutPanelElement : ILayoutElement
{
    /// <summary>
    /// Gets a value indicating whether this instance is visible.
    /// </summary>
    bool IsVisible
    {
        get;
    }
}
