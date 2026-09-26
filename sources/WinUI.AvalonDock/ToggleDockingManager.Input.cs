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
            Text = "Move To"
        };
        foreach (DockZone zone in Enum.GetValues<DockZone>())
        {
            move.Items.Add(MenuItem(System.Text.RegularExpressions.Regex.Replace(zone.ToString(), "(\\B[A-Z])", " $1"), () => MoveAnchorableToZone(anchorable, zone)));
        }

        menu.Items.Add(move);
        menu.Items.Add(new MenuFlyoutSeparator());
        MenuFlyoutSubItem modes = new()
        {
            Text = "View Mode"
        };
        modes.Items.Add(MenuItem("Float", () =>
        {
            ReattachAnchorable(anchorable);
            if (anchorable.IsAutoHidden)
            {
                anchorable.ToggleSingleAutoHide();
            }

            GetLayoutItemFromModel(anchorable)?.FloatCommand?.Execute(null);
        }, AllowFloatingWindows));
        // The pinned WPF MenuItem sets IsChecked but leaves IsCheckable=false, so
        // invoking it must not toggle its checked state before the Click handler.
        MenuFlyoutItem separate = new()
        {
            Text = "Window",
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
        modes.Items.Add(MenuItem("Docked", () =>
        {
            if (IsDetached(anchorable))
            {
                ReattachAnchorable(anchorable);
            }
            else if (anchorable.IsAutoHidden)
            {
                ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
            }
        }));
        modes.Items.Add(MenuItem("Hidden", () => { ReattachAnchorable(anchorable); (GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.HideCommand?.Execute(null); }));
        menu.Items.Add(modes);
        return menu;
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
        if (anchorable == null)
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
