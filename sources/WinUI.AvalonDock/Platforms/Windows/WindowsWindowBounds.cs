using Windows.Foundation;

namespace AvalonDock.Platforms.Windows;

/// <summary>Converts window bounds between physical pixels and the window's device-independent units.</summary>
internal static class WindowsWindowBounds
{
    internal static Rect ToPhysical(Rect layoutBounds, double windowScale) =>
        new(layoutBounds.X * windowScale, layoutBounds.Y * windowScale,
            layoutBounds.Width * windowScale, layoutBounds.Height * windowScale);

    internal static Rect ToLayout(Rect physicalBounds, double windowScale) =>
        new(physicalBounds.X / windowScale, physicalBounds.Y / windowScale,
            physicalBounds.Width / windowScale, physicalBounds.Height / windowScale);
}
