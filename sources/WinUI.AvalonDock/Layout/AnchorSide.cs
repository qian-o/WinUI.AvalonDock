// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/AnchorSide.cs

namespace AvalonDock.Layout;

/// <summary>
/// Determines the side to which a <see cref="LayoutAnchorSide"/> model is anchored in the
/// <see cref="DockingManager.Layout"/> root dependency property.
/// </summary>
public enum AnchorSide
{
    /// <summary>The object is anchored to the left side.</summary>
    Left,

    /// <summary>The object is anchored to the top side.</summary>
    Top,

    /// <summary>The object is anchored to the right side.</summary>
    Right,

    /// <summary>The object is anchored to the bottom side.</summary>
    Bottom
}
