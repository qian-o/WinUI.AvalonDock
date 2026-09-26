// Native WinUI presentation for the pinned AvalonDock overlay target rules (MS-PL).
using AvalonDock.Layout;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

internal sealed record OverlayTarget(ILayoutGroup Model, FrameworkElement Area, DropTargetType Type, Rect ScreenBounds);

/// <summary>Reuses one overlay window per destination and closes it when the destination unloads.</summary>
internal sealed class DockingOverlay : IDisposable
{
    private readonly Dictionary<FrameworkElement, OverlayWindow> windows = [];
    private OverlayWindow? active;
    private bool disposed;

    internal OverlayWindow Show(IOverlayWindowHost host, LayoutFloatingWindowControl source)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        FrameworkElement destination = OverlayHost.Element(host) ?? throw new InvalidOperationException("An overlay host requires connected content.");
        if (!destination.IsLoaded || !ReferenceEquals(source.Model?.Root?.Manager, host.Manager) || ReferenceEquals(host, source))
        {
            throw new ArgumentException("The dragged window and destination must be distinct loaded hosts of the same manager.", nameof(source));
        }

        if (!windows.TryGetValue(destination, out OverlayWindow? window) || window.IsClosed)
        {
            window = new OverlayWindow(host);
            window.UpdateThemeResources(host.Manager.Theme);
            windows[destination] = window;
            destination.Unloaded -= OnHostUnloaded;
            destination.Unloaded += OnHostUnloaded;
        }
        if (!ReferenceEquals(active, window) && active != null)
        {
            Release(active);
        }

        active = window;
        ((IOverlayWindow)window).DragEnter(source);
        if (!window.IsVisible)
        {
            window.Show();
        }

        return window;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        KeyValuePair<FrameworkElement, OverlayWindow>[] ownedWindows = windows.ToArray();
        windows.Clear();
        active = null;
        foreach (KeyValuePair<FrameworkElement, OverlayWindow> entry in ownedWindows)
        {
            entry.Key.Unloaded -= OnHostUnloaded;
            entry.Value.CloseHost();
        }
    }

    internal void Hide(IOverlayWindowHost host)
    {
        OverlayHost.InvalidateAreas(host);
        if (OverlayHost.Element(host) is not { } element || !windows.TryGetValue(element, out OverlayWindow? window))
        {
            return;
        }

        window.Hide();
        if (ReferenceEquals(active, window))
        {
            active = null;
        }
    }

    internal void Hide(bool keepSource = false)
    {
        foreach (OverlayWindow window in windows.Values)
        {
            if (keepSource)
            {
                window.Hide();
            }
            else
            {
                Release(window);
            }
        }

        active = null;
    }
    private static void Release(OverlayWindow window)
    {
        if (window.DraggingWindow is { } source)
        {
            ((IOverlayWindow)window).DragLeave(source);
        }
        else
        {
            window.Hide();
        }
    }
    internal void UpdateThemeResources(Theme? theme)
    {
        foreach (OverlayWindow window in windows.Values)
        {
            window.UpdateThemeResources(theme);
        }
    }
    private void OnHostUnloaded(object? sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement host || !windows.Remove(host, out OverlayWindow? window))
        {
            return;
        }

        host.Unloaded -= OnHostUnloaded;
        if (ReferenceEquals(active, window))
        {
            active = null;
        }

        window.CloseHost();
    }
}
