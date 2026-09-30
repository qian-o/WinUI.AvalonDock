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

internal sealed partial class WindowsWindowHostService(FrameworkElement owner) : IWindowHostService
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

    private sealed partial class WindowHost : IDockingWindowHost
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
                    UnsubscribeWindowEvents();
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

                    UnsubscribeWindowEvents();
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
            UnsubscribeWindowEvents();
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

        private void UnsubscribeWindowEvents()
        {
            appWindow.Closing -= OnClosing;
            appWindow.Changed -= OnChanged;
            window.Closed -= OnClosed;
        }

    }
}
