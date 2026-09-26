// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/DetachedAnchorableWindow.cs
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>Hosts one tool presenter in an independent native window.</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class DetachedAnchorableWindow : Window
{
    private readonly Grid root;
    private ContentPresenter? hostedView;
    private readonly IDockingWindowHost host;
    private ResourceDictionary? themeResources;
    private readonly DockingManager? manager;
    private WindowIconPresenter? iconPresenter;
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = [];

    public DetachedAnchorableWindow(LayoutAnchorable model, ContentPresenter hostedView, FrameworkElement? header = null)
    {
        if (model == null || hostedView == null)
        {
            base.Close();
            throw new ArgumentNullException(model == null ? nameof(model) : nameof(hostedView));
        }
        Model = model;
        this.hostedView = hostedView;
        manager = model.Root?.Manager;
        root = new Grid();
        Closed += OnNativeClosed;
        try
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            if (header != null)
            {
                root.Children.Add(header);
            }

            Grid.SetRow(hostedView, 1);
            root.Children.Add(hostedView);
            root.PreviewKeyDown += (_, args) => Model.Root?.Manager?.HandleNavigatorKey(args);
            Content = root;
            host = PlatformServices.CreateWindowHostService(manager ?? (FrameworkElement)root).Attach(this, root, model.Title ?? string.Empty,
                new Rect(model.FloatingLeft, model.FloatingTop, model.FloatingWidth > 0 ? model.FloatingWidth : 400,
                    model.FloatingHeight > 0 ? model.FloatingHeight : 500), false, WindowPlacement.RestoreOrCenterScreen);
            Thickness frame = host.GetFrameThickness();
            host.SetOptions(false, true, new Size(Math.Max(0, 120 - frame.Left - frame.Right), Math.Max(0, 120 - frame.Top - frame.Bottom)), new Thickness(0));
            host.SetMaximized(model.IsMaximized);
            host.Closing += OnNativeClosing;
            host.GeometryChanged += OnGeometryChanged;
            iconPresenter = new WindowIconPresenter(this, root) { Source = model.IconSource };
            Model.PropertyChanged += OnModelChanged;
            if (manager != null)
            {
                manager.ActualThemeChanged += OnManagerThemeChanged;
                foreach (DependencyProperty? property in new[] { FrameworkElement.DataContextProperty, FrameworkElement.FlowDirectionProperty })
                {
                    managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => UpdatePresentation())));
                }
            }
            UpdatePresentation();
        }
        catch
        {
            ReleaseView();
            if (!IsClosed)
            {
                base.Close();
            }

            throw;
        }
    }

    public LayoutAnchorable Model
    {
        get;
    }
    public bool HasView => hostedView != null;
    public bool IsClosed
    {
        get; private set;
    }
    public ContentPresenter? ReleaseView()
    {
        ContentPresenter? view = hostedView;
        hostedView = null;
        if (view != null)
        {
            root.Children.Remove(view);
        }

        return view;
    }
    public void UpdateThemeResources(Theme? oldTheme, Theme? newTheme)
    {
        // WinUI eagerly loads Source and requires a distinct dictionary owner.
        // Prepare the native copy before changing the previous theme resources.
        ResourceDictionary? candidate = ThemeResourceFactory.Create(newTheme);
        ResourceDictionary resources = root.Resources;
        if (oldTheme != null)
        {
            if (themeResources != null)
            {
                resources.MergedDictionaries.Remove(themeResources);
                themeResources = null;
            }
            else
            {
                ResourceDictionary? toRemove = resources.MergedDictionaries.FirstOrDefault(r => r.Source == oldTheme.GetResourceUri());
                if (toRemove != null)
                {
                    resources.MergedDictionaries.Remove(toRemove);
                }
            }
        }

        if (newTheme is DictionaryTheme && candidate is not null)
        {
            themeResources = candidate;
            resources.MergedDictionaries.Add(themeResources);
        }
        else if (candidate != null)
        {
            resources.MergedDictionaries.Add(candidate);
        }
    }
    protected virtual void OnClosing(CancelEventArgs e) => PersistBounds();
    protected virtual void OnClosed(EventArgs e)
    {
    }
    public ResourceDictionary Resources
    {
        get => root.Resources; set => root.Resources = value;
    }
    public ImageSource? Icon
    {
        get => iconPresenter?.Source;
        set
        {
            if (iconPresenter is not null)
            {
                iconPresenter.Source = value;
            }
        }
    }
    public void Show() => host.Show();
    public void Hide() => host.Hide();
    public new void Close()
    {
        if (!IsClosed)
        {
            host.RequestClose();
        }
    }
    public double Left
    {
        get => host.Geometry.Bounds.X; set => host.SetBounds(new Rect(value, Top, Width, Height));
    }
    public double Top
    {
        get => host.Geometry.Bounds.Y; set => host.SetBounds(new Rect(Left, value, Width, Height));
    }
    public double Width
    {
        get => host.Geometry.Bounds.Width; set => host.SetBounds(new Rect(Left, Top, value, Height));
    }
    public double Height
    {
        get => host.Geometry.Bounds.Height; set => host.SetBounds(new Rect(Left, Top, Width, value));
    }
    internal IDockingWindowHost WindowHost => host;
    internal void ActivateHost()
    {
        host.Show();
        PlatformServices.Coordinates.ActivateWindow(root);
    }
    private void OnNativeClosing(object? sender, CancelEventArgs args) => OnClosing(args);
    private void OnNativeClosed(object? sender, WindowEventArgs args)
    {
        IsClosed = true;
        iconPresenter?.Dispose();
        Model.PropertyChanged -= OnModelChanged;
        if (host != null)
        {
            host.Closing -= OnNativeClosing;
            host.GeometryChanged -= OnGeometryChanged;
        }
        if (manager != null)
        {
            manager.ActualThemeChanged -= OnManagerThemeChanged;
            foreach ((DependencyProperty Property, long Token) item in managerTokens)
            {
                manager.UnregisterPropertyChangedCallback(item.Property, item.Token);
            }

            managerTokens.Clear();
        }
        Closed -= OnNativeClosed;
        OnClosed(EventArgs.Empty);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LayoutAnchorable.Title) && !IsClosed)
        {
            Title = Model.Title ?? string.Empty;
        }
        else if (args.PropertyName == nameof(LayoutAnchorable.IconSource) && !IsClosed)
        {
            Icon = Model.IconSource;
        }
    }
    private void OnGeometryChanged(object? sender, WindowGeometry geometry) => PersistBounds();
    private void PersistBounds()
    {
        if (host == null)
        {
            return;
        }

        WindowGeometry geometry = host.Geometry;
        Model.FloatingLeft = geometry.Bounds.X;
        Model.FloatingTop = geometry.Bounds.Y;
        Model.FloatingWidth = geometry.Bounds.Width;
        Model.FloatingHeight = geometry.Bounds.Height;
        Model.IsMaximized = geometry.IsMaximized;
    }
    private void OnManagerThemeChanged(FrameworkElement sender, object args) => UpdatePresentation();
    private void UpdatePresentation()
    {
        if (IsClosed || manager == null)
        {
            return;
        }

        root.DataContext = manager.DataContext;
        root.FlowDirection = manager.FlowDirection;
        root.RequestedTheme = manager.ActualTheme;
    }
}
