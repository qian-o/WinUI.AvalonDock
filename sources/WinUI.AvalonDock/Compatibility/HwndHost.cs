using System.Runtime.InteropServices;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Compatibility;

/// <summary>Adapts the native child-window lifecycle required by AvalonDock's WPF host extension points.</summary>
/// <remarks>The legacy HandleRef hooks remain Windows-specific. Layout and content services do not exchange native handles.</remarks>
public abstract class HwndHost : Control, IDisposable
{
    private readonly IChildWindowHost site;
    private bool disposed;
    private HandleRef handle;

    protected HwndHost()
    {
        IsTabStop = false;
        site = PlatformServices.CreateChildWindowHost(this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LayoutUpdated += OnLayoutUpdated;
    }

    public nint Handle => handle.Handle;
    protected abstract HandleRef BuildWindowCore(HandleRef hwndParent);
    protected abstract void DestroyWindowCore(HandleRef hwnd);
    protected virtual bool HasFocusWithinCore() => HostedRoot?.XamlRoot is { } root && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) != null;
    protected virtual System.Collections.IEnumerator LogicalChildren => HostedRoot == null ? Array.Empty<UIElement>().GetEnumerator() : new[] { HostedRoot }.GetEnumerator();
    internal System.Collections.IEnumerator GetLogicalChildren() => LogicalChildren;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        LayoutUpdated -= OnLayoutUpdated;
        site.Dispose();
    }

    internal HandleRef PreparedChild
    {
        get; set;
    }
    internal event EventHandler? Disconnected;
    internal void NotifyDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);
    internal UIElement? HostedRoot
    {
        get => site.RootVisual; set => site.RootVisual = value;
    }
    internal void BuildNativeHost(HandleRef parent) => handle = BuildWindowCore(parent);
    internal void ConnectHost()
    {
        if (!disposed)
        {
            site.Connect();
        }
    }
    internal void DisconnectHost() => site.Disconnect();
    internal void DestroyNativeHost()
    {
        HandleRef previous = handle;
        handle = default;
        DestroyWindowCore(previous);
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (disposed)
        {
            return;
        }
        // Refresh native layout notifications after a template detaches and reloads this host.
        // Re-register for the current visual tree before tracking its new child-window bounds.
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        site.Connect();
        site.UpdateBounds();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (IsLoaded)
        {
            return;
        }

        site.Disconnect();
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !disposed) { site.Connect(); } });
    }
    private void OnLayoutUpdated(object? sender, object args)
    {
        if (!disposed)
        {
            site.UpdateBounds();
        }
    }
}
