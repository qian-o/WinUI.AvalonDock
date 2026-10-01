using Windows.Foundation;

namespace AvalonDock.Platforms.Windows;

/// <summary>Separates the stable desktop position scale from a window's current size scale.</summary>
internal static class WindowsWindowBounds
{
    internal static Point ToLayoutPosition(Point physicalPosition, double desktopScale) =>
        new(physicalPosition.X / desktopScale, physicalPosition.Y / desktopScale);

    internal static Rect ToPhysical(Rect layoutBounds, double desktopScale, double windowScale) =>
        new(layoutBounds.X * desktopScale, layoutBounds.Y * desktopScale,
            layoutBounds.Width * windowScale, layoutBounds.Height * windowScale);

    internal static Rect ToLayout(Rect physicalBounds, double desktopScale, double windowScale) =>
        new(physicalBounds.X / desktopScale, physicalBounds.Y / desktopScale,
            physicalBounds.Width / windowScale, physicalBounds.Height / windowScale);
}
