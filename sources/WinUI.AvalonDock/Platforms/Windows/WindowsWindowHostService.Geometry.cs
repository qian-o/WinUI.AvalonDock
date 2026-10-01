using System.ComponentModel;
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
        public void SetMaximized(bool maximized)
        {
            if (IsClosed || window.AppWindow.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            if (!hasShown)
            {
                pendingMaximized = maximized;
                UpdateGeometry();
                return;
            }
            if (maximized)
            {
                presenter.Maximize();
            }
            else
            {
                presenter.Restore();
            }

            UpdateGeometry();
        }

        public void MoveWithPointer(DragInputPosition pointer, Point anchorOffset)
        {
            if (IsClosed)
            {
                return;
            }

            if (pointer.Space != DragCoordinateSpace.DesktopPhysicalPixels)
            {
                throw new ArgumentException("Windows window movement requires physical desktop coordinates.", nameof(pointer));
            }

            if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized } presenter)
            {
                presenter.Restore();
            }

            double scale = GetWindowScale(handle);
            window.AppWindow.Move(new global::Windows.Graphics.PointInt32(
                (int)Math.Round(pointer.X - anchorOffset.X * scale),
                (int)Math.Round(pointer.Y - anchorOffset.Y * scale)));
        }

        public Point GetPointerAnchor(DragInputPosition pointer)
        {
            if (pointer.Space != DragCoordinateSpace.DesktopPhysicalPixels)
            {
                throw new ArgumentException("Windows window movement requires physical desktop coordinates.", nameof(pointer));
            }

            PointInt32 position = window.AppWindow.Position;
            double scale = GetWindowScale(handle);
            return new Point((pointer.X - position.X) / scale, (pointer.Y - position.Y) / scale);
        }

        public void SetBounds(Rect bounds)
        {
            if (IsClosed)
            {
                return;
            }

            double scale = GetWindowScale(handle);
            PlaceWindow(window, handle, bounds, scale, WindowPlacement.PreserveBounds);
        }

        public Thickness GetFrameThickness()
        {
            if (!GetWindowRect(handle, out NativeRect outer) || !GetClientRect(handle, out NativeRect client))
            {
                return default;
            }

            NativePoint origin = new();
            if (!ClientToScreen(handle, ref origin))
            {
                return default;
            }

            double scale = GetWindowScale(handle);
            return new Thickness((origin.X - outer.Left) / scale, (origin.Y - outer.Top) / scale,
                (outer.Right - origin.X - client.Right) / scale, (outer.Bottom - origin.Y - client.Bottom) / scale);
        }

        private void OnChanged(AppWindow sender, AppWindowChangedEventArgs args) => UpdateGeometry();

        private void UpdateGeometry()
        {
            if (IsClosed || initialShowInProgress)
            {
                return;
            }

            OverlappedPresenterState state = (window.AppWindow.Presenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;
            if (state == OverlappedPresenterState.Restored)
            {
                normalBounds = ReadLogicalBounds();
            }

            WindowGeometry geometry = new(normalBounds, ReadActualLogicalSize(), hasShown ? state == OverlappedPresenterState.Maximized : pendingMaximized,
                hasShown && state == OverlappedPresenterState.Minimized);
            if (geometry == Geometry)
            {
                return;
            }

            Geometry = geometry;
            GeometryChanged?.Invoke(this, geometry);
        }

        private Rect ReadLogicalBounds()
        {
            PointInt32 position = window.AppWindow.Position;
            SizeInt32 size = window.AppWindow.Size;
            double scale = GetWindowScale(handle);
            return WindowsWindowBounds.ToLayout(new Rect(position.X, position.Y, size.Width, size.Height),
                WindowsWindowGeometryService.DesktopScale, scale);
        }

        private Size ReadActualLogicalSize()
        {
            SizeInt32 size = window.AppWindow.Size;
            double scale = GetWindowScale(handle);
            return new Size(size.Width / scale, size.Height / scale);
        }

        private static double Sanitize(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

        private Size MinimumOuterSize()
        {
            uint dpi = GetDpiForWindow(handle);
            if (dpi == 0)
            {
                dpi = 96;
            }

            double scale = dpi / 96d;
            if (customCaption)
            {
                Thickness frame = GetFrameThickness();
                WindowCaptionMetrics caption = GetCaptionMetrics();
                double width = Math.Max(contentMinimum.Width, caption.LeftInset + caption.RightInset + 40);
                return new Size(Math.Ceiling((width + frame.Left + frame.Right) * scale),
                    Math.Ceiling((contentMinimum.Height + frame.Top + frame.Bottom) * scale));
            }
            NativeRect rectangle = new()
            {
                Right = (int)Math.Ceiling(contentMinimum.Width * scale),
                Bottom = (int)Math.Ceiling(contentMinimum.Height * scale)
            };
            uint style = unchecked((uint)(long)GetWindowLongPtrW(handle, -16));
            uint extended = unchecked((uint)(long)GetWindowLongPtrW(handle, -20));
            if (AdjustWindowRectExForDpi(ref rectangle, style, false, extended, dpi))
            {
                return new Size(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
            }

            return new Size(Math.Ceiling(contentMinimum.Width * scale), Math.Ceiling(contentMinimum.Height * scale));
        }

        private void EnforceMinimumSize()
        {
            if (window.AppWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
            {
                return;
            }

            Size minimum = MinimumOuterSize();
            SizeInt32 actual = window.AppWindow.Size;
            int width = Math.Max(actual.Width, (int)minimum.Width);
            int height = Math.Max(actual.Height, (int)minimum.Height);
            if (width != actual.Width || height != actual.Height)
            {
                window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));
            }
        }

        private nint? HitTestResizeBorder(nint coordinates)
        {
            if (resizeBorder == default || window.AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored, IsResizable: true }
                || !GetWindowRect(handle, out NativeRect rectangle))
            {
                return null;
            }

            short x = unchecked((short)((long)coordinates & 0xffff));
            short y = unchecked((short)(((long)coordinates >> 16) & 0xffff));
            if (x < rectangle.Left || x >= rectangle.Right || y < rectangle.Top || y >= rectangle.Bottom)
            {
                return null;
            }

            double scale = GetWindowScale(handle);
            bool left = x < rectangle.Left + resizeBorder.Left * scale;
            bool right = x >= rectangle.Right - resizeBorder.Right * scale;
            bool top = y < rectangle.Top + resizeBorder.Top * scale;
            bool bottom = y >= rectangle.Bottom - resizeBorder.Bottom * scale;
            if (top && left)
            {
                return 13;
            }

            if (top && right)
            {
                return 14;
            }

            if (bottom && left)
            {
                return 16;
            }

            if (bottom && right)
            {
                return 17;
            }

            if (left)
            {
                return 10;
            }

            if (right)
            {
                return 11;
            }

            if (top)
            {
                return 12;
            }

            if (bottom)
            {
                return 15;
            }

            return null;
        }
    }
}
