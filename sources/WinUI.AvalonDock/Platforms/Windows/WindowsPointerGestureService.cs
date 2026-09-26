using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Platforms.Windows;

/// <summary>Tracks click sequences using Windows timing, pointer positions and DPI-aware system metrics.</summary>
internal sealed class WindowsPointerGestureService : IPointerGestureService
{
    private readonly ConditionalWeakTable<object, ClickHistory> history = new();
    private WeakReference<object>? previousOrigin;

    public int RegisterPrimaryPress(object origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!GetCursorPos(out NativePoint position))
        {
            history.Remove(origin);
            previousOrigin = null;
            return 1;
        }

        ClickHistory state = history.GetValue(origin, _ => new ClickHistory());
        long timestamp = Stopwatch.GetTimestamp();
        uint dpi = GetOriginDpi(origin);
        bool sameOrigin = previousOrigin?.TryGetTarget(out object? previous) == true && ReferenceEquals(previous, origin);
        double elapsed = state.Count == 0 ? double.PositiveInfinity : Stopwatch.GetElapsedTime(state.Timestamp, timestamp).TotalMilliseconds;
        bool insideRectangle = Math.Abs((long)position.X - state.Position.X) * 2 < Math.Abs(GetSystemMetricsForDpi(36, dpi))
            && Math.Abs((long)position.Y - state.Position.Y) * 2 < Math.Abs(GetSystemMetricsForDpi(37, dpi));
        state.Count = sameOrigin && elapsed <= GetDoubleClickTime() && insideRectangle ? state.Count + 1 : 1;
        state.Timestamp = timestamp;
        state.Position = position;
        previousOrigin = new WeakReference<object>(origin);
        return state.Count;
    }

    public Size GetDragThreshold(object origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        uint dpi = GetOriginDpi(origin);
        return new Size(Math.Abs(GetSystemMetricsForDpi(68, dpi)), Math.Abs(GetSystemMetricsForDpi(69, dpi)));
    }

    private static uint GetOriginDpi(object origin)
    {
        if (origin is FrameworkElement { XamlRoot: { } root })
        {
            nint window = Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId);
            uint dpi = GetDpiForWindow(window);
            if (dpi != 0)
            {
                return dpi;
            }
        }
        return GetDpiForSystem();
    }

    private sealed class ClickHistory
    {
        internal NativePoint Position;
        internal long Timestamp;
        internal int Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetDpiForSystem();
}
