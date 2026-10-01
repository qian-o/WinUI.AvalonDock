// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/WindowHookHandler.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AvalonDock.Platforms.Windows;

internal sealed class FocusChangeEventArgs(nint gotFocusWinHandle) : EventArgs
{
    internal nint GotFocusWinHandle { get; } = gotFocusWinHandle;
}

internal sealed class WindowHookHandler
{
    private nint windowHook;
    private HookCallback? hookProc;
    internal event EventHandler<FocusChangeEventArgs>? FocusChanged;

    internal void Attach()
    {
        hookProc = HookProc;
        windowHook = SetWindowsHookExW(5 /* WH_CBT */, hookProc, 0, GetCurrentThreadId());
        if (windowHook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    internal void Detach()
    {
        if (windowHook == 0)
        {
            return;
        }

        if (!UnhookWindowsHookEx(windowHook))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        windowHook = 0;
        hookProc = null;
    }

    // LRESULT is pointer-sized on Windows x64; preserve the native hook chain.
    private nint HookProc(int code, nint wParam, nint lParam)
    {
        if (code == 9 /* HCBT_SETFOCUS */)
        {
            try
            {
                FocusChanged?.Invoke(this, new FocusChangeEventArgs(wParam));
            }
            catch (Exception exception)
            {
                // Application model callbacks must not escape through the native hook or
                // prevent the remaining hooks from receiving the focus notification.
                Trace.WriteLine($"Docking native focus synchronization failed: {exception}");
            }
        }
        return CallNextHookEx(windowHook, code, wParam, lParam);
    }

    private delegate nint HookCallback(int code, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookExW(int hook, HookCallback callback, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
