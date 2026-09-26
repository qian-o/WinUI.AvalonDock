// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutFloatingWindowControl.cs
using System.Runtime.InteropServices;
using AvalonDock.Compatibility;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using PropertyMetadata = Microsoft.UI.Xaml.PropertyMetadata;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    public static readonly DependencyProperty SizeToContentProperty = Register(nameof(SizeToContent), typeof(SizeToContent), SizeToContent.Manual,
        (owner, args) => { if (owner.Content is FloatingWindowContentHost content) { content.SizeToContent = (SizeToContent)args.NewValue; } });
    public SizeToContent SizeToContent
    {
        get => (SizeToContent?)GetValue(SizeToContentProperty) ?? SizeToContent.Manual; set => SetValue(SizeToContentProperty, value);
    }
    public static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty;
    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value);
    }
    public static readonly DependencyProperty DataContextProperty = Register(nameof(DataContext), typeof(object), null,
        (owner, _) => { if (owner.Content is FloatingWindowContentHost content) { content.UpdatePresentation(); } });
    public object? DataContext
    {
        get => GetValue(DataContextProperty); set => SetValue(DataContextProperty, value);
    }
    internal FrameworkElement? HostedRoot => (Content as FloatingWindowContentHost)?.RootVisual as FrameworkElement ?? Content as FrameworkElement;

    /// <summary>Hosts floating content in a native child XAML root through the platform adapter.</summary>
    protected internal class FloatingWindowContentHost : HwndHost
    {
        private readonly LayoutFloatingWindowControl owner;
        private Border? rootPresenter;
        private FrameworkElement? observedContent;
        private bool disposed;
        private bool sizeUpdatePending;
        private bool applyingSize;
        private DockingManager? observedManager;
        private long themeToken;
        private ResourceDictionary? themeDictionary;
        private Theme? currentTheme;

        public FloatingWindowContentHost(LayoutFloatingWindowControl owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            this.owner = owner;
            SizeToContent = owner.SizeToContent;
            DataContextChanged += OnHostDataContextChanged;
            Disconnected += OnHostDisconnected;
        }

        public UIElement? RootVisual => rootPresenter;
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(FloatingWindowContentHost),
            new PropertyMetadata(null, (sender, args) => ((FloatingWindowContentHost)sender).OnContentChanged((UIElement?)args.OldValue, (UIElement?)args.NewValue)));
        public UIElement? Content
        {
            get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value);
        }
        protected virtual void OnContentChanged(UIElement? oldValue, UIElement? newValue)
        {
            if (observedContent != null)
            {
                observedContent.SizeChanged -= OnContentSizeChanged;
            }

            observedContent = newValue as FrameworkElement;
            if (!disposed && observedContent != null)
            {
                observedContent.SizeChanged += OnContentSizeChanged;
            }

            if (rootPresenter != null)
            {
                rootPresenter.Child = newValue;
            }

            InvalidateMeasure();
            InvalidateArrange();
            QueueContentSize();
        }

        public static readonly DependencyProperty SizeToContentProperty = DependencyProperty.Register(nameof(SizeToContent), typeof(SizeToContent), typeof(FloatingWindowContentHost),
            new PropertyMetadata(SizeToContent.Manual, (sender, args) => ((FloatingWindowContentHost)sender).OnSizeToContentChanged((SizeToContent)args.OldValue, (SizeToContent)args.NewValue)));
        public SizeToContent SizeToContent
        {
            get => (SizeToContent?)GetValue(SizeToContentProperty) ?? SizeToContent.Manual; set => SetValue(SizeToContentProperty, value);
        }
        protected virtual void OnSizeToContentChanged(SizeToContent oldValue, SizeToContent newValue)
        {
            InvalidateMeasure();
            QueueContentSize();
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            ReleaseHostedContent();
            rootPresenter = new Border { Child = Content };
            AutomationProperties.SetName(rootPresenter, "FloatingWindowHost");
            rootPresenter.PreviewKeyDown += OnPreviewKeyDown;
            rootPresenter.LayoutUpdated += OnRootLayoutUpdated;
            ObserveManager(owner.Manager);
            UpdatePresentation();
            HostedRoot = rootPresenter;
            QueueContentSize();
            return PreparedChild;
        }
        protected override void DestroyWindowCore(HandleRef hwnd) => ReleaseHostedContent();
        internal void ReleaseHostedContent()
        {
            ObserveManager(null);
            HostedRoot = null;
            if (rootPresenter == null)
            {
                return;
            }

            rootPresenter.PreviewKeyDown -= OnPreviewKeyDown;
            rootPresenter.LayoutUpdated -= OnRootLayoutUpdated;
            rootPresenter.Child = null;
            if (themeDictionary != null)
            {
                rootPresenter.Resources.MergedDictionaries.Remove(themeDictionary);
            }

            currentTheme = null;
            themeDictionary = null;
            rootPresenter = null;
        }
        internal void UpdatePresentation()
        {
            if (rootPresenter == null)
            {
                return;
            }

            rootPresenter.Background = owner.Background;
            rootPresenter.RequestedTheme = owner.Manager?.ActualTheme ?? ElementTheme.Default;
            rootPresenter.DataContext = ReadLocalValue(FrameworkElement.DataContextProperty) != DependencyProperty.UnsetValue ? DataContext
                : owner.ReadLocalValue(LayoutFloatingWindowControl.DataContextProperty) != DependencyProperty.UnsetValue ? owner.DataContext : owner.Manager?.DataContext;
            Theme? theme = owner.Manager?.Theme;
            if (!ReferenceEquals(theme, currentTheme))
            {
                if (themeDictionary != null)
                {
                    rootPresenter.Resources.MergedDictionaries.Remove(themeDictionary);
                }

                currentTheme = theme;
                themeDictionary = ThemeResourceFactory.Create(theme);
                if (themeDictionary != null)
                {
                    rootPresenter.Resources.MergedDictionaries.Add(themeDictionary);
                }
            }
        }
        protected override Size MeasureOverride(Size constraint)
        {
            if (Content == null)
            {
                return base.MeasureOverride(constraint);
            }

            SizeToContent mode = SizeToContent;
            Content.Measure(new Size(mode is SizeToContent.Width or SizeToContent.WidthAndHeight ? double.PositiveInfinity : constraint.Width,
                mode is SizeToContent.Height or SizeToContent.WidthAndHeight ? double.PositiveInfinity : constraint.Height));
            return Content.DesiredSize;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (observedContent != null)
            {
                observedContent.SizeChanged -= OnContentSizeChanged;
            }

            observedContent = null;
            DataContextChanged -= OnHostDataContextChanged;
            UIElement? content = Content;
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                ReleaseHostedContent();
                ClearValue(ContentProperty);
                LayoutViewBuilder.Release(content);
                Disconnected -= OnHostDisconnected;
            }
        }
        private void OnHostDisconnected(object? sender, EventArgs args) => ReleaseHostedContent();
        private void ObserveManager(DockingManager? manager)
        {
            if (ReferenceEquals(observedManager, manager))
            {
                return;
            }

            if (observedManager != null)
            {
                observedManager.DataContextChanged -= OnHostDataContextChanged;
                observedManager.ActualThemeChanged -= OnManagerThemeChanged;
                observedManager.UnregisterPropertyChangedCallback(DockingManager.ThemeProperty, themeToken);
            }
            observedManager = manager;
            if (manager == null)
            {
                return;
            }

            manager.DataContextChanged += OnHostDataContextChanged;
            manager.ActualThemeChanged += OnManagerThemeChanged;
            themeToken = manager.RegisterPropertyChangedCallback(DockingManager.ThemeProperty, (_, _) => UpdatePresentation());
        }
        private void OnHostDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => UpdatePresentation();
        private void OnManagerThemeChanged(FrameworkElement sender, object args) => UpdatePresentation();
        private void OnContentSizeChanged(object? sender, SizeChangedEventArgs args)
        {
            InvalidateMeasure();
            InvalidateArrange();
            QueueContentSize();
        }
        private void OnRootLayoutUpdated(object? sender, object args)
        {
            // Setting a host-local null can suppress the DataContextChanged notification.
            // Reconcile that value-source change on the next actual layout pass.
            ObserveManager(owner.Manager);
            UpdatePresentation();
            QueueContentSize();
        }
        private void OnPreviewKeyDown(object? sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args) => owner.OnPreviewKeyDown(args);
        private void QueueContentSize()
        {
            if (disposed || applyingSize || sizeUpdatePending || SizeToContent == SizeToContent.Manual)
            {
                return;
            }

            sizeUpdatePending = DispatcherQueue.TryEnqueue(() =>
            {
                sizeUpdatePending = false;
                if (disposed || !IsLoaded || owner.IsClosed || Content == null || owner.IsMaximized || SizeToContent == SizeToContent.Manual)
                {
                    return;
                }

                applyingSize = true;
                try
                {
                    SizeToContent mode = SizeToContent;
                    Content.Measure(new Size(mode is SizeToContent.Width or SizeToContent.WidthAndHeight ? double.PositiveInfinity : ActualWidth,
                        mode is SizeToContent.Height or SizeToContent.WidthAndHeight ? double.PositiveInfinity : ActualHeight));
                    Thickness margin = owner.WindowHost?.GetFrameThickness() ?? default;
                    Size decoration = owner.TemplateDecoration;
                    double width = mode is SizeToContent.Width or SizeToContent.WidthAndHeight ? Math.Max(Content.DesiredSize.Width, owner.ContentMinWidth) + margin.Left + margin.Right + decoration.Width : owner.Width;
                    double height = mode is SizeToContent.Height or SizeToContent.WidthAndHeight ? Math.Max(Content.DesiredSize.Height, owner.ContentMinHeight) + margin.Top + margin.Bottom + decoration.Height : owner.Height;
                    if (double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0
                        && (Math.Abs(width - owner.Width) > 1 || Math.Abs(height - owner.Height) > 1))
                    {
                        owner.SetBounds(owner.Left, owner.Top, width, height);
                    }
                }
                finally { applyingSize = false; }
            });
        }
    }
}
