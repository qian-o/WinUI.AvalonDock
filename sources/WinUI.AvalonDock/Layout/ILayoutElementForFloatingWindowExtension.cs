// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutElementForFloatingWindowExtension.cs

using AvalonDock.Platforms;
using Windows.Foundation;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout element for floating window extension.
/// </summary>
public static class ILayoutElementForFloatingWindowExtension
{
    /// <summary>
    /// 使用布局坐标表示矩形的四条边。
    /// </summary>
    private struct RectangleBounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public RectangleBounds(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    internal static void KeepInsideNearestMonitor(this ILayoutElementForFloatingWindow paneInsideFloatingWindow)
    {
        RectangleBounds normalPosition = new();
        normalPosition.Left = (int)paneInsideFloatingWindow.FloatingLeft;
        normalPosition.Top = (int)paneInsideFloatingWindow.FloatingTop;
        normalPosition.Bottom = normalPosition.Top + (int)paneInsideFloatingWindow.FloatingHeight;
        normalPosition.Right = normalPosition.Left + (int)paneInsideFloatingWindow.FloatingWidth;

        // SystemParameters supplies these virtual-screen values in the WPF source.
        Rect screen = PlatformServices.WindowGeometry.GetVirtualScreenBounds();
        RectangleBounds primaryscreen = new((int)screen.Left, (int)screen.Top,
            (int)(screen.Left + screen.Width), (int)(screen.Top + screen.Height));

        if (!RectanglesIntersect(normalPosition, primaryscreen))
        {
            normalPosition = PlaceOnScreen(primaryscreen, normalPosition);

            paneInsideFloatingWindow.FloatingLeft = normalPosition.Left;
            paneInsideFloatingWindow.FloatingTop = normalPosition.Top;
            paneInsideFloatingWindow.FloatingHeight = normalPosition.Bottom - normalPosition.Top;
            paneInsideFloatingWindow.FloatingWidth = normalPosition.Right - normalPosition.Left;

            paneInsideFloatingWindow.RaiseFloatingPropertiesUpdated();
        }
    }

    private static bool RectanglesIntersect(RectangleBounds a, RectangleBounds b)
    {
        if (a.Left > b.Right || a.Right < b.Left)
        {
            return false;
        }

        if (a.Top > b.Bottom || a.Bottom < b.Top)
        {
            return false;
        }

        return true;
    }

    private static RectangleBounds PlaceOnScreen(RectangleBounds monitorRect, RectangleBounds windowRect)
    {
        int monitorWidth = monitorRect.Right - monitorRect.Left;
        int monitorHeight = monitorRect.Bottom - monitorRect.Top;

        if (windowRect.Right < monitorRect.Left)
        {
            int width = windowRect.Right - windowRect.Left;
            if (width > monitorWidth)
            {
                width = monitorWidth;
            }

            windowRect.Left = monitorRect.Left;
            windowRect.Right = windowRect.Left + width;
        }
        else if (windowRect.Left > monitorRect.Right)
        {
            int width = windowRect.Right - windowRect.Left;
            if (width > monitorWidth)
            {
                width = monitorWidth;
            }

            windowRect.Right = monitorRect.Right;
            windowRect.Left = windowRect.Right - width;
        }

        if (windowRect.Bottom < monitorRect.Top)
        {
            int height = windowRect.Bottom - windowRect.Top;
            if (height > monitorHeight)
            {
                height = monitorHeight;
            }

            windowRect.Top = monitorRect.Top;
            windowRect.Bottom = windowRect.Top + height;
        }
        else if (windowRect.Top > monitorRect.Bottom)
        {
            int height = windowRect.Bottom - windowRect.Top;
            if (height > monitorHeight)
            {
                height = monitorHeight;
            }

            windowRect.Bottom = monitorRect.Bottom;
            windowRect.Top = windowRect.Bottom - height;
        }

        return windowRect;
    }
}
