// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private bool settingUp;
    private ToggleDockButtonBar[] bars = [];
    internal ToggleDockButtonBar? leftTopBar, leftBottomBar, rightTopBar, rightBottomBar, bottomLeftBar, bottomRightBar;
    internal Grid? injectedLeftDockPanel, injectedRightDockPanel;
    private Grid? injectedRoot;
    internal FrameworkElement? leftSeparator, rightSeparator;
    private Button? hiddenButton;
    internal IEnumerable<ToggleDockButtonBar> Bars => bars;

    private void SetupToggleDockButtonBars()
    {
        if (IsDisposed || settingUp)
        {
            return;
        }

        settingUp = true;
        try
        {
            RemoveToggleDockButtonBars();
            HideOrdinarySides();
            foreach (LayoutAnchorable? tool in Layout.Descendents().OfType<LayoutAnchorable>().Where(a => a.Parent is LayoutAnchorablePane && !a.IsFloating).ToList())
            {
                tool.ToggleSingleAutoHide();
            }

            leftTopBar = Bar(DockZone.LeftTop);
            leftBottomBar = Bar(DockZone.LeftBottom);
            rightTopBar = Bar(DockZone.RightTop);
            rightBottomBar = Bar(DockZone.RightBottom);
            bottomLeftBar = Bar(DockZone.BottomLeft);
            bottomRightBar = Bar(DockZone.BottomRight);
            // 为拖动几何与按钮状态扫描保留一份按六区顺序排列的快照。
            bars = [leftTopBar, leftBottomBar, rightTopBar, rightBottomBar, bottomLeftBar, bottomRightBar];
            foreach (LayoutAnchorSide side in new[] { Layout.LeftSide, Layout.RightSide, Layout.BottomSide }.OfType<LayoutAnchorSide>())
            {
                foreach (LayoutAnchorable tool in CollectAnchorables(side))
                {
                    DockZone zone = InitialZone(tool, side.Side);
                    GetBarForZone(zone)?.Items.Add(new ToggleDockButton { Anchorable = tool, Zone = zone });
                }
            }
            // The original SetAnchorables populates all six bars before registering
            // toolboxes. Registration order is bar order, including duplicate shortcuts.
            RegisterToolboxesFromBars();
            Grid? root = GetTemplateChild("PART_ToggleNavigationGrid") as Grid
                ?? Visuals<Grid>(this).FirstOrDefault();
            if (root == null)
            {
                return;
            }

            injectedRoot = root;
            injectedLeftDockPanel = SidePanel(leftTopBar, leftBottomBar, bottomLeftBar, true);
            injectedRightDockPanel = SidePanel(rightTopBar, rightBottomBar, bottomRightBar, false);
            Grid.SetRow(injectedLeftDockPanel, 0);
            Grid.SetRowSpan(injectedLeftDockPanel, 3);
            Grid.SetColumn(injectedLeftDockPanel, 0);
            Grid.SetRow(injectedRightDockPanel, 0);
            Grid.SetRowSpan(injectedRightDockPanel, 3);
            Grid.SetColumn(injectedRightDockPanel, 2);
            root.Children.Add(injectedLeftDockPanel);
            root.Children.Add(injectedRightDockPanel);
            UpdateNavigationPanelVisibility();
            RefreshShortcuts();
        }
        finally { settingUp = false; }
    }
    private ToggleDockButtonBar Bar(DockZone zone)
    {
        ToggleDockButtonBar bar = new()
        {
            Zone = zone,
            Orientation = Orientation.Vertical
        };
        AutomationProperties.SetAutomationId(bar, "ToggleDockBar_" + zone);
        return bar;
    }
    private Grid SidePanel(ToggleDockButtonBar top, ToggleDockButtonBar middle, ToggleDockButtonBar bottom, bool left)
    {
        Grid grid = new()
        {
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        }

        Border separator = new()
        {
            Height = 1,
            Margin = new Thickness(4, 6, 4, 6),
            Background = (Brush)Application.Current.Resources["SurfaceStrokeColorDefaultBrush"]
        };
        if (left)
        {
            leftSeparator = separator;
        }
        else
        {
            rightSeparator = separator;
        }

        grid.Children.Add(top);
        grid.Children.Add(separator);
        Grid.SetRow(separator, 1);
        grid.Children.Add(middle);
        Grid.SetRow(middle, 2);
        grid.Children.Add(bottom);
        Grid.SetRow(bottom, 5);
        if (left)
        {
            hiddenButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE712", FontSize = 14 },
                Width = ButtonSize,
                Height = ButtonSize,
                MinWidth = 0,
                MinHeight = 0,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                IsTabStop = false
            };
            if (Resources.TryGetValue("AvalonDockChromeButtonStyle", out object? chromeStyle)
                || Application.Current.Resources.TryGetValue("AvalonDockChromeButtonStyle", out chromeStyle))
            {
                hiddenButton.Style = chromeStyle as Style;
            }
            hiddenButton.SetBinding(WidthProperty, new Binding { Source = this, Path = new PropertyPath(nameof(ButtonSize)) });
            hiddenButton.SetBinding(HeightProperty, new Binding { Source = this, Path = new PropertyPath(nameof(ButtonSize)) });
            ToolTipService.SetToolTip(hiddenButton, global::AvalonDock.Properties.Resources.Toggle_ShowHiddenToolWindows);
            AutomationProperties.SetName(hiddenButton, global::AvalonDock.Properties.Resources.Toggle_ShowHiddenToolWindows);
            hiddenButton.Click += (_, _) => ShowHiddenMenu(hiddenButton);
            grid.Children.Add(hiddenButton);
            Grid.SetRow(hiddenButton, 3);
        }
        return grid;
    }
    private static DockZone InitialZone(LayoutAnchorable tool, AnchorSide side) => side switch
    {
        AnchorSide.Left => tool.Content is IToolbox { Zone: DockZone.LeftBottom } ? DockZone.LeftBottom : DockZone.LeftTop,
        AnchorSide.Right => tool.Content is IToolbox { Zone: DockZone.RightBottom } ? DockZone.RightBottom : DockZone.RightTop,
        _ => tool.Content is IToolbox { Zone: DockZone.BottomRight } ? DockZone.BottomRight : DockZone.BottomLeft
    };
    private void HideOrdinarySides()
    {
        foreach (LayoutAnchorSideControl? side in new[] { LeftSidePanel, RightSidePanel, TopSidePanel, BottomSidePanel })
        {
            if (side != null)
            {
                side.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void RemoveToggleDockButtonBars()
    {
        foreach (IToolbox? toolbox in toolboxToAnchorable.Keys.ToArray())
        {
            UnregisterToolbox(toolbox);
        }

        RemoveShortcuts();
        foreach (ToggleDockButtonBar bar in Bars)
        {
            foreach (ToggleDockButton button in bar.Items.OfType<ToggleDockButton>())
            {
                button.Release();
            }
            bar.Items.Clear();
        }
        if (injectedLeftDockPanel != null)
        {
            injectedRoot?.Children.Remove(injectedLeftDockPanel);
        }

        if (injectedRightDockPanel != null)
        {
            injectedRoot?.Children.Remove(injectedRightDockPanel);
        }

        leftTopBar = leftBottomBar = rightTopBar = rightBottomBar = bottomLeftBar = bottomRightBar = null;
        bars = [];
        injectedLeftDockPanel = injectedRightDockPanel = null;
        leftSeparator = rightSeparator = null;
        injectedRoot = null;
        hiddenButton = null;
    }
    private void AddButton(LayoutAnchorable tool, DockZone zone)
    {
        ToggleDockButtonBar? bar = GetBarForZone(zone);
        if (bar == null || bar.ContainsAnchorable(tool))
        {
            return;
        }

        bar.Items.Add(new ToggleDockButton { Anchorable = tool, Zone = zone });
        if (tool.Content is IToolbox toolbox)
        {
            RegisterToolbox(toolbox, tool);
        }
        UpdateNavigationPanelVisibility();
    }
    internal void RemoveButtonFromAllBars(LayoutAnchorable anchorable) => RemoveFromAllBars(anchorable);
    private void RemoveFromAllBars(LayoutAnchorable anchorable)
    {
        foreach (ToggleDockButtonBar bar in Bars)
        {
            foreach (ToggleDockButton? button in bar.Items.OfType<ToggleDockButton>().Where(item => ReferenceEquals(item.Anchorable, anchorable)).ToArray())
            {
                button.Release();
                bar.Items.Remove(button);
            }
        }
        UpdateNavigationPanelVisibility();
    }
    private void UpdateNavigationPanelVisibility()
    {
        if (injectedLeftDockPanel != null)
        {
            injectedLeftDockPanel.Visibility = Visibility.Visible;
        }

        if (injectedRightDockPanel != null)
        {
            injectedRightDockPanel.Visibility = rightTopBar?.Items.Count > 0 || rightBottomBar?.Items.Count > 0
                || bottomRightBar?.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private DockZone GetAnchorableZone(LayoutAnchorable anchorable)
    {
        if (leftTopBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.LeftTop;
        }

        if (leftBottomBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.LeftBottom;
        }

        if (rightTopBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.RightTop;
        }

        if (rightBottomBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.RightBottom;
        }

        if (bottomLeftBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.BottomLeft;
        }

        if (bottomRightBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.BottomRight;
        }

        // Fallback
        if (anchorable.Parent is LayoutAnchorGroup group && group.Parent is LayoutAnchorSide side)
        {
            switch (side.Side)
            {
                case AnchorSide.Left:
                    return DockZone.LeftTop;
                case AnchorSide.Right:
                    return DockZone.RightTop;
                case AnchorSide.Bottom:
                    return DockZone.BottomLeft;
            }
        }

        return DockZone.LeftTop;
    }
    private LayoutAnchorSide GetLayoutSideForZone(DockZone zone)
    {
        switch (zone)
        {
            case DockZone.LeftTop:
            case DockZone.LeftBottom:
                return Layout.LeftSide ??= new LayoutAnchorSide();
            case DockZone.RightTop:
            case DockZone.RightBottom:
                return Layout.RightSide ??= new LayoutAnchorSide();
            case DockZone.BottomLeft:
            case DockZone.BottomRight:
                return Layout.BottomSide ??= new LayoutAnchorSide();
            default:
                return Layout.LeftSide ??= new LayoutAnchorSide();
        }
    }
    internal ToggleDockButtonBar? GetBarForZone(DockZone zone)
    {
        switch (zone)
        {
            case DockZone.LeftTop:
                return leftTopBar;
            case DockZone.LeftBottom:
                return leftBottomBar;
            case DockZone.RightTop:
                return rightTopBar;
            case DockZone.RightBottom:
                return rightBottomBar;
            case DockZone.BottomLeft:
                return bottomLeftBar;
            case DockZone.BottomRight:
                return bottomRightBar;
            default:
                return leftTopBar;
        }
    }
    private static void RefreshBarStates(ToggleDockButtonBar? bar, object? activeContent)
    {
        if (bar == null)
        {
            return;
        }

        foreach (object? item in bar.Items)
        {
            if (item is ToggleDockButton btn && btn.Anchorable != null)
            {
                btn.IsChecked = !btn.Anchorable.IsAutoHidden;
                btn.IsAnchorableFocused = !btn.Anchorable.IsAutoHidden
                                          && activeContent != null
                                          && activeContent == btn.Anchorable.Content;
            }
        }
    }
    private void RefreshButtonStates()
    {
        if (IsDisposed)
        {
            return;
        }

        object? activeContent = ActiveContent;
        RefreshBarStates(leftTopBar, activeContent);
        RefreshBarStates(leftBottomBar, activeContent);
        RefreshBarStates(rightTopBar, activeContent);
        RefreshBarStates(rightBottomBar, activeContent);
        RefreshBarStates(bottomLeftBar, activeContent);
        RefreshBarStates(bottomRightBar, activeContent);
    }
    private void HideDockedInBar(ToggleDockButtonBar? bar)
    {
        if (bar == null)
        {
            return;
        }

        foreach (object? item in bar.Items)
        {
            if (item is ToggleDockButton btn && btn.Anchorable != null && !btn.Anchorable.IsAutoHidden)
            {
                AutoHideFromDock(btn.Anchorable, bar.Zone);

                // This collapse is a side effect of opening a sibling, so nothing else writes it back.
                // Leaving it out is what used to strand a toolbox at IsOpen == true while it sat on
                // its stripe, after which setting IsOpen = true again raised no change and the
                // toolbox could not be reopened from the view model at all.
                SetToolboxIsOpen(btn.Anchorable);
            }
        }
    }
}
