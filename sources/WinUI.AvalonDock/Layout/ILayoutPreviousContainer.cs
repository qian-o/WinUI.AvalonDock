// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutPreviousContainer.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout previous container.
/// </summary>
public interface ILayoutPreviousContainer
{
    /// <summary>
    /// Gets or sets the previous container.
    /// </summary>
    ILayoutContainer? PreviousContainer
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the previous container id.
    /// </summary>
    string? PreviousContainerId
    {
        get; set;
    }
}
