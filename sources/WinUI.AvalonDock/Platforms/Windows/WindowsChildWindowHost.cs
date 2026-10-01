using System.Runtime.InteropServices;
using AvalonDock.Compatibility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsChildWindowHost(IChildWindowHostOwner owner) : IChildWindowHost
{
    [ThreadStatic] private static List<WeakReference<WindowsChildWindowHost>>? nativeHosts;
    private int connectionVersion;
    internal FrameworkElement Owner => owner.Element;
    internal int ConnectionVersion => connectionVersion;
    internal bool ContainsNativeChild(nint child) => connected && !disconnecting && source == null
        && nativeChild != 0 && (child == nativeChild || IsChild(nativeChild, child));
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
    private bool semanticContentBuilt;
    private readonly HwndHost? legacyOwner = owner as HwndHost;
    private readonly List<(UIElement Element, long Token)> visibilityObservers = [];
    private readonly List<UIElement> visibilityAncestors = [];
    private bool? nativeVisible;
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
        if (connected || source != null || !owner.Element.IsLoaded || owner.Element.Visibility != Visibility.Visible || owner.Element.XamlRoot == null)
        {
            return;
        }

        FrameworkElement element = owner.Element;
        XamlRoot xamlRoot = element.XamlRoot ?? throw new InvalidOperationException("A child-window host requires a connected XamlRoot.");
        nint parent = Win32Interop.GetWindowFromWindowId(xamlRoot.ContentIslandEnvironment.AppWindowId);
        nativeParent = parent;
        source = new DesktopWindowXamlSource();
        try
        {
            source.Initialize(Win32Interop.GetWindowIdFromWindow(parent));
            if (legacyOwner is { } legacy)
            {
                legacy.PreparedChild = new HandleRef(legacy, Win32Interop.GetWindowFromWindowId(source.SiteBridge.WindowId));
                buildStarted = true;
                legacy.BuildNativeHost(new HandleRef(legacy, parent));
                if (legacy.Handle == 0 || !IsWindow(legacy.Handle) || GetParent(legacy.Handle) != parent || (GetWindowLongPtrW(legacy.Handle, -16) & 0x40000000) == 0)
                {
                    throw new InvalidOperationException("BuildWindowCore must return a live child window of the supplied parent.");
                }

                nativeChild = legacy.Handle;
                if (nativeChild == legacy.PreparedChild.Handle)
                {
                    source.Content = owner.RootVisual;
                    source.TakeFocusRequested += OnTakeFocusRequested;
                }
                else
                {
                    // A derived legacy host may supply a native child instead of the prepared XAML island.
                    source.Dispose();
                    source = null;
                    legacy.PreparedChild = default;
                    (nativeHosts ??= []).Add(new WeakReference<WindowsChildWindowHost>(this));
                }
            }
            else
            {
                owner.BuildHostContent();
                semanticContentBuilt = true;
                source.Content = owner.RootVisual;
                source.TakeFocusRequested += OnTakeFocusRequested;
                nativeChild = Win32Interop.GetWindowFromWindowId(source.SiteBridge.WindowId);
            }

            connected = true;
            connectionVersion++;
            UpdateBounds();
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
        FrameworkElement element = owner.Element;
        if (!connected || !element.IsLoaded || element.XamlRoot == null || disconnecting)
        {
            return;
        }

        ObserveVisibility();
        UpdateVisibility();
        XamlRoot xamlRoot = element.XamlRoot;
        GeneralTransform transform = element.TransformToVisual(null);
        Rect rectangle = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
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
                float scale = (float)(xamlRoot.RasterizationScale * xScale);
                if (Math.Abs(source.SiteBridge.OverrideScale - scale) > .001)
                {
                    source.SiteBridge.OverrideScale = scale;
                }
            }
        }
        RectInt32 screen = xamlRoot.CoordinateConverter.ConvertLocalToScreen(rectangle);
        NativePoint origin = new()
        {
            X = screen.X,
            Y = screen.Y
        };
        nint parent = Win32Interop.GetWindowFromWindowId(xamlRoot.ContentIslandEnvironment.AppWindowId);
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

    private void ObserveVisibility()
    {
        visibilityAncestors.Clear();
        for (DependencyObject? current = owner.Element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement element)
            {
                visibilityAncestors.Add(element);
            }
        }

        bool unchanged = visibilityAncestors.Count == visibilityObservers.Count;
        for (int index = 0; unchanged && index < visibilityAncestors.Count; index++)
        {
            unchanged = ReferenceEquals(visibilityAncestors[index], visibilityObservers[index].Element);
        }
        if (unchanged)
        {
            return;
        }

        ReleaseVisibilityObservers();
        foreach (UIElement element in visibilityAncestors)
        {
            long token = element.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => UpdateVisibility());
            visibilityObservers.Add((element, token));
        }
    }

    private void UpdateVisibility()
    {
        if (!connected || disconnecting)
        {
            return;
        }

        bool visible = owner.Element.IsLoaded && visibilityObservers.All(observer => observer.Element.Visibility == Visibility.Visible);
        if (nativeVisible == visible)
        {
            return;
        }

        if (source != null)
        {
            if (visible)
            {
                source.SiteBridge.Show();
                source.SiteBridge.MoveInZOrderAtTop();
            }
            else
            {
                source.SiteBridge.Hide();
            }
        }
        else
        {
            ShowWindow(nativeChild, visible ? 5 : 0);
        }

        nativeVisible = visible;
    }

    private void ReleaseVisibilityObservers()
    {
        foreach ((UIElement Element, long Token) observer in visibilityObservers)
        {
            observer.Element.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, observer.Token);
        }

        visibilityObservers.Clear();
    }

    public void Disconnect()
    {
        if (source == null && !buildStarted || disconnecting)
        {
            return;
        }

        disconnecting = true;
        ReleaseVisibilityObservers();
        nativeHosts?.RemoveAll(reference => !reference.TryGetTarget(out WindowsChildWindowHost? host) || ReferenceEquals(host, this));
        DesktopWindowXamlSource? previous = source;
        try
        {
            if (previous != null)
            {
                previous.TakeFocusRequested -= OnTakeFocusRequested;
            }

            if (legacyOwner is { } legacy)
            {
                legacy.DestroyNativeHost();
            }
            else if (semanticContentBuilt)
            {
                owner.ReleaseHostContent();
                semanticContentBuilt = false;
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
                nativeVisible = null;
                visibilityAncestors.Clear();
                if (legacyOwner is { } legacy)
                {
                    legacy.PreparedChild = default;
                }
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
