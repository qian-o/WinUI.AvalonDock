using System.Runtime.InteropServices;
using AvalonDock.Compatibility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsChildWindowHost(HwndHost owner) : IChildWindowHost
{
    [ThreadStatic] private static List<WeakReference<WindowsChildWindowHost>>? nativeHosts;
    private int connectionVersion;
    internal HwndHost Owner => owner;
    internal int ConnectionVersion => connectionVersion;
    internal bool ContainsNativeChild(nint child) => connected && !disconnecting && source == null
        && nativeChild != 0 && IsChild(nativeChild, child);
    internal static WindowsChildWindowHost? FindNativeHost(nint child)
    {
        if (nativeHosts == null)
        {
            return null;
        }

        nativeHosts.RemoveAll(reference => !reference.TryGetTarget(out _));
        return nativeHosts.Select(reference => reference.TryGetTarget(out WindowsChildWindowHost? host) ? host : null)
            .FirstOrDefault(host => host?.ContainsNativeChild(child) == true);
    }
    private DesktopWindowXamlSource? source;
    private UIElement? root;
    private RectInt32 bounds;
    private bool disconnecting;
    private bool connected;
    private bool buildStarted;
    private nint nativeChild;
    private nint nativeParent;
    public UIElement? RootVisual
    {
        get => root;
        set
        {
            root = value;
            if (source != null)
            {
                source.Content = value;
            }
        }
    }

    public void Connect()
    {
        if (connected || source != null || !owner.IsLoaded || owner.Visibility != Visibility.Visible || owner.XamlRoot == null)
        {
            return;
        }

        nint parent = Win32Interop.GetWindowFromWindowId(owner.XamlRoot.ContentIslandEnvironment.AppWindowId);
        nativeParent = parent;
        source = new DesktopWindowXamlSource();
        try
        {
            source.Initialize(Win32Interop.GetWindowIdFromWindow(parent));
            owner.PreparedChild = new HandleRef(owner, Win32Interop.GetWindowFromWindowId(source.SiteBridge.WindowId));
            buildStarted = true;
            owner.BuildNativeHost(new HandleRef(owner, parent));
            if (owner.Handle == 0 || !IsWindow(owner.Handle) || GetParent(owner.Handle) != parent || (GetWindowLongPtrW(owner.Handle, -16) & 0x40000000) == 0)
            {
                throw new InvalidOperationException("BuildWindowCore must return a live child window of the supplied parent.");
            }

            nativeChild = owner.Handle;
            connected = true;
            connectionVersion++;
            if (nativeChild == owner.PreparedChild.Handle)
            {
                source.Content = root;
                source.TakeFocusRequested += OnTakeFocusRequested;
            }
            else
            {
                // A derived host may supply a native child instead of the prepared XAML island.
                source.Dispose();
                source = null;
                owner.PreparedChild = default;
                (nativeHosts ??= []).Add(new WeakReference<WindowsChildWindowHost>(this));
            }
            UpdateBounds();
            if (source != null)
            {
                source.SiteBridge.Show();
                source.SiteBridge.MoveInZOrderAtTop();
            }
            else
            {
                ShowWindow(nativeChild, 5);
            }
        }
        catch (Exception failure)
        {
            try
            {
                Disconnect();
            }
            catch (Exception cleanup) { throw new AggregateException("Content host creation and cleanup both failed.", failure, cleanup); }
            throw;
        }
    }

    public void UpdateBounds()
    {
        if (!connected || !owner.IsLoaded || owner.XamlRoot == null || disconnecting)
        {
            return;
        }

        GeneralTransform transform = owner.TransformToVisual(null);
        Rect rectangle = transform.TransformBounds(new Rect(0, 0, owner.ActualWidth, owner.ActualHeight));
        if (source != null)
        {
            Point originInRoot = transform.TransformPoint(default);
            Point xAxis = transform.TransformPoint(new Point(1, 0));
            Point yAxis = transform.TransformPoint(new Point(0, 1));
            double xScale = xAxis.X - originInRoot.X;
            double yScale = yAxis.Y - originInRoot.Y;
            // A child HWND remains axis-aligned. Uniform positive Viewbox/scale transforms
            // can be reproduced exactly through the XAML island's rasterization override.
            if (xScale > 0 && Math.Abs(xScale - yScale) < .001 && Math.Abs(xAxis.Y - originInRoot.Y) < .001 && Math.Abs(yAxis.X - originInRoot.X) < .001)
            {
                float scale = (float)(owner.XamlRoot.RasterizationScale * xScale);
                if (Math.Abs(source.SiteBridge.OverrideScale - scale) > .001)
                {
                    source.SiteBridge.OverrideScale = scale;
                }
            }
        }
        RectInt32 screen = owner.XamlRoot.CoordinateConverter.ConvertLocalToScreen(rectangle);
        NativePoint origin = new()
        {
            X = screen.X,
            Y = screen.Y
        };
        nint parent = Win32Interop.GetWindowFromWindowId(owner.XamlRoot.ContentIslandEnvironment.AppWindowId);
        if (!ScreenToClient(parent, ref origin))
        {
            return;
        }

        RectInt32 next = new(origin.X, origin.Y, Math.Max(1, screen.Width), Math.Max(1, screen.Height));
        if (bounds.Equals(next))
        {
            return;
        }

        if (source != null)
        {
            source.SiteBridge.MoveAndResize(next);
        }
        else if (!MoveWindow(nativeChild, next.X, next.Y, next.Width, next.Height, true))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        bounds = next;
    }

    public void Disconnect()
    {
        if (source == null && !buildStarted || disconnecting)
        {
            return;
        }

        disconnecting = true;
        nativeHosts?.RemoveAll(reference => !reference.TryGetTarget(out WindowsChildWindowHost? host) || ReferenceEquals(host, this));
        DesktopWindowXamlSource? previous = source;
        try
        {
            if (previous != null)
            {
                previous.TakeFocusRequested -= OnTakeFocusRequested;
            }

            if (buildStarted)
            {
                owner.DestroyNativeHost();
            }
        }
        finally
        {
            try
            {
                if (previous != null)
                {
                    try
                    {
                        previous.Content = null;
                    }
                    finally { previous.Dispose(); }
                }
            }
            finally
            {
                // A throwing custom destroy override must not orphan its validated child.
                if (nativeChild != 0 && IsWindow(nativeChild) && GetParent(nativeChild) == nativeParent)
                {
                    DestroyWindow(nativeChild);
                }

                source = null;
                connected = false;
                buildStarted = false;
                nativeChild = nativeParent = 0;
                root = null;
                bounds = default;
                owner.PreparedChild = default;
                disconnecting = false;
                owner.NotifyDisconnected();
            }
        }
    }
    public void Dispose() => Disconnect();
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(nint parent, nint child);
    private void OnTakeFocusRequested(DesktopWindowXamlSource sender, DesktopWindowXamlSourceTakeFocusRequestedEventArgs args)
    {
        // A floating window has one child content root. Keep keyboard traversal inside it.
        XamlSourceFocusNavigationReason reason = args.Request.Reason == XamlSourceFocusNavigationReason.Last ? XamlSourceFocusNavigationReason.Last : XamlSourceFocusNavigationReason.First;
        sender.NavigateFocus(new XamlSourceFocusNavigationRequest(reason));
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X; internal int Y;
    }
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(nint window, ref NativePoint point);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool MoveWindow(nint window, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
}
