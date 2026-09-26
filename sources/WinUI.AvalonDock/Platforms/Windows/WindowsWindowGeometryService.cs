using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsWindowGeometryService : IWindowGeometryService
{
    public Rect GetVirtualScreenBounds()
    {
        uint dpi = GetDpiForSystem();
        double scale = dpi == 0 ? 1 : dpi / 96d;
        return new Rect(GetSystemMetrics(76) / scale, GetSystemMetrics(77) / scale,
            GetSystemMetrics(78) / scale, GetSystemMetrics(79) / scale);
    }
    internal static Rect RestoreOrCenterScreen(Rect bounds, double minimumVisible)
    {
        Rect screen = new(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        Rect intersection = bounds;
        intersection.Intersect(screen);
        if ((bounds.X != 0 || bounds.Y != 0) && intersection.Width >= minimumVisible && intersection.Height >= minimumVisible)
        {
            return bounds;
        }

        GetCursorPos(out NativePoint cursor);
        RectInt32 area = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(new global::Windows.Graphics.PointInt32(cursor.X, cursor.Y),
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        return new Rect(area.X + (area.Width - bounds.Width) / 2, area.Y + (area.Height - bounds.Height) / 2, bounds.Width, bounds.Height);
    }
    public Rect KeepVisible(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new global::Windows.Graphics.PointInt32((int)bounds.X, (int)bounds.Y),
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        RectInt32? workArea = displayArea?.WorkArea;
        if (workArea is null)
        {
            return bounds;
        }

        RectInt32 area = workArea.Value;
        double width = Math.Min(bounds.Width, area.Width);
        double height = Math.Min(bounds.Height, area.Height);
        double x = Math.Clamp(bounds.X, area.X, area.X + area.Width - width);
        double y = Math.Clamp(bounds.Y, area.Y, area.Y + area.Height - height);
        return new Rect(x, y, width, height);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X; internal int Y;
    }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
}
