// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DropArea.cs
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>Describes a docking region and its coordinate conversion.</summary>
public interface IDropArea
{
    /// <summary>Gets the region's captured screen bounds in the target host's logical units.</summary>
    Rect DetectionRect
    {
        get;
    }

    /// <summary>Gets the region kind.</summary>
    DropAreaType Type
    {
        get;
    }

    /// <summary>Converts a physical screen point into the target host's logical screen units.</summary>
    Point TransformToDeviceDPI(Point dragPosition);
}
