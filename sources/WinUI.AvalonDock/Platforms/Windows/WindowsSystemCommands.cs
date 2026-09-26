// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/Shell/SystemCommands.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsSystemCommands(Window window) : IWindowSystemCommands
{
    private const uint Restore = 0xF120;
    private const uint Move = 0xF010;
    private const uint Size = 0xF000;
    private const uint Minimize = 0xF020;
    private const uint Maximize = 0xF030;

    public void Post(WindowSystemCommand command)
    {
        uint nativeCommand = command switch
        {
            WindowSystemCommand.Close => 0xF060u,
            WindowSystemCommand.Maximize => 0xF030u,
            WindowSystemCommand.Minimize => 0xF020u,
            WindowSystemCommand.Restore => 0xF120u,
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        nint hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return;
        }

        PostMessage(hWnd, 0x0112, nativeCommand, IntPtr.Zero);
    }

    public void ShowSystemMenu(Point screenLocation)
    {
        nint hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return;
        }

        UpdateSystemMenu(hWnd);
        // WinUI uses per-window rasterization. The original helper converts desktop DIPs
        // through WPF DpiHelper; native coordinates remain confined to this adapter.
        double scale = GetDpiForWindow(hWnd) / 96d;
        ShowSystemMenuPhysicalCoordinates(hWnd, new Point(screenLocation.X * scale, screenLocation.Y * scale));
    }

    private static void UpdateSystemMenu(nint hWnd)
    {
        WindowPlacement placement = new()
        {
            Length = (uint)Marshal.SizeOf<WindowPlacement>()
        };
        if (!GetWindowPlacement(hWnd, ref placement))
        {
            return;
        }

        nint hMenu = GetSystemMenu(hWnd, false);
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        const uint MF_ENABLED = 0;
        const uint MF_DISABLED = 0x0003;
        const int GWL_STYLE = -16;
        const uint WS_MINIMIZEBOX = 0x00020000;
        const uint WS_MAXIMIZEBOX = 0x00010000;
        const uint WS_THICKFRAME = 0x00040000;
        const uint SW_SHOWMINIMIZED = 2;
        const uint SW_SHOWMAXIMIZED = 3;
        uint style = unchecked((uint)GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64());
        bool canMinimize = (style & WS_MINIMIZEBOX) != 0;
        bool canMaximize = (style & WS_MAXIMIZEBOX) != 0;
        bool canSize = (style & WS_THICKFRAME) != 0;
        void Enable(uint command, bool enabled) => EnableMenuItem(hMenu, command, enabled ? MF_ENABLED : MF_DISABLED);

        // 按窗口当前状态和样式更新原生菜单项。
        switch (placement.ShowCommand)
        {
            case SW_SHOWMAXIMIZED:
                Enable(Restore, true);
                Enable(Move, false);
                Enable(Size, false);
                Enable(Minimize, canMinimize);
                Enable(Maximize, false);
                break;
            case SW_SHOWMINIMIZED:
                Enable(Restore, true);
                Enable(Move, false);
                Enable(Size, false);
                Enable(Minimize, false);
                Enable(Maximize, canMaximize);
                break;
            default:
                Enable(Restore, false);
                Enable(Move, true);
                Enable(Size, canSize);
                Enable(Minimize, canMinimize);
                Enable(Maximize, canMaximize);
                break;
        }
    }

    private static void ShowSystemMenuPhysicalCoordinates(nint hWnd, Point physicalScreenLocation)
    {
        const uint TPM_RETURNCMD = 0x0100;
        const uint TPM_LEFTBUTTON = 0x0;

        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return;
        }

        nint hMenu = GetSystemMenu(hWnd, false);
        uint cmd = TrackPopupMenuEx(hMenu, TPM_LEFTBUTTON | TPM_RETURNCMD,
            (int)physicalScreenLocation.X, (int)physicalScreenLocation.Y, hWnd, IntPtr.Zero);
        if (cmd != 0)
        {
            PostMessage(hWnd, 0x0112, cmd, IntPtr.Zero);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        internal uint Length, Flags, ShowCommand;
        internal NativePoint MinPosition, MaxPosition;
        internal NativeRect NormalPosition;
    }

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(nint hWnd, ref WindowPlacement placement);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32.dll")] private static extern uint EnableMenuItem(nint menu, uint command, uint flags);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, uint message, nuint command, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern nint GetSystemMenu(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool revert);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hWnd, nint parameters);
}
