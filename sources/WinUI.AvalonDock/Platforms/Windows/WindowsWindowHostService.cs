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

internal sealed class WindowsWindowHostService(FrameworkElement owner) : IWindowHostService
{
    internal static nint GetNativeHandle(IDockingWindowHost host) => host is WindowHost window ? window.NativeHandle : 0;
    public IDockingWindowHost Attach(Window window, UIElement? content, string title, Rect bounds, bool owned,
        WindowPlacement placement = WindowPlacement.ConstrainToWorkArea)
    {
        try
        {
            window.Title = title;
            if (window is LayoutFloatingWindowControl floating)
            {
                floating.Content = content;
            }
            else
            {
                window.Content = content;
            }

            nint handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            nint ownerHandle = owner.XamlRoot is null ? 0 : Win32Interop.GetWindowFromWindowId(owner.XamlRoot.ContentIslandEnvironment.AppWindowId);
            if (owned && ownerHandle != 0)
            {
                SetWindowLongPtrW(handle, -8, ownerHandle);
            }

            // Platform geometry works in physical desktop pixels. The creation request uses the
            // owner's current logical space; subsequent notifications use the new host's own DPI.
            double scale = GetWindowScale(ownerHandle != 0 ? ownerHandle : handle);
            Rect physicalBounds = new(bounds.X * scale, bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
            Rect visible = placement switch
            {
                WindowPlacement.PreserveBounds => physicalBounds,
                WindowPlacement.RestoreOrCenterScreen => WindowsWindowGeometryService.RestoreOrCenterScreen(physicalBounds, 80 * scale),
                _ => PlatformServices.WindowGeometry.KeepVisible(physicalBounds)
            };
            window.AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(
                (int)Math.Round(visible.X), (int)Math.Round(visible.Y),
                Math.Max(1, (int)Math.Round(visible.Width)), Math.Max(1, (int)Math.Round(visible.Height))));
            return new WindowHost(window, handle, ownerHandle);
        }
        catch
        {
            if (window is not null)
            {
                window.Content = null;
                window.Close();
            }
            Controls.LayoutViewBuilder.Release(content);
            throw;
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SetWindowLongPtrW(nint window, int index, nint value);

    private static double GetWindowScale(nint handle)
    {
        uint dpi = GetDpiForWindow(handle);
        return dpi == 0 ? 1 : dpi / 96d;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(nint window);

    private sealed class WindowHost : IDockingWindowHost
    {
        private readonly Window window;
        private readonly nint handle;
        private readonly nint ownerHandle;
        private readonly AppWindow appWindow;
        private GCHandle ownerReference;
        private bool ownerHookInstalled;
        private bool ownerDetachedForShutdown;
        private bool owned;
        private bool disposing;
        private bool handlingClose;
        private bool hasShown;
        private bool initialShowInProgress;
        private bool pendingMaximized;
        private Rect normalBounds;
        private Size contentMinimum;
        private Thickness resizeBorder;
        private GCHandle selfHandle;
        private bool subclassInstalled;
        private bool nativeMoveActive;
        private bool nativeMoveCancelled;
        private bool nativeResizeStarted;
        private bool customCaption;
        private bool clientCaptionButtons;
        private InputNonClientPointerSource? captionInput;
        private global::Windows.Graphics.RectInt32[] captionDragRegions = [];
        private global::Windows.Graphics.RectInt32[] captionInteractiveRegions = [];
        private bool captionRegionsQueued;
        private long moveSequence;
        private const nuint SubclassId = 0x57414457;
        private static readonly SubclassProcedure NativeProcedure = OnNativeMessage;
        private static readonly SubclassProcedure OwnerProcedure = OnOwnerMessage;

        internal WindowHost(Window window, nint handle, nint ownerHandle)
        {
            this.window = window;
            this.handle = handle;
            this.ownerHandle = ownerHandle;
            appWindow = window.AppWindow;
            owned = ownerHandle != 0 && GetWindowLongPtrW(handle, -8) == ownerHandle;
            normalBounds = ReadLogicalBounds();
            Geometry = new WindowGeometry(normalBounds, ReadActualLogicalSize(), false);
            window.AppWindow.Closing += OnClosing;
            window.AppWindow.Changed += OnChanged;
            window.Closed += OnClosed;
            if (ownerHandle != 0)
            {
                ownerReference = GCHandle.Alloc(this);
                if (!SetWindowSubclass(ownerHandle, OwnerProcedure, unchecked((nuint)handle), unchecked((nuint)GCHandle.ToIntPtr(ownerReference))))
                {
                    ownerReference.Free();
                    appWindow.Closing -= OnClosing;
                    appWindow.Changed -= OnChanged;
                    window.Closed -= OnClosed;
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                ownerHookInstalled = true;
            }
            if (window is LayoutFloatingWindowControl or DetachedAnchorableWindow)
            {
                selfHandle = GCHandle.Alloc(this);
                try
                {
                    if (!SetWindowSubclass(handle, NativeProcedure, SubclassId, unchecked((nuint)GCHandle.ToIntPtr(selfHandle))))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "The floating-window native message hook could not be installed.");
                    }

                    subclassInstalled = true;
                }
                catch
                {
                    if (selfHandle.IsAllocated)
                    {
                        selfHandle.Free();
                    }

                    window.AppWindow.Closing -= OnClosing;
                    window.AppWindow.Changed -= OnChanged;
                    window.Closed -= OnClosed;
                    ReleaseOwnerHook();
                    throw;
                }
            }
        }

        public event EventHandler<CancelEventArgs>? Closing;
        public event EventHandler? Closed;
        public event EventHandler? OwnerClosed;
        public event EventHandler<WindowGeometry>? GeometryChanged;
        public event EventHandler<WindowMoveUpdate>? MoveChanged;
        public event EventHandler? UserResizeStarted;
        public event EventHandler? InteractionCompleted;
        public event EventHandler<WindowCaptionContextRequest>? CaptionContextRequested;
        public bool IsClosed
        {
            get; private set;
        }
        public bool IsVisible => !IsClosed && window.AppWindow?.IsVisible == true;
        internal nint NativeHandle => handle;
        public WindowGeometry Geometry
        {
            get; private set;
        }

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
                if (clientCaptionButtons && appWindow.Presenter is OverlappedPresenter standard)
                {
                    standard.SetBorderAndTitleBar(true, true);
                }

                clientCaptionButtons = false;
                ResetCaptionColors();
                window.ExtendsContentIntoTitleBar = false;
                return;
            }
            if (!customCaption)
            {
                customCaption = true;
                window.ExtendsContentIntoTitleBar = true;
                captionInput = InputNonClientPointerSource.GetForWindowId(appWindow.Id);
                captionInput.RegionsChanged += OnCaptionRegionsChanged;
            }
            if (clientCaptionButtons != layout.HasClientButtons)
            {
                clientCaptionButtons = layout.HasClientButtons;
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(true, !clientCaptionButtons);
                }
            }
            double scale = GetWindowScale(handle);
            global::Windows.Graphics.RectInt32 Physical(Rect rectangle) => new((int)Math.Floor(rectangle.X * scale), (int)Math.Floor(rectangle.Y * scale),
                Math.Max(0, (int)Math.Ceiling(rectangle.Width * scale)), Math.Max(0, (int)Math.Ceiling(rectangle.Height * scale)));
            WindowCaptionMetrics metrics = GetCaptionMetrics();
            Rect drag = layout.DragRegion;
            drag.X += metrics.LeftInset;
            drag.Width = Math.Max(0, drag.Width - metrics.LeftInset - metrics.RightInset);
            if (clientCaptionButtons || new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
            {
                ResetCaptionColors();
            }
            else
            {
                Color foreground = layout.IsDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                appWindow.TitleBar.ButtonForegroundColor = foreground;
                appWindow.TitleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
                appWindow.TitleBar.ButtonHoverForegroundColor = foreground;
                appWindow.TitleBar.ButtonPressedForegroundColor = foreground;
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(30, foreground.R, foreground.G, foreground.B);
                appWindow.TitleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(50, foreground.R, foreground.G, foreground.B);
            }
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
            appWindow.TitleBar.ButtonForegroundColor = null;
            appWindow.TitleBar.ButtonInactiveForegroundColor = null;
            appWindow.TitleBar.ButtonHoverForegroundColor = null;
            appWindow.TitleBar.ButtonPressedForegroundColor = null;
            appWindow.TitleBar.ButtonBackgroundColor = null;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = null;
            appWindow.TitleBar.ButtonHoverBackgroundColor = null;
            appWindow.TitleBar.ButtonPressedBackgroundColor = null;
        }

        public void Show()
        {
            if (IsClosed)
            {
                return;
            }

            bool firstShow = !hasShown;
            bool activated = false;
            initialShowInProgress = firstShow;
            try
            {
                window.Activate();
                activated = true;
                if (firstShow && pendingMaximized && window.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
            finally
            {
                hasShown |= activated;
                initialShowInProgress = false;
            }
            UpdateGeometry();
        }

        public void Hide()
        {
            if (!IsClosed)
            {
                window.AppWindow?.Hide();
            }
        }

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

        public void BeginMove()
        {
            if (IsClosed)
            {
                return;
            }
            if (!GetCursorPos(out NativePoint point))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            ReleaseCapture();
            nint coordinates = (nint)((point.X & 0xffff) | (point.Y << 16));
            SendMessageW(handle, 0x00a1, 2, coordinates);
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

        public void Close()
        {
            if (!IsClosed && !handlingClose)
            {
                window.Close();
            }
        }

        public void RequestClose()
        {
            if (!IsClosed && !handlingClose)
            {
                SendMessageW(handle, 0x0010, 0, 0);
            }
        }

        public void SetOptions(bool owned, bool allowMinimize, Size contentMinimum, Thickness resizeBorder)
        {
            if (IsClosed)
            {
                return;
            }

            this.contentMinimum = new Size(Sanitize(contentMinimum.Width), Sanitize(contentMinimum.Height));
            this.resizeBorder = new Thickness(Sanitize(resizeBorder.Left), Sanitize(resizeBorder.Top), Sanitize(resizeBorder.Right), Sanitize(resizeBorder.Bottom));
            this.owned = owned;
            if (!owned)
            {
                ownerDetachedForShutdown = false;
            }

            SetWindowLongPtrW(handle, -8, owned && !ownerDetachedForShutdown ? ownerHandle : 0);
            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMinimizable = allowMinimize;
            }

            if (window.Content is FrameworkElement content)
            {
                content.MinWidth = this.contentMinimum.Width;
                content.MinHeight = this.contentMinimum.Height;
            }
            EnforceMinimumSize();
        }

        public void Dispose()
        {
            disposing = true;
            if (window is LayoutFloatingWindowControl control)
            {
                control.MarkInternalClose();
            }

            Close();
        }

        public void SetBounds(Rect bounds)
        {
            if (IsClosed)
            {
                return;
            }

            double scale = GetWindowScale(handle);
            window.AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(
                (int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale),
                Math.Max(1, (int)Math.Round(bounds.Width * scale)), Math.Max(1, (int)Math.Round(bounds.Height * scale))));
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

        private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (disposing)
            {
                return;
            }
            CancelEventArgs cancellation = new();
            handlingClose = true;
            try
            {
                Closing?.Invoke(this, cancellation);
                args.Cancel = !disposing && cancellation.Cancel;
            }
            finally
            {
                handlingClose = false;
            }
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            ReleaseOwnerHook();
            if (IsClosed)
            {
                return;
            }

            CompleteNativeMove(true);
            IsClosed = true;
            if (captionInput != null)
            {
                captionInput.RegionsChanged -= OnCaptionRegionsChanged;
            }

            captionInput = null;
            captionDragRegions = [];
            captionInteractiveRegions = [];
            ReleaseSubclass(false);
            appWindow.Closing -= OnClosing;
            appWindow.Changed -= OnChanged;
            window.Closed -= OnClosed;
            UIElement? content = window is LayoutFloatingWindowControl floating ? floating.Content : window.Content;
            // Native owner destruction may precede WinUI's Closed notification. Changing
            // AppWindow 关闭后释放内容时，需避免访问已销毁的原生窗口。
            if (window.AppWindow != null)
            {
                window.Content = null;
            }

            Controls.LayoutViewBuilder.Release(content);
            Closed?.Invoke(this, EventArgs.Empty);
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
            return new Rect(position.X / scale, position.Y / scale, size.Width / scale, size.Height / scale);
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

        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(nint window, ref NativePoint point);

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
