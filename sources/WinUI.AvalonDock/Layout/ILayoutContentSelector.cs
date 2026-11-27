// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutContentSelector.cs

namespace AvalonDock.Layout;

public interface ILayoutContentSelector
{
    /// <summary>
    /// Gets or sets the selected content index.
    /// </summary>
    int SelectedContentIndex
    {
        get; set;
    }

    /// <summary>
    /// Gets the selected content.
    /// </summary>
    LayoutContent? SelectedContent
    {
        get;
    }

    int IndexOf(LayoutContent content);
}
