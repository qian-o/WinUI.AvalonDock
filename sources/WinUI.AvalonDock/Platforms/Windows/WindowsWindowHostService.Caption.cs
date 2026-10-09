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
        private int? nativeBorderColor;
        private bool captionColorsApplied;
        private nint? originalCaptionStyle;

        public WindowCaptionMetrics GetCaptionMetrics()
        {
            if (clientCaptionButtons)
            {
                return new WindowCaptionMetrics(36, 0, 0);
            }

            double scale = GetWindowScale(handle);
            return new WindowCaptionMetrics(Math.Max(32, appWindow.TitleBar.Height / scale), appWindow.TitleBar.LeftInset / scale, appWindow.TitleBar.RightInset / scale);
        }

        public void SetCaptionLayout(WindowCaptionLayout? layout)
        {
            if (IsClosed)
            {
                return;
            }

            if (layout == null)
            {
                if (!customCaption)
                {
                    return;
                }

                captionDragRegions = [];
                captionInteractiveRegions = [];
                if (captionInput != null)
                {
                    captionInput.RegionsChanged -= OnCaptionRegionsChanged;
                }

                captionInput?.ClearRegionRects(NonClientRegionKind.Caption);
                captionInput?.ClearRegionRects(NonClientRegionKind.Passthrough);
                customCaption = false;
                if (clientCaptionButtons)
                {
                    SetClientCaption(false);
                    nativeBorderColor = null;
                }

                clientCaptionButtons = false;
                ResetCaptionColors();
                if (window.ExtendsContentIntoTitleBar)
                {
                    window.ExtendsContentIntoTitleBar = false;
                }
                SetNativeBorderColor(false);
                return;
            }
            if (!customCaption)
            {
                customCaption = true;
                captionInput = InputNonClientPointerSource.GetForWindowId(appWindow.Id);
                captionInput.RegionsChanged += OnCaptionRegionsChanged;
            }
            if (clientCaptionButtons != layout.HasClientButtons)
            {
                clientCaptionButtons = layout.HasClientButtons;
                SetClientCaption(clientCaptionButtons);
                nativeBorderColor = null;
            }
            if (!clientCaptionButtons && !window.ExtendsContentIntoTitleBar)
            {
                window.ExtendsContentIntoTitleBar = true;
            }
            double scale = GetWindowScale(handle);
            global::Windows.Graphics.RectInt32 Physical(Rect rectangle) => new((int)Math.Floor(rectangle.X * scale), (int)Math.Floor(rectangle.Y * scale),
                Math.Max(0, (int)Math.Ceiling(rectangle.Width * scale)), Math.Max(0, (int)Math.Ceiling(rectangle.Height * scale)));
            WindowCaptionMetrics metrics = GetCaptionMetrics();
            Rect drag = layout.DragRegion;
            drag.X += metrics.LeftInset;
            drag.Width = Math.Max(0, drag.Width - metrics.LeftInset - metrics.RightInset);
            bool highContrast = new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
            if (clientCaptionButtons || highContrast)
            {
                ResetCaptionColors();
            }
            else
            {
                Color foreground = layout.IsDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                AppWindowTitleBar titleBar = appWindow.TitleBar;
                if (titleBar.ButtonForegroundColor != foreground) titleBar.ButtonForegroundColor = foreground;
                if (titleBar.ButtonInactiveForegroundColor != Microsoft.UI.Colors.Gray) titleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
                if (titleBar.ButtonHoverForegroundColor != foreground) titleBar.ButtonHoverForegroundColor = foreground;
                if (titleBar.ButtonPressedForegroundColor != foreground) titleBar.ButtonPressedForegroundColor = foreground;
                if (titleBar.ButtonBackgroundColor != Microsoft.UI.Colors.Transparent) titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                if (titleBar.ButtonInactiveBackgroundColor != Microsoft.UI.Colors.Transparent) titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                Color hover = global::Windows.UI.Color.FromArgb(30, foreground.R, foreground.G, foreground.B);
                Color pressed = global::Windows.UI.Color.FromArgb(50, foreground.R, foreground.G, foreground.B);
                if (titleBar.ButtonHoverBackgroundColor != hover) titleBar.ButtonHoverBackgroundColor = hover;
                if (titleBar.ButtonPressedBackgroundColor != pressed) titleBar.ButtonPressedBackgroundColor = pressed;
                captionColorsApplied = true;
            }
            SetNativeBorderColor(layout.IsDark && !highContrast);
            EnforceMinimumSize();
            // Native title-bar appearance updates can replace the non-client regions.
            // Install measured caption/input rectangles after those updates.
            captionDragRegions = [Physical(drag)];
            captionInteractiveRegions = layout.InteractiveRegions.Select(Physical).ToArray();
            captionInput?.SetRegionRects(NonClientRegionKind.Caption, captionDragRegions);
            captionInput?.SetRegionRects(NonClientRegionKind.Passthrough, captionInteractiveRegions);
        }

        private void OnCaptionRegionsChanged(InputNonClientPointerSource sender, NonClientRegionsChangedEventArgs args)
        {
            if (!customCaption || IsClosed || captionRegionsQueued)
            {
                return;
            }

            if ((sender.GetRegionRects(NonClientRegionKind.Caption)?.Length ?? 0) != 0
                && (captionInteractiveRegions.Length == 0 || (sender.GetRegionRects(NonClientRegionKind.Passthrough)?.Length ?? 0) != 0))
            {
                return;
            }
            // WinUI may clear its managed non-client regions during activation or
            // appearance/layout changes after our initial request has completed.
            captionRegionsQueued = window.DispatcherQueue.TryEnqueue(() =>
            {
                captionRegionsQueued = false;
                if (!customCaption || IsClosed || captionInput == null)
                {
                    return;
                }

                if (captionDragRegions.Length > 0 && (captionInput.GetRegionRects(NonClientRegionKind.Caption)?.Length ?? 0) == 0)
                {
                    captionInput.SetRegionRects(NonClientRegionKind.Caption, captionDragRegions);
                }

                if (captionInteractiveRegions.Length > 0 && (captionInput.GetRegionRects(NonClientRegionKind.Passthrough)?.Length ?? 0) == 0)
                {
                    captionInput.SetRegionRects(NonClientRegionKind.Passthrough, captionInteractiveRegions);
                }
            });
        }

        private void ResetCaptionColors()
        {
            if (!captionColorsApplied)
            {
                return;
            }
            // Even writing an unchanged value invokes native title-bar rendering.
            // Read the actual properties so consumer changes are still reconciled.
            AppWindowTitleBar titleBar = appWindow.TitleBar;
            if (titleBar.ButtonForegroundColor != null) titleBar.ButtonForegroundColor = null;
            if (titleBar.ButtonInactiveForegroundColor != null) titleBar.ButtonInactiveForegroundColor = null;
            if (titleBar.ButtonHoverForegroundColor != null) titleBar.ButtonHoverForegroundColor = null;
            if (titleBar.ButtonPressedForegroundColor != null) titleBar.ButtonPressedForegroundColor = null;
            if (titleBar.ButtonBackgroundColor != null) titleBar.ButtonBackgroundColor = null;
            if (titleBar.ButtonInactiveBackgroundColor != null) titleBar.ButtonInactiveBackgroundColor = null;
            if (titleBar.ButtonHoverBackgroundColor != null) titleBar.ButtonHoverBackgroundColor = null;
            if (titleBar.ButtonPressedBackgroundColor != null) titleBar.ButtonPressedBackgroundColor = null;
            captionColorsApplied = false;
        }

        private void SetClientCaption(bool enabled)
        {
            const long captionMask = 0x00C00000; // WS_CAPTION
            nint style = GetWindowLongPtrW(handle, -16 /* GWL_STYLE */);
            originalCaptionStyle ??= style & (nint)captionMask;
            nint next = (style & ~(nint)captionMask) | (enabled ? 0 : originalCaptionStyle.Value);
            if (window.ExtendsContentIntoTitleBar)
            {
                window.ExtendsContentIntoTitleBar = false;
            }
            if (next == style)
            {
                return;
            }

            // Change only the library window's caption bits. Keep resize, system menu,
            // minimize and maximize styles; FRAMECHANGED updates the cached frame.
            Marshal.SetLastPInvokeError(0);
            nint previous = SetWindowLongPtrW(handle, -16, next);
            int error = Marshal.GetLastPInvokeError();
            if (previous == 0 && error != 0)
            {
                throw new Win32Exception(error);
            }
            if (!SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0237 /* NOSIZE | NOMOVE | NOZORDER | NOACTIVATE | NOOWNERZORDER | FRAMECHANGED */))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

        private void SetNativeBorderColor(bool dark)
        {
            // 原生外框与默认深色浮窗表面保持同色，保留系统的尺寸调整边框。
            // 浅色和高对比度呈现恢复系统默认颜色；只缓存设置成功的颜色。
            const int borderColorAttribute = 34; // DWMWA_BORDER_COLOR
            int color = dark ? 0x00282828 : unchecked((int)0xFFFFFFFF);
            if (nativeBorderColor == color)
            {
                return;
            }

            if (DwmSetWindowAttribute(handle, borderColorAttribute, ref color, sizeof(int)) >= 0)
            {
                nativeBorderColor = color;
            }
        }

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

        private nint? HitTestClientCaption(nint position)
        {
            NativePoint point = new()
            {
                X = unchecked((short)((long)position & 0xffff)),
                Y = unchecked((short)(((long)position >> 16) & 0xffff))
            };
            if (!ScreenToClient(handle, ref point))
            {
                return null;
            }

            bool Contains(global::Windows.Graphics.RectInt32 area) => point.X >= area.X && point.Y >= area.Y && point.X < area.X + area.Width && point.Y < area.Y + area.Height;
            if (captionInteractiveRegions.Any(Contains))
            {
                return 1;
            }

            return captionDragRegions.Any(Contains) ? 2 : null;
        }

        private void ShowCaptionSystemMenu(DragInputPosition position)
        {
            if (position.Space != DragCoordinateSpace.DesktopPhysicalPixels)
            {
                throw new ArgumentException("A Windows system menu requires physical desktop coordinates.", nameof(position));
            }

            nint menu = GetSystemMenu(handle, false);
            if (menu == 0 || appWindow.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            bool maximized = presenter.State == OverlappedPresenterState.Maximized;
            bool minimized = presenter.State == OverlappedPresenterState.Minimized;
            void Enable(uint command, bool enabled) => EnableMenuItem(menu, command, enabled ? 0u : 1u);
            Enable(0xF120, maximized || minimized);
            Enable(0xF010, !maximized && !minimized);
            Enable(0xF000, presenter.IsResizable && !maximized && !minimized);
            Enable(0xF020, presenter.IsMinimizable && !minimized);
            Enable(0xF030, presenter.IsMaximizable && !maximized);
            uint command = TrackPopupMenuEx(menu, 0x0100, (int)position.X, (int)position.Y, handle, 0);
            if (command != 0 && !IsClosed)
            {
                PostMessageW(handle, 0x0112, command, 0);
            }
        }

    }
}
