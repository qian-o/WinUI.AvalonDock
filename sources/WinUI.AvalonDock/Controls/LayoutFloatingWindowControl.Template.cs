using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Layout;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private FloatingTemplateView templateView;
    private UIElement? windowContent;
    private Style defaultWindowStyle;
    private ResourceDictionary? windowThemeResources;
    private Theme? windowTheme;
    private bool applyingWindowStyle;
    private Size lastDecoration;
    private bool templateClosed;
    private long backgroundToken;

    public static readonly DependencyProperty TemplateProperty = Control.TemplateProperty;
    public ControlTemplate? Template
    {
        get => templateView.Template; set => SetValue(TemplateProperty, value);
    }
    public static readonly DependencyProperty StyleProperty = FrameworkElement.StyleProperty;
    public Style? Style
    {
        get => templateView.Style; set => SetValue(StyleProperty, value);
    }
    public static readonly DependencyProperty BorderBrushProperty = Control.BorderBrushProperty;
    public Brush? BorderBrush
    {
        get => templateView.BorderBrush; set => SetValue(BorderBrushProperty, value);
    }
    public static readonly DependencyProperty BorderThicknessProperty = Control.BorderThicknessProperty;
    public Thickness BorderThickness
    {
        get => templateView.BorderThickness; set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly DependencyProperty PaddingProperty = Control.PaddingProperty;
    public Thickness Padding
    {
        get => templateView.Padding; set => SetValue(PaddingProperty, value);
    }
    public ResourceDictionary Resources
    {
        get => templateView.Resources;
        set
        {
            templateView.Resources = value;
            windowTheme = null;
            windowThemeResources = null;
            UpdateWindowPresentation();
        }
    }
    public bool ApplyTemplate() => templateView.ApplyTemplate();
    public virtual void OnApplyTemplate()
    {
    }
    protected DependencyObject? GetTemplateChild(string childName) => templateView.Part(childName);

    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(templateView), nameof(defaultWindowStyle))]
    private void InitializeWindowTemplate()
    {
        templateView = new FloatingTemplateView(this) { IsTabStop = false };
        ResourceDictionary defaults = new()
        {
            Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/FloatingWindow.xaml")
        };
        Resources.MergedDictionaries.Add(defaults);
        defaultWindowStyle = (Style)defaults["AvalonDockFloatingWindowStyle"];
        templateView.Style = defaultWindowStyle;
        backgroundToken = templateView.RegisterPropertyChangedCallback(Control.BackgroundProperty, (_, _) =>
        {
            if (windowContent is FloatingWindowContentHost content)
            {
                content.UpdatePresentation();
            }
        });
        templateView.LayoutUpdated += OnTemplateLayoutUpdated;
        Closed += OnNativeWindowClosed;
        base.Content = templateView;
    }

    private void UpdateWindowPresentation()
    {
        if (closed || templateClosed || applyingWindowStyle)
        {
            return;
        }

        applyingWindowStyle = true;
        try
        {
            DockingManager? manager = Manager;
            if (!ReferenceEquals(windowTheme, manager?.Theme))
            {
                UpdateThemeResources(windowTheme);
            }

            templateView.RefreshBindings();
            RefreshInheritedPresentation();
            UpdateCaptionPresentation();
            templateView.RequestedTheme = manager?.ActualTheme ?? ElementTheme.Default;
            templateView.DataContext = state.ReadLocalValue(DataContextProperty) == DependencyProperty.UnsetValue ? manager?.DataContext : DataContext;
            if (templateView.ReadLocalValue(StyleProperty) == DependencyProperty.UnsetValue || automaticWindowStyle)
            {
                Style? style = null;
                for (Type? type = GetType(); type != null && style == null; type = type.BaseType)
                {
                    if (Resources.TryGetValue(type, out object? local) && local is Style own)
                    {
                        style = own;
                    }
                    else if (Application.Current.Resources.TryGetValue(type, out object? global) && global is Style application)
                    {
                        style = application;
                    }
                }

                style ??= defaultWindowStyle;
                if (!ReferenceEquals(templateView.Style, style))
                {
                    templateView.Style = style;
                }

                automaticWindowStyle = true;
            }
            Size decoration = TemplateDecoration;
            if (decoration != lastDecoration)
            {
                lastDecoration = decoration;
                ApplyWindowOptions();
            }
        }
        finally { applyingWindowStyle = false; }
    }
    private bool automaticWindowStyle = true;
    private void OnTemplateLayoutUpdated(object? sender, object args)
    {
        UpdateWindowPresentation();
        QueueInitialContentSize();
    }

    /// <summary>
    /// Updates the floating root resources using the original URI-versus-dictionary removal rules.
    /// WinUI requires a copied dictionary for a secondary XamlRoot, but URI themes still remove the
    /// first matching merged dictionary so caller supplied precedence remains source-compatible.
    /// </summary>
    internal virtual void UpdateThemeResources(Theme? oldTheme = null)
    {
        DockingManager? manager = Manager;
        Theme? newTheme = manager?.Theme;
        if (ReferenceEquals(windowTheme, newTheme))
        {
            return;
        }
        // WinUI loads URI sources eagerly. Prepare the new root-owned dictionary before removing
        // the old one, preserving the previous resources if loading fails.
        ResourceDictionary? candidate = ThemeResourceFactory.Create(newTheme);

        if (oldTheme != null)
        {
            if (oldTheme is DictionaryTheme)
            {
                if (windowThemeResources != null)
                {
                    Resources.MergedDictionaries.Remove(windowThemeResources);
                    windowThemeResources = null;
                }
            }
            else
            {
                ResourceDictionary? resourceDictionaryToRemove = Resources.MergedDictionaries
                    .FirstOrDefault(dictionary => dictionary.Source == oldTheme.GetResourceUri());
                if (resourceDictionaryToRemove != null)
                {
                    Resources.MergedDictionaries.Remove(resourceDictionaryToRemove);
                }

                windowThemeResources = null;
            }
        }

        windowTheme = newTheme;
        windowThemeResources = newTheme is DictionaryTheme ? candidate : null;
        if (candidate != null)
        {
            Resources.MergedDictionaries.Add(candidate);
        }
    }
    private bool UpdateTotalMargin(FrameworkElement? editor = null)
    {
        if (host == null || closed || templateClosed || templateView is not { IsLoaded: true, ActualWidth: > 0, ActualHeight: > 0 }
            || windowContent is not FrameworkElement { IsLoaded: true, ActualWidth: > 0, ActualHeight: > 0 } content)
        {
            return false;
        }
        // The original UpdateMargins walks from the editor to its WPF Window.
        // WinUI's editor lives in a child XamlRoot, so measure the host position
        // in the outer template and add the native frame owned by the window service.
        Rect bounds = content.TransformToVisual(templateView).TransformBounds(new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        Thickness frame = host.GetFrameThickness();
        Thickness margin = new(frame.Left + Math.Max(0, bounds.Left), frame.Top + Math.Max(0, bounds.Top),
            frame.Right + Math.Max(0, templateView.ActualWidth - bounds.Right),
            frame.Bottom + Math.Max(0, templateView.ActualHeight - bounds.Bottom));
        // The child XamlRoot is absent from the outer bounds. Keep the source's
        // parent-only bottom sums; its left/right sums are overwritten by the
        // outer ancestors and its top is replaced by the caption row height.
        for (DependencyObject? parent = editor is null ? null : VisualTreeHelper.GetParent(editor); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is FrameworkElement element)
            {
                margin.Bottom += element.Margin.Bottom;
            }

            if (parent is Control control)
            {
                margin.Bottom += control.Padding.Bottom;
            }

            if (parent is Border border)
            {
                margin.Bottom += border.BorderThickness.Bottom;
            }
        }
        if (!initialContentSizeApplied && TotalMargin != margin)
        {
            TotalMargin = margin;
        }

        return true;
    }
    private void OnNativeWindowClosed(object? sender, WindowEventArgs args)
    {
        templateClosed = true;
        Activated -= OnNativeActivated;
        ReleaseInheritedPresentation();
        ReleaseCaptionObservers();
        if (host != null)
        {
            host.CaptionContextRequested -= OnCaptionContextRequested;
        }

        templateView.LayoutUpdated -= OnTemplateLayoutUpdated;
        templateView.UnregisterPropertyChangedCallback(Control.BackgroundProperty, backgroundToken);
        Closed -= OnNativeWindowClosed;
        if (host == null && !closed)
        {
            closed = true;
            LayoutViewBuilder.Release(windowContent);
            OnClosed(EventArgs.Empty);
        }
    }
    private static bool IsTemplateProperty(DependencyProperty dp) => dp == TemplateProperty || dp == StyleProperty || dp == BorderBrushProperty
        || dp == BorderThicknessProperty || dp == PaddingProperty || dp == BackgroundProperty || IsInheritedTemplateProperty(dp);
    private void SetTemplateProperty(DependencyProperty dp, object? value)
    {
        if (dp == StyleProperty)
        {
            automaticWindowStyle = false;
        }

        templateView.SetValue(dp, value);
    }
    private void ClearTemplateProperty(DependencyProperty dp)
    {
        templateView.ClearValue(dp);
        if (dp == StyleProperty)
        {
            automaticWindowStyle = true;
            UpdateWindowPresentation();
        }
    }
    internal Size TemplateDecoration => windowContent is FrameworkElement content && templateView.IsLoaded && content.IsLoaded
        ? new Size(Math.Max(0, templateView.ActualWidth - content.ActualWidth), Math.Max(0, templateView.ActualHeight - content.ActualHeight)) : default;

    [Microsoft.UI.Xaml.Data.Bindable]
    private sealed class FloatingTemplateView(LayoutFloatingWindowControl owner) : ContentControl, INotifyPropertyChanged
    {
        public ILayoutElement Model => owner.Model;
        public bool IsMaximized => owner.IsMaximized;
        public string Title => owner.Title;
        public Window Window => owner;
        public ICommand MaximizeWindowCommand => Microsoft.Windows.Shell.SystemCommands.MaximizeWindowCommand;
        public ICommand RestoreWindowCommand => Microsoft.Windows.Shell.SystemCommands.RestoreWindowCommand;
        public LayoutItem? SingleContentLayoutItem => owner is LayoutDocumentFloatingWindowControl documents ? documents.SingleContentLayoutItem
            : (owner as LayoutAnchorableFloatingWindowControl)?.SingleContentLayoutItem;
        public ICommand? CloseWindowCommand => owner is LayoutDocumentFloatingWindowControl documents ? documents.CloseWindowCommand
            : (owner as LayoutAnchorableFloatingWindowControl)?.CloseWindowCommand;
        public ICommand? HideWindowCommand => owner is LayoutDocumentFloatingWindowControl documents ? documents.HideWindowCommand
            : (owner as LayoutAnchorableFloatingWindowControl)?.HideWindowCommand;
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly Dictionary<string, object?> lastValues = [];
        internal void RefreshBindings()
        {
            if (owner.templateClosed)
            {
                return;
            }

            foreach ((string? name, object? value) in new (string, object?)[] { (nameof(Model), Model), (nameof(IsMaximized), IsMaximized), (nameof(Title), Title),
                (nameof(SingleContentLayoutItem), SingleContentLayoutItem), (nameof(CloseWindowCommand), CloseWindowCommand), (nameof(HideWindowCommand), HideWindowCommand) })
            {
                if (!lastValues.TryGetValue(name, out object? previous) || !Equals(previous, value))
                {
                    lastValues[name] = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                }
            }
        }
        internal DependencyObject? Part(string name) => GetTemplateChild(name);
        protected override void OnApplyTemplate()
        {
            object retained = Content;
            Content = null;
            base.OnApplyTemplate();
            Content = retained;
            owner.AttachCaptionTemplate();
            RefreshBindings();
            owner.OnApplyTemplate();
        }
    }
}
