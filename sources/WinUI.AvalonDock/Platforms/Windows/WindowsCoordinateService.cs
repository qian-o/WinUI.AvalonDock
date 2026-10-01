using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsCoordinateService : ICoordinateService
{
    public bool ActivateWindow(FrameworkElement element) => WindowsModalWindowHost.Activate(element);
    public bool TryGetPointerPosition(out Point position)
    {
        if (GetCursorPos(out NativePoint cursor))
        {
            position = new Point(cursor.X, cursor.Y);
            return true;
        }
        position = default;
        return false;
    }
    public bool IsPointerOver(FrameworkElement element) => GetCursorPos(out NativePoint cursor)
        && TryGetScreenBounds(element, out Rect bounds) && bounds.Contains(new Point(cursor.X, cursor.Y))
        && IsVisibleAt(element, new Point(cursor.X, cursor.Y));
    public bool TryGetScreenBounds(FrameworkElement element, out Rect bounds)
    {
        bounds = default;
        if (!element.IsLoaded || element.XamlRoot is null || element.Visibility != Visibility.Visible)
        {
            return false;
        }
        Rect local = element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        RectInt32 screen = element.XamlRoot.CoordinateConverter.ConvertLocalToScreen(local);
        bounds = new Rect(screen.X, screen.Y, screen.Width, screen.Height);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public bool IsVisibleAt(FrameworkElement element, Point position, IDockingWindowHost? excludedHost = null)
    {
        if (element.XamlRoot is null)
        {
            return false;
        }
        nint expected = Win32Interop.GetWindowFromWindowId(element.XamlRoot.ContentIslandEnvironment.AppWindowId);
        nint hit = WindowFromPoint(new NativePoint { X = (int)position.X, Y = (int)position.Y });
        nint top = GetAncestor(hit, 2);
        nint excluded = excludedHost == null ? 0 : WindowsWindowHostService.GetNativeHandle(excludedHost);
        if (excluded == 0 || top != excluded)
        {
            return top == expected;
        }
        // A moving floating host can cover the intended docking area. Inspect the next visible
        // top-level window in Z order while still respecting unrelated window occlusion.
        for (nint candidate = GetWindow(top, 2); candidate != 0; candidate = GetWindow(candidate, 2))
        {
            if (WindowsOverlayWindowSurface.IsOverlay(candidate) || !IsWindowVisible(candidate) || IsIconic(candidate)
                || DwmGetWindowAttribute(candidate, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0
                || !GetWindowRect(candidate, out NativeRect bounds))
            {
                continue;
            }

            if (position.X >= bounds.Left && position.X < bounds.Right && position.Y >= bounds.Top && position.Y < bounds.Bottom)
            {
                return candidate == expected;
            }
        }
        return false;
    }

    public Point ToHostLogical(FrameworkElement element, Point position)
    {
        double scale = element.XamlRoot?.RasterizationScale ?? 1;
        return new Point(position.X / scale, position.Y / scale);
    }
    public Point ToLayoutPosition(Point position) =>
        WindowsWindowBounds.ToLayoutPosition(position, WindowsWindowGeometryService.DesktopScale);
    public Point ToElementLocal(FrameworkElement element, Point position)
    {
        if (element.XamlRoot == null)
        {
            throw new InvalidOperationException("A coordinate target must have a connected XamlRoot.");
        }

        Point rootPoint = element.XamlRoot.CoordinateConverter.ConvertScreenToLocal(new global::Windows.Graphics.PointInt32((int)Math.Round(position.X), (int)Math.Round(position.Y)));
        return element.TransformToVisual(null).Inverse.TransformPoint(rootPoint);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rectangle);
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left; internal int Top; internal int Right; internal int Bottom;
    }
}
