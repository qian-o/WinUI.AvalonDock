using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Platforms.Windows;

/// <summary>Renders XAML snapshots through the same WinUI Window's layered HWND.</summary>
internal sealed class WindowsOverlayWindowSurface : IOverlayWindowSurface
{
    private static readonly HashSet<nint> Windows = [];
    private readonly Window window;
    private readonly nint handle;
    private Rect bounds;
    private bool disposed;

    internal WindowsOverlayWindowSurface(Window window, FrameworkElement owner)
    {
        this.window = window;
        handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (owner.XamlRoot == null)
        {
            throw new ArgumentException("An overlay destination must be connected to a window.", nameof(owner));
        }

        ((OverlappedPresenter)window.AppWindow.Presenter).SetBorderAndTitleBar(false, false);
        window.AppWindow.IsShownInSwitchers = false;
        // The coordinator owns lifetime. Native ownership can destroy a WinUI HWND before
        // XAML closes its Window, leaving the runtime with an invalid composition host.
        SetWindowLongPtrW(handle, -16, GetWindowLongPtrW(handle, -16) & ~0x00C40000);
        // Layered + transparent passes input through the complete window, including opaque pixels.
        // NOACTIVATE protects the editor focus when the overlay is shown or repositioned.
        SetWindowLongPtrW(handle, -20, GetWindowLongPtrW(handle, -20) | 0x080800A0);
        Check(SetWindowPos(handle, 0, 0, 0, 0, 0, 0x37));
        HideCompositionChildren();
        window.Closed += OnClosed;
        lock (Windows)
        {
            Windows.Add(handle);
        }
    }

    internal static bool IsOverlay(nint handle)
    {
        lock (Windows)
        {
            return Windows.Contains(handle);
        }
    }
    public bool IsVisible
    {
        get; private set;
    }
    public bool IsClosed => disposed || !IsWindow(handle);

    public void Show(Rect screenBounds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(screenBounds.X) || !double.IsFinite(screenBounds.Y)
            || !double.IsFinite(screenBounds.Width) || !double.IsFinite(screenBounds.Height)
            || screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenBounds));
        }

        bounds = screenBounds;
        window.AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(
            (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y), (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height)));
        if (!IsVisible)
        {
            window.AppWindow.Show(false);
        }

        HideCompositionChildren();
        // Keep targets above the moving source without activating either window.
        Check(SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, 0x13));
        IsVisible = true;
    }

    public void Present(byte[] premultipliedBgra, int pixelWidth, int pixelHeight)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsVisible)
        {
            return;
        }

        if (pixelWidth <= 0 || pixelHeight <= 0 || premultipliedBgra.Length != checked(pixelWidth * pixelHeight * 4))
        {
            throw new ArgumentException("The overlay frame must contain exactly one premultiplied BGRA pixel per output pixel.", nameof(premultipliedBgra));
        }

        HideCompositionChildren();
        nint screen = GetDC(0);
        if (screen == 0)
        {
            throw new Win32Exception();
        }

        nint memoryDc = CreateCompatibleDC(screen);
        if (memoryDc == 0)
        {
            ReleaseDC(0, screen);
            throw new Win32Exception();
        }
        nint bitmap = 0, previous = 0;
        try
        {
            BitmapInfo info = new()
            {
                Size = 40,
                Width = pixelWidth,
                Height = -pixelHeight,
                Planes = 1,
                Bits = 32
            };
            bitmap = CreateDIBSection(memoryDc, ref info, 0, out nint pixels, 0, 0);
            if (bitmap == 0)
            {
                throw new Win32Exception();
            }

            previous = SelectObject(memoryDc, bitmap);
            if (previous == 0 || previous == -1)
            {
                throw new Win32Exception();
            }

            Marshal.Copy(premultipliedBgra, 0, pixels, premultipliedBgra.Length);
            NativePoint destination = new()
            {
                X = (int)Math.Round(bounds.X),
                Y = (int)Math.Round(bounds.Y)
            };
            NativePoint size = new()
            {
                X = pixelWidth,
                Y = pixelHeight
            };
            NativePoint source = new();
            Blend blend = new()
            {
                Alpha = 255,
                Format = 1
            };
            Check(UpdateLayeredWindow(handle, screen, ref destination, ref size, memoryDc, ref source, 0, ref blend, 2));
        }
        finally
        {
            if (previous != 0 && previous != -1)
            {
                SelectObject(memoryDc, previous);
            }

            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            DeleteDC(memoryDc);
            ReleaseDC(0, screen);
        }
    }

    public void Hide()
    {
        if (IsClosed)
        {
            Dispose();
            return;
        }
        window.AppWindow?.Hide();
        IsVisible = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        IsVisible = false;
        lock (Windows)
        {
            Windows.Remove(handle);
        }

        window.Closed -= OnClosed;
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();
    private void HideCompositionChildren()
    {
        // WinUI's opaque DirectComposition child cannot supply per-pixel desktop transparency.
        // Keep its XAML tree alive for layout/snapshot rendering, but display only the layered frame.
        for (nint child = GetWindow(handle, 5); child != 0; child = GetWindow(child, 2))
        {
            ShowWindow(child, 0);
        }
    }
    private static void Check(bool success)
    {
        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X; internal int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal uint Size; internal int Width; internal int Height; internal ushort Planes; internal ushort Bits; internal uint Compression; internal uint ImageSize; internal int XPixels; internal int YPixels; internal uint Colors; internal uint Important;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Blend
    {
        internal byte Operation; internal byte Flags; internal byte Alpha; internal byte Format;
    }
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll")] private static extern nint SetWindowLongPtrW(nint window, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint memory, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateLayeredWindow(nint window, nint screenDc, ref NativePoint destination, ref NativePoint size, nint dc, ref NativePoint source, uint key, ref Blend blend, uint flags);
}
