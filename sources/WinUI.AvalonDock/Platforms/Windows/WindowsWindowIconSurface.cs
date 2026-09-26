using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsWindowIconSurface : IWindowIconSurface
{
    private readonly nint window;
    private readonly nint originalSmall;
    private readonly nint originalLarge;
    private nint icon;
    private bool disposed;
    internal WindowsWindowIconSurface(Window owner)
    {
        window = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        originalSmall = SendMessageW(window, 0x007F, 0, 0);
        originalLarge = SendMessageW(window, 0x007F, 1, 0);
    }
    public void SetIcon(byte[]? pixels, int width = 0, int height = 0)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        nint next = 0;
        if (pixels != null)
        {
            if (width < 1 || height < 1 || pixels.Length != checked(width * height * 4))
            {
                throw new ArgumentException("Invalid icon raster dimensions.", nameof(pixels));
            }

            BitmapInfo bitmap = new()
            {
                Size = (uint)Marshal.SizeOf<BitmapInfo>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32
            };
            nint color = CreateDIBSection(0, ref bitmap, 0, out nint bits, 0, 0);
            if (color == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            nint mask = CreateBitmap(width, height, 1, 1, new byte[((width + 15) / 16) * 2 * height]);
            try
            {
                if (mask == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                Marshal.Copy(pixels, 0, bits, pixels.Length);
                IconInfo info = new()
                {
                    IsIcon = true,
                    Color = color,
                    Mask = mask
                };
                next = CreateIconIndirect(ref info);
                if (next == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                DeleteObject(color);
                if (mask != 0)
                {
                    DeleteObject(mask);
                }
            }
        }
        if (IsWindow(window))
        {
            SendMessageW(window, 0x0080, 0, next == 0 ? originalSmall : next);
            SendMessageW(window, 0x0080, 1, next == 0 ? originalLarge : next);
        }
        if (icon != 0)
        {
            DestroyIcon(icon);
        }

        icon = next;
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        SetIcon(null);
        disposed = true;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal uint Size; internal int Width, Height; internal ushort Planes, BitCount;
        internal uint Compression, ImageSize; internal int PixelsPerMeterX, PixelsPerMeterY; internal uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] internal bool IsIcon;
        internal uint HotspotX, HotspotY; internal nint Mask, Color;
    }
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateBitmap(int width, int height, uint planes, uint bits, byte[] data);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window, uint message, nuint parameter, nint data);
}
