using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AvalonDock.Controls;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace AvalonDock.Platforms.Windows;

internal sealed partial class WindowsWindowHostService
{
    private sealed partial class WindowHost
    {
        private static nint OnOwnerMessage(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint reference)
        {
            WindowHost? target = null;
            try
            {
                target = GCHandle.FromIntPtr(unchecked((nint)reference)).Target as WindowHost;
                if (target == null)
                {
                    return DefSubclassProc(hwnd, message, wParam, lParam);
                }

                if (message == 0x0046 && lParam != 0 && (Marshal.PtrToStructure<NativeWindowPosition>(lParam).Flags & 0x80) != 0
                    && target.owned && GetWindowLongPtrW(target.handle, -8) == hwnd)
                {
                    // Owner HWNDs destroy owned windows before their own WM_DESTROY. Detach
                    // at the preceding hide notification so WinUI can close its own window.
                    SetWindowLongPtrW(target.handle, -8, 0);
                    target.ownerDetachedForShutdown = true;
                    target.window.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (target.IsClosed)
                        {
                            return;
                        }

                        if (!IsWindow(hwnd))
                        {
                            target.Dispose();
                        }
                        else if (IsWindowVisible(hwnd))
                        {
                            target.RestoreOwner();
                        }
                    });
                }
                if (message == 0x0047 && target.ownerDetachedForShutdown && IsWindowVisible(hwnd))
                {
                    target.RestoreOwner();
                }

                if (message == 0x0002 && !target.IsClosed && target.window is DetachedAnchorableWindow)
                {
                    try
                    {
                        target.OwnerClosed?.Invoke(target, EventArgs.Empty);
                    }
                    finally
                    {
                        if (!target.IsClosed)
                        {
                            target.Dispose();
                        }
                    }
                }
                else if (message == 0x0002 && !target.IsClosed && target.owned)
                {
                    SetWindowLongPtrW(target.handle, -8, 0);
                    target.Dispose();
                }
                if (message == 0x0082)
                {
                    target.ReleaseOwnerHook();
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Floating owner lifecycle failed: {exception}");
                if (message == 0x0082)
                {
                    target?.ReleaseOwnerHook();
                }
            }
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }

        private void RestoreOwner()
        {
            if (IsClosed || !owned || !ownerDetachedForShutdown)
            {
                return;
            }

            ownerDetachedForShutdown = false;
            SetWindowLongPtrW(handle, -8, ownerHandle);
        }

        private void ReleaseOwnerHook()
        {
            if (!ownerHookInstalled)
            {
                return;
            }

            ownerHookInstalled = false;
            RemoveWindowSubclass(ownerHandle, OwnerProcedure, unchecked((nuint)handle));
            if (ownerReference.IsAllocated)
            {
                ownerReference.Free();
            }
        }

        private static nint OnNativeMessage(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint reference)
        {
            WindowHost? target = null;
            try
            {
                target = GCHandle.FromIntPtr(unchecked((nint)reference)).Target as WindowHost;
                if (target?.window is LayoutFloatingWindowControl floating)
                {
                    bool handled = false;
                    nint result = floating.ProcessNativeMessage(hwnd, (int)message, unchecked((nint)wParam), lParam, ref handled);
                    if (message == 0x0232)
                    {
                        target.InteractionCompleted?.Invoke(target, EventArgs.Empty);
                    }

                    if (!handled || message is 0x0232 or 0x001F or 0x0082)
                    {
                        if (message == 0x0216)
                        {
                            floating.SetDraggingState(true);
                        }
                        else if (message is 0x0232 or 0x001F or 0x0082)
                        {
                            floating.SetDraggingState(false);
                        }

                        target.ObserveNativeMove(message, wParam);
                    }
                    if (handled && message != 0x0082)
                    {
                        return result;
                    }

                    if (message == 0x0084 && target.HitTestResizeBorder(lParam) is { } hit)
                    {
                        return hit;
                    }

                    if (message == 0x0084 && target.clientCaptionButtons && target.HitTestClientCaption(lParam) is { } captionHit)
                    {
                        return captionHit;
                    }
                }
                if (target != null)
                {
                    if (message == 0x00A5 && wParam == 2 && target.window is LayoutFloatingWindowControl)
                    {
                        DragInputPosition point = new(unchecked((short)((long)lParam & 0xffff)), unchecked((short)(((long)lParam >> 16) & 0xffff)), DragCoordinateSpace.DesktopPhysicalPixels);
                        target.window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (target.IsClosed)
                            {
                                return;
                            }

                            WindowCaptionContextRequest request = new(point);
                            target.CaptionContextRequested?.Invoke(target, request);
                            if (!request.Handled && request.ShowSystemMenu && !target.IsClosed)
                            {
                                target.ShowCaptionSystemMenu(point);
                            }
                        });
                        return 0;
                    }
                    if (message == 0x0082)
                    {
                        target.ReleaseSubclass(true);
                    }

                    if (message == 0x0024 && lParam != 0)
                    {
                        nint result = DefSubclassProc(hwnd, message, wParam, lParam);
                        NativeMinMaxInfo limits = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
                        Size minimum = target.MinimumOuterSize();
                        limits.MinTrackSize.X = Math.Max(limits.MinTrackSize.X, (int)minimum.Width);
                        limits.MinTrackSize.Y = Math.Max(limits.MinTrackSize.Y, (int)minimum.Height);
                        Marshal.StructureToPtr(limits, lParam, false);
                        return result;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Floating-window message processing failed: {exception}");
                if (message == 0x0082)
                {
                    try
                    {
                        target?.ReleaseSubclass(true);
                    }
                    catch (Exception cleanup) { Debug.WriteLine($"Floating-window hook cleanup failed: {cleanup}"); }
                }
            }
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }

        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(nint window, ref NativePoint point);

        private void ObserveNativeMove(uint message, nuint wParam)
        {
            switch (message)
            {
                case 0x0231: // A resize loop is not a docking drag; wait for WM_MOVING.
                    nativeMoveCancelled = false;
                    nativeResizeStarted = false;
                    break;
                case 0x0214:
                    if (!nativeResizeStarted)
                    {
                        nativeResizeStarted = true;
                        UserResizeStarted?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case 0x0216:
                    if (!GetCursorPos(out NativePoint point))
                    {
                        return;
                    }

                    DragInputPosition position = new(point.X, point.Y, DragCoordinateSpace.DesktopPhysicalPixels);
                    if (!nativeMoveActive)
                    {
                        nativeMoveActive = true;
                        moveSequence++;
                        MoveChanged?.Invoke(this, new WindowMoveUpdate(position, WindowMovePhase.Started, moveSequence));
                    }
                    MoveChanged?.Invoke(this, new WindowMoveUpdate(position, WindowMovePhase.Moved, moveSequence));
                    break;
                case 0x0100 when wParam == 0x1B:
                case 0x001F:
                    nativeMoveCancelled = true;
                    CompleteNativeMove(true);
                    break;
                case 0x0232:
                    CompleteNativeMove(nativeMoveCancelled || (GetAsyncKeyState(0x1B) & 0x8000) != 0);
                    break;
                case 0x0082:
                    CompleteNativeMove(true);
                    break;
            }
        }

        [DllImport("user32.dll")] private static extern nint GetSystemMenu(nint window, [MarshalAs(UnmanagedType.Bool)] bool revert);
        [DllImport("user32.dll")] private static extern uint EnableMenuItem(nint menu, uint item, uint flags);
        [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);
        private void CompleteNativeMove(bool cancelled)
        {
            if (!nativeMoveActive)
            {
                return;
            }

            nativeMoveActive = false;
            bool available = GetCursorPos(out NativePoint point);
            WindowMoveUpdate update = new(new DragInputPosition(point.X, point.Y, DragCoordinateSpace.DesktopPhysicalPixels),
                cancelled || !available ? WindowMovePhase.Cancelled : WindowMovePhase.Released, moveSequence);
            // The docking commit may destroy this HWND. Run it after the system's move loop
            // has unwound rather than closing a window from inside WM_EXITSIZEMOVE.
            window.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    MoveChanged?.Invoke(this, update);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Floating-window move completion failed: {exception}");
                }
            });
        }

        private void ReleaseSubclass(bool windowDestroyed)
        {
            if (subclassInstalled && (windowDestroyed || RemoveWindowSubclass(handle, NativeProcedure, SubclassId) || !IsWindow(handle)))
            {
                subclassInstalled = false;
            }

            if (!subclassInstalled && selfHandle.IsAllocated)
            {
                selfHandle.Free();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate nint SubclassProcedure(nint window, uint message, nuint wParam, nint lParam, nuint subclassId, nuint reference);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left; internal int Top; internal int Right; internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWindowPosition
        {
            internal nint Window; internal nint After; internal int X; internal int Y; internal int Width; internal int Height; internal uint Flags;
        }

        [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMinMaxInfo
        {
            internal NativePoint Reserved;
            internal NativePoint MaxSize;
            internal NativePoint MaxPosition;
            internal NativePoint MinTrackSize;
            internal NativePoint MaxTrackSize;
        }

        [DllImport("comctl32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(nint window, SubclassProcedure procedure, nuint id, nuint reference);
        [DllImport("comctl32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(nint window, SubclassProcedure procedure, nuint id);
        [DllImport("comctl32.dll", ExactSpelling = true)] private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
        [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetWindowLongPtrW(nint window, int index);
        [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
        [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rectangle);
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustWindowRectExForDpi(ref NativeRect rectangle, uint style, [MarshalAs(UnmanagedType.Bool)] bool menu, uint extendedStyle, uint dpi);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll", ExactSpelling = true)] private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out NativeRect rect);
        [DllImport("user32.dll", ExactSpelling = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    }
}
