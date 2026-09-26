// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutPaneSerializable.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout pane serializable.
/// </summary>
public interface ILayoutPaneSerializable
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    string? Id
    {
        get; set;
    }
}
