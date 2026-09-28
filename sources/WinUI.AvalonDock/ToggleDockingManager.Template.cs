// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private readonly Style defaultPaneStyle;
    private bool pinButtonUpdateQueued;

    private void ReapplyThemeStyles()
    {
        if (IsDisposed || !IsLoaded || settingUp)
        {
            return;
        }

        AnchorablePaneControlStyle = Resources.TryGetValue("ToggleAnchorablePaneControlStyle", out object? style) && style is Style paneStyle ? paneStyle
            : Application.Current.Resources.TryGetValue("ToggleAnchorablePaneControlStyle", out style) && style is Style applicationStyle ? applicationStyle : defaultPaneStyle;
        SetupToggleDockButtonBars();
        ApplyInitialToolboxState();
        RefreshButtonStates();
        QueuePinButtonUpdate();
    }

    internal static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (T nested in Visuals<T>(child))
            {
                yield return nested;
            }
        }
    }

    private void QueuePinButtonUpdate()
    {
        if (!IsDisposed && !pinButtonUpdateQueued)
        {
            pinButtonUpdateQueued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                pinButtonUpdateQueued = false;
                UpdatePinButtonsToMinimize();
            });
        }
    }

    private void UpdatePinButtonsToMinimize()
    {
        if (IsDisposed || !IsLoaded)
        {
            return;
        }

        foreach (AnchorablePaneTitle title in Visuals<AnchorablePaneTitle>(this))
        {
            // The source leaves the specialized title's own template buttons alone.
            if (title is ToggleAnchorablePaneTitle)
            {
                continue;
            }

            foreach (Button button in Visuals<Button>(title))
            {
                if (button.Name == "PART_AutoHidePin")
                {
                    ToolTipService.SetToolTip(button, "Minimize");
                    if (button.Content is not Border border)
                    {
                        border = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                        button.Content = border;
                    }
                    if (border.Child == null)
                    {
                        border.Child = CreateMinimizeIcon();
                    }
                }
                else if (button.Name == "PART_HidePin")
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }

            foreach (Controls.DropDownButton dropDown in Visuals<AvalonDock.Controls.DropDownButton>(title))
            {
                dropDown.Visibility = Visibility.Collapsed;
            }

            Grid? grid = Visuals<Grid>(title).FirstOrDefault();
            if (grid == null || grid.Children.OfType<Button>().Any(button => button.Name == "PART_ToggleMenu"))
            {
                continue;
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button? autoHide = grid.Children.OfType<Button>().FirstOrDefault(button => button.Name == "PART_AutoHidePin");
            if (autoHide != null)
            {
                Grid.SetColumn(autoHide, grid.ColumnDefinitions.Count - 1);
            }

            Button menu = CreateThreeDotMenuButton(title);
            Grid.SetColumn(menu, 2);
            grid.Children.Add(menu);
        }
    }

    private static UIElement CreateMinimizeIcon() => new Microsoft.UI.Xaml.Shapes.Path
    {
        Data = new LineGeometry { StartPoint = new Windows.Foundation.Point(2, 11), EndPoint = new Windows.Foundation.Point(11, 11) },
        Stroke = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x55, 0x55, 0x55)),
        StrokeThickness = 1.5,
        Width = 13,
        Height = 13,
        Stretch = Stretch.None
    };

    private Button CreateThreeDotMenuButton(AnchorablePaneTitle title)
    {
        Microsoft.UI.Xaml.Shapes.Path ellipsis = (Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Path xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  Data="M64 360a56 56 0 1 0 0 112 56 56 0 1 0 0-112zm0-160a56 56 0 1 0 0 112 56 56 0 1 0 0-112zM120 96A56 56 0 1 0 8 96a56 56 0 1 0 112 0z" />
            """);
        ellipsis.Stretch = Stretch.Uniform;
        ellipsis.Width = 4;
        ellipsis.Height = 14;
        ellipsis.HorizontalAlignment = HorizontalAlignment.Center;
        ellipsis.VerticalAlignment = VerticalAlignment.Center;
        Button button = new()
        {
            Name = "PART_ToggleMenu",
            Content = ellipsis,
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            IsTabStop = false
        };
        ellipsis.SetBinding(Microsoft.UI.Xaml.Shapes.Shape.FillProperty,
            new Binding { Source = button, Path = new PropertyPath(nameof(Button.Foreground)) });
        ToolTipService.SetToolTip(button, "Options");
        button.Click += (_, _) =>
        {
            if (title.Model is { } model)
            {
                BuildToggleContextMenu(model).ShowAt(button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
            }
        };
        return button;
    }
}
