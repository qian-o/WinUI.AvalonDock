using System.Collections;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Compatibility;

/// <summary>Hosts content through a semantic platform adapter without exposing native handles.</summary>
public abstract class ChildWindowHost : Control, IDisposable, IChildWindowHostOwner
{
    private readonly IChildWindowHost site;
    private bool disposed;

    protected ChildWindowHost()
    {
        IsTabStop = false;
        site = PlatformServices.CreateChildWindowHost(this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LayoutUpdated += OnLayoutUpdated;
    }

    FrameworkElement IChildWindowHostOwner.Element => this;
    UIElement? IChildWindowHostOwner.RootVisual
    {
        get => HostedRoot;
        set => HostedRoot = value;
    }

    internal UIElement? HostedRoot
    {
        get => site.RootVisual;
        set => site.RootVisual = value;
    }

    internal IEnumerator GetLogicalChildren() => LogicalChildren;
    protected virtual IEnumerator LogicalChildren => HostedRoot == null ? Array.Empty<UIElement>().GetEnumerator() : new[] { HostedRoot }.GetEnumerator();
    protected virtual bool HasFocusWithinCore() => HostedRoot?.XamlRoot is { } root
        && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) != null;
    protected virtual void OnHostConnected()
    {
    }
    protected virtual void OnHostDisconnected()
    {
    }

    void IChildWindowHostOwner.BuildHostContent() => OnHostConnected();
    void IChildWindowHostOwner.ReleaseHostContent() => OnHostDisconnected();
    void IChildWindowHostOwner.NotifyDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

    internal event EventHandler? Disconnected;

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

    internal void ConnectHost() => site.Connect();
    internal void DisconnectHost() => site.Disconnect();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!disposed)
        {
            site.Connect();
            site.UpdateBounds();
        }
    }


    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (!IsLoaded)
        {
            site.Disconnect();
            DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !disposed) { site.Connect(); } });
        }
    }

    private void OnLayoutUpdated(object? sender, object args)
    {
        if (!disposed)
        {
            site.UpdateBounds();
        }
    }
}
