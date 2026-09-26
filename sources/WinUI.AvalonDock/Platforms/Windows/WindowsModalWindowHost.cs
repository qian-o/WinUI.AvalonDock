using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsModalWindowHost(Window window, FrameworkElement owner) : IModalWindowHost
{
    private readonly List<nint> disabled = [];
    private bool running;
    private bool closed;
    private bool disposed;
    private nint previousForeground;
    private nint handle;

    public void Run(Size logicalSize)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (running || owner.XamlRoot == null)
        {
            throw new InvalidOperationException("A modal window needs a connected owner and can only run once.");
        }

        handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        nint parent = Win32Interop.GetWindowFromWindowId(owner.XamlRoot.ContentIslandEnvironment.AppWindowId);
        previousForeground = GetForegroundWindow();
        double scale = GetDpiForWindow(parent) / 96d;
        if (scale <= 0)
        {
            scale = 1;
        }

        OverlappedPresenter presenter = (OverlappedPresenter)window.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        window.AppWindow.IsShownInSwitchers = false;
        int placedWidth = -1;
        int placedHeight = -1;
        Place(logicalSize);
        window.Closed += OnClosed;
        EnumThreadWindows(GetCurrentThreadId(), (candidate, _) =>
        {
            if (candidate != handle && IsWindowVisible(candidate) && IsWindowEnabled(candidate))
            {
                disabled.Add(candidate);
            }

            return true;
        }, 0);
        // WinUI's virtualizing lists need a XamlRoot before their intrinsic size is reliable.
        FrameworkElement? content = window.Content as FrameworkElement;
        bool sizeQueued = false;
        if (content != null)
        {
            content.Loaded += OnContentLoaded;
            content.SizeChanged += OnContentSizeChanged;
            content.LayoutUpdated += OnContentLayoutUpdated;
        }
        try
        {
            foreach (nint candidate in disabled)
            {
                EnableWindow(candidate, false);
            }

            window.Activate();
            if (!closed)
            {
                if (content?.IsLoaded == true)
                {
                    ScheduleContentSize();
                }

                running = true;
                // Only exit this nested loop; the application's outer dispatcher remains alive.
                window.DispatcherQueue.RunEventLoop(DispatcherRunOptions.QuitOnlyLocalLoop, null);
            }
        }
        finally
        {
            if (content != null)
            {
                content.Loaded -= OnContentLoaded;
                content.SizeChanged -= OnContentSizeChanged;
                content.LayoutUpdated -= OnContentLayoutUpdated;
            }
            running = false;
            RestoreWindows();
        }

        void OnContentLoaded(object sender, RoutedEventArgs args) => ScheduleContentSize();
        void OnContentSizeChanged(object sender, SizeChangedEventArgs args) => ScheduleContentSize();
        void OnContentLayoutUpdated(object? sender, object args) => ScheduleContentSize();

        void ScheduleContentSize()
        {
            if (closed || disposed || sizeQueued || content?.IsLoaded != true)
            {
                return;
            }

            sizeQueued = true;
            if (!window.DispatcherQueue.TryEnqueue(() =>
            {
                sizeQueued = false;
                if (closed || disposed || content?.IsLoaded != true)
                {
                    return;
                }

                content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size desired = content.DesiredSize;
                if (!double.IsFinite(desired.Width) || !double.IsFinite(desired.Height) || desired.Width <= 0 || desired.Height <= 0)
                {
                    return;
                }

                Place(desired);
            }))
            {
                sizeQueued = false;
            }
        }

        void Place(Size size)
        {
            SizeInt32 frame = window.AppWindow.Size;
            SizeInt32 client = window.AppWindow.ClientSize;
            int width = (int)Math.Ceiling(size.Width * scale) + Math.Max(0, frame.Width - client.Width);
            int height = (int)Math.Ceiling(size.Height * scale) + Math.Max(0, frame.Height - client.Height);
            if (width == placedWidth && height == placedHeight)
            {
                return;
            }

            if (!GetWindowRect(parent, out NativeRect area))
            {
                return;
            }

            Rect bounds = PlatformServices.WindowGeometry.KeepVisible(new Rect(area.Left + (area.Right - area.Left - width) / 2,
                area.Top + (area.Bottom - area.Top - height) / 2, width, height));
            window.AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height));
            placedWidth = width;
            placedHeight = height;
        }
    }
    private void OnClosed(object sender, WindowEventArgs args)
    {
        closed = true;
        RestoreWindows();
        if (running)
        {
            window.DispatcherQueue.EnqueueEventLoopExit();
        }
    }
    private void RestoreWindows()
    {
        foreach (nint candidate in disabled)
        {
            if (IsWindow(candidate))
            {
                EnableWindow(candidate, true);
            }
        }

        disabled.Clear();
        if ((GetForegroundWindow() == handle || GetForegroundWindow() == 0) && IsWindow(previousForeground))
        {
            SetForegroundWindow(previousForeground);
        }
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        RestoreWindows();
        window.Closed -= OnClosed;
    }
    internal static bool Activate(FrameworkElement element)
    {
        if (element.XamlRoot == null)
        {
            return false;
        }

        nint window = Win32Interop.GetWindowFromWindowId(element.XamlRoot.ContentIslandEnvironment.AppWindowId);
        window = GetAncestor(window, 2);
        if (!IsWindow(window) || !IsWindowEnabled(window))
        {
            return false;
        }

        if (IsIconic(window))
        {
            ShowWindow(window, 9);
        }

        return SetForegroundWindow(window);
    }
    private delegate bool EnumWindow(nint hwnd, nint parameter);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left; internal int Top; internal int Right; internal int Bottom;
    }
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumWindow callback, nint parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint hwnd, bool enable);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);
}
