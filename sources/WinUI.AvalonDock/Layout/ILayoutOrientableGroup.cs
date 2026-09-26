// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutOrientableGroup.cs

using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout orientable group.
/// </summary>
public interface ILayoutOrientableGroup : ILayoutGroup
{
    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    Orientation Orientation
    {
        get; set;
    }
}
