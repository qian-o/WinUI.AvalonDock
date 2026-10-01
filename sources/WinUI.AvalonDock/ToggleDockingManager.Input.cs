// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using System.Windows.Input;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private UIElement? shortcutRoot;
    private readonly List<(ToggleToolboxCommand Command, VirtualKey Key, VirtualKeyModifiers Modifiers)> shortcuts = [];
    private void RemoveShortcuts()
    {
        shortcutRoot?.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnShortcutKey));
        shortcuts.Clear();
        shortcutRoot = null;
    }
    private void RefreshShortcuts()
    {
        RemoveShortcuts();
        if (IsDisposed || !IsLoaded || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        shortcutRoot = root;
        shortcutRoot.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnShortcutKey), false);
        foreach (KeyValuePair<IToolbox, LayoutAnchorable> pair in toolboxToAnchorable)
        {
            if (!TryParseShortcut(pair.Key.Shortcut, out VirtualKey key, out VirtualKeyModifiers modifiers))
            {
                continue;
            }

            shortcuts.Add((new ToggleToolboxCommand(this, pair.Key), key, modifiers));
        }
    }
    private void OnShortcutKey(object sender, KeyRoutedEventArgs args)
    {
        if (IsDisposed || args.Handled)
        {
            return;
        }

        VirtualKeyModifiers modifiers = PlatformServices.Keyboard.GetModifiers();
        // WPF's RealKey distinguishes modifier sides. WinUI reports the generic key
        // with physical scan/extended metadata, so normalize only these three families.
        VirtualKey key = PlatformServices.Keyboard.NormalizeKey(args.Key, modifiers, args.KeyStatus);
        foreach ((ToggleToolboxCommand Command, VirtualKey Key, VirtualKeyModifiers Modifiers) shortcut in shortcuts.AsEnumerable().Reverse())
        {
            if (shortcut.Key == key && shortcut.Modifiers == modifiers && shortcut.Command.CanExecute(null))
            {
                shortcut.Command.Execute(null);
                args.Handled = true;
                return;
            }
        }
    }
    private static bool TryParseShortcut(string? text, out VirtualKey key, out VirtualKeyModifiers modifiers) =>
        ShortcutGestureParser.TryParse(text, out key, out modifiers);

    // The original KeyBinding invokes this source command. WinUI supplies the
    // keystroke; the command retains its original toolbox lookup and toggle body.
    private sealed class ToggleToolboxCommand : ICommand
    {
        private readonly ToggleDockingManager manager;
        private readonly IToolbox toolbox;

        public ToggleToolboxCommand(ToggleDockingManager manager, IToolbox toolbox)
        {
            this.manager = manager;
            this.toolbox = toolbox;
        }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public bool CanExecute(object? parameter) => !manager.IsDisposed;

        public void Execute(object? parameter)
        {
            if (manager.IsDisposed || !manager.toolboxToAnchorable.TryGetValue(toolbox, out LayoutAnchorable? anchorable))
            {
                return;
            }

            DockZone zone = manager.GetAnchorableZone(anchorable);
            manager.ToggleAnchorable(anchorable, zone);
        }
    }

    internal MenuFlyout BuildToggleContextMenu(LayoutAnchorable anchorable)
    {
        MenuFlyout menu = new();
        MenuFlyoutSubItem move = new()
        {
            Text = global::AvalonDock.Properties.Resources.Toggle_MoveTo
        };
        foreach (DockZone zone in Enum.GetValues<DockZone>())
        {
            move.Items.Add(MenuItem(GetLocalizedZoneName(zone), () => MoveAnchorableToZone(anchorable, zone)));
        }

        menu.Items.Add(move);
        menu.Items.Add(new MenuFlyoutSeparator());
        MenuFlyoutSubItem modes = new()
        {
            Text = global::AvalonDock.Properties.Resources.Toggle_ViewMode
        };
        modes.Items.Add(MenuItem(global::AvalonDock.Properties.Resources.Anchorable_Float, () => FloatAnchorableFromMenu(anchorable),
            AllowFloatingWindows && GetLayoutItemFromModel(anchorable)?.FloatCommand?.CanExecute(null) == true));
        // The pinned WPF MenuItem sets IsChecked but leaves IsCheckable=false, so
        // invoking it must not toggle its checked state before the Click handler.
        MenuFlyoutItem separate = new()
        {
            Text = global::AvalonDock.Properties.Resources.Anchorable_DetachToWindow,
            IsEnabled = AllowDetachedWindows
        };
        if (IsDetached(anchorable))
        {
            separate.Icon = new SymbolIcon { Symbol = Symbol.Accept };
        }

        separate.Click += (_, _) =>
        {
            if (IsDetached(anchorable))
            {
                ReattachAnchorable(anchorable);
            }
            else
            {
                DetachAnchorableToWindow(anchorable);
            }
        };
        modes.Items.Add(separate);
        modes.Items.Add(MenuItem(global::AvalonDock.Properties.Resources.Toggle_Docked, () => DockAnchorableFromMenu(anchorable)));
        modes.Items.Add(MenuItem(global::AvalonDock.Properties.Resources.Toggle_Hidden, () => HideAnchorableFromMenu(anchorable),
            (GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.HideCommand?.CanExecute(null) == true));
        menu.Items.Add(modes);
        return menu;
    }
    internal void DockAnchorableFromMenu(LayoutAnchorable anchorable)
    {
        if (IsDisposed || !ReferenceEquals(anchorable.Root, Layout))
        {
            return;
        }

        if (IsDetached(anchorable))
        {
            ReattachAnchorable(anchorable);
        }
        else if (anchorable.IsAutoHidden)
        {
            ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
        }
        else if (anchorable.IsFloating)
        {
            ICommand? command = (GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.DockCommand;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                if (anchorable.IsAutoHidden)
                {
                    ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
                }
            }
        }
    }
    internal static string GetLocalizedZoneName(DockZone zone) => zone switch
    {
        DockZone.LeftTop => global::AvalonDock.Properties.Resources.Toggle_Zone_LeftTop,
        DockZone.LeftBottom => global::AvalonDock.Properties.Resources.Toggle_Zone_LeftBottom,
        DockZone.RightTop => global::AvalonDock.Properties.Resources.Toggle_Zone_RightTop,
        DockZone.RightBottom => global::AvalonDock.Properties.Resources.Toggle_Zone_RightBottom,
        DockZone.BottomLeft => global::AvalonDock.Properties.Resources.Toggle_Zone_BottomLeft,
        DockZone.BottomRight => global::AvalonDock.Properties.Resources.Toggle_Zone_BottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };
    internal void FloatAnchorableFromMenu(LayoutAnchorable anchorable)
    {
        ICommand? command = GetLayoutItemFromModel(anchorable)?.FloatCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
    internal void HideAnchorableFromMenu(LayoutAnchorable? anchorable)
    {
        if (anchorable == null)
        {
            return;
        }

        ICommand? command = (GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.HideCommand;
        if (command?.CanExecute(null) != true)
        {
            return;
        }

        // The command handles detached windows after cancellation checks. Keep the
        // sidebar button when a hide or close request was canceled.
        command.Execute(null);
        if (anchorable.IsHidden || !ReferenceEquals(anchorable.Root, Layout))
        {
            SetToolboxIsOpen(anchorable);
            RemoveFromAllBars(anchorable);
        }
    }
    private static MenuFlyoutItem MenuItem(string? text, Action action, bool enabled = true)
    {
        MenuFlyoutItem item = new()
        {
            Text = text,
            IsEnabled = enabled
        };
        item.Click += (_, _) => action();
        return item;
    }
    private void ShowHiddenMenu(FrameworkElement target)
    {
        MenuFlyout menu = new();
        foreach (LayoutAnchorable? tool in Layout.Hidden.Where(item => item.Content != null))
        {
            menu.Items.Add(MenuItem(tool.Title, () => RestoreHiddenAnchorable(tool)));
        }

        if (menu.Items.Count != 0)
        {
            menu.ShowAt(target);
        }
    }
    internal void RestoreHiddenAnchorable(LayoutAnchorable anchorable)
    {
        if (IsDisposed || anchorable == null || !anchorable.IsHidden || !ReferenceEquals(anchorable.Root, Layout))
        {
            return;
        }

        DockZone zone = anchorable.Content is IToolbox toolbox ? toolbox.Zone : DockZone.LeftTop;
        Layout.Hidden.Remove(anchorable);
        LayoutAnchorGroup group = new();
        GetLayoutSideForZone(zone).Children.Add(group);
        group.Children.Add(anchorable);
        AddButton(anchorable, zone);
        RefreshButtonStates();
        ToggleAnchorable(anchorable, zone);
    }
}
