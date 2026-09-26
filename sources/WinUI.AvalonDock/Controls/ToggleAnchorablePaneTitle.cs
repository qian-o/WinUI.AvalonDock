// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ToggleAnchorablePaneTitle.cs.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

public class ToggleAnchorablePaneTitle : AnchorablePaneTitle
{
    private Button? optionsButton;
    private Button? minimizeButton;
    private ToggleDockingManager? boundManager;
    private Binding? minimizeBinding;
    private Binding? optionsBinding;
    private bool refreshing;
    public ToggleAnchorablePaneTitle()
    {
        DefaultStyleKey = typeof(ToggleAnchorablePaneTitle);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/Toggle.xaml");
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) =>
        {
            refreshing = true;
            try
            {
                ClearManagerBindings();
                boundManager = null;
            }
            finally { refreshing = false; }
            DispatcherQueue.TryEnqueue(() => { if (IsLoaded) { Refresh(); } });
        };
        RegisterPropertyChangedCallback(ModelProperty, (_, _) => Refresh());
        RegisterPropertyChangedCallback(ShowOptionsButtonProperty, (_, _) => Refresh());
        RegisterPropertyChangedCallback(ShowMinimizeButtonProperty, (_, _) => Refresh());
    }
    public static readonly DependencyProperty ShowMinimizeButtonProperty = DependencyProperty.Register(nameof(ShowMinimizeButton), typeof(bool), typeof(ToggleAnchorablePaneTitle), new PropertyMetadata(true));
    public bool ShowMinimizeButton
    {
        get => (bool)GetValue(ShowMinimizeButtonProperty); set => SetValue(ShowMinimizeButtonProperty, value);
    }
    public static readonly DependencyProperty ShowOptionsButtonProperty = DependencyProperty.Register(nameof(ShowOptionsButton), typeof(bool), typeof(ToggleAnchorablePaneTitle), new PropertyMetadata(true));
    public bool ShowOptionsButton
    {
        get => (bool)GetValue(ShowOptionsButtonProperty); set => SetValue(ShowOptionsButtonProperty, value);
    }
    protected override void OnApplyTemplate()
    {
        if (optionsButton != null)
        {
            optionsButton.Click -= OnOptionsClick;
        }

        base.OnApplyTemplate();
        optionsButton = GetTemplateChild("PART_OptionsButton") as Button;
        minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
        if (optionsButton != null)
        {
            optionsButton.Click += OnOptionsClick;
        }

        Refresh();
    }
    private void Refresh()
    {
        if (refreshing)
        {
            return;
        }

        refreshing = true;
        try
        {
            ToggleDockingManager? manager = Model?.Root?.Manager as ToggleDockingManager;
            if (!ReferenceEquals(boundManager, manager))
            {
                ClearManagerBindings();
                boundManager = manager;
                if (manager != null)
                {
                    if (ReadLocalValue(ShowMinimizeButtonProperty) == DependencyProperty.UnsetValue)
                    {
                        SetBinding(ShowMinimizeButtonProperty, minimizeBinding = new Binding { Source = manager, Path = new PropertyPath(nameof(manager.ShowHeaderMinimizeButton)) });
                    }

                    if (ReadLocalValue(ShowOptionsButtonProperty) == DependencyProperty.UnsetValue)
                    {
                        SetBinding(ShowOptionsButtonProperty, optionsBinding = new Binding { Source = manager, Path = new PropertyPath(nameof(manager.ShowHeaderOptionsButton)) });
                    }
                }
            }
            if (optionsButton != null)
            {
                optionsButton.Visibility = ShowOptionsButton ? Visibility.Visible : Visibility.Collapsed;
            }

            if (minimizeButton != null)
            {
                minimizeButton.Visibility = ShowMinimizeButton ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        finally { refreshing = false; }
    }
    private void ClearManagerBindings()
    {
        if (minimizeBinding != null && ReferenceEquals(GetBindingExpression(ShowMinimizeButtonProperty)?.ParentBinding, minimizeBinding))
        {
            ClearValue(ShowMinimizeButtonProperty);
        }

        if (optionsBinding != null && ReferenceEquals(GetBindingExpression(ShowOptionsButtonProperty)?.ParentBinding, optionsBinding))
        {
            ClearValue(ShowOptionsButtonProperty);
        }

        minimizeBinding = null;
        optionsBinding = null;
    }
    private void OnOptionsClick(object? sender, RoutedEventArgs args)
    {
        ToggleDockingManager? manager = FindToggleDockingManager();
        if (manager == null || Model == null || sender is not Button button)
        {
            return;
        }

        manager.BuildToggleContextMenu(Model).ShowAt(button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
    }

    private ToggleDockingManager? FindToggleDockingManager()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is ToggleDockingManager manager)
            {
                return manager;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return Model?.Root?.Manager as ToggleDockingManager;
    }
}
