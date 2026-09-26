using System.Runtime.CompilerServices;
using AvalonDock.Converters;
using AvalonDock.Platforms;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Controls;

/// <summary>Supplies the mapped context-menu root context without overriding item-local values.</summary>
internal static class MenuFlyoutContext
{
    private static readonly ConditionalWeakTable<MenuFlyout, State> States = new();
    private static readonly ConditionalWeakTable<MenuFlyout, ResourceScope> ResourceScopes = new();

    internal static void AttachResources(MenuFlyout menu, ResourceDictionary resources)
        => ResourceScopes.GetValue(menu, static value => new ResourceScope(value)).Source = resources;

    // A WinUI flyout has no Resources owner. Its presenter is created when the menu
    // opens, so install the manager's dictionary copy before item resources resolve.
    private sealed class ResourceScope
    {
        private static readonly DependencyProperty InstallProperty = DependencyProperty.RegisterAttached(
            "Install", typeof(InstallRequest), typeof(ResourceScope), new PropertyMetadata(null, OnInstall));
        private readonly MenuFlyout menu;
        private readonly ConditionalWeakTable<MenuFlyoutPresenter, ResourceDictionary> installed = new();
        private Style? originalStyle;
        private Style? temporaryStyle;

        internal ResourceDictionary? Source
        {
            get; set;
        }

        internal ResourceScope(MenuFlyout menu)
        {
            this.menu = menu;
            menu.Opening += OnOpening;
            menu.Opened += OnOpened;
            menu.Closed += OnClosed;
        }

        private void OnOpening(object? sender, object args)
        {
            if (Source == null)
            {
                return;
            }

            originalStyle = menu.MenuFlyoutPresenterStyle;
            temporaryStyle = new Style(typeof(MenuFlyoutPresenter)) { BasedOn = originalStyle };
            temporaryStyle.Setters.Add(new Setter(InstallProperty, new InstallRequest(this)));
            menu.MenuFlyoutPresenterStyle = temporaryStyle;
        }

        private void OnOpened(object? sender, object args)
            => RestoreStyle();

        private void OnClosed(object? sender, object args)
            => RestoreStyle();

        private void RestoreStyle()
        {
            if (temporaryStyle != null && ReferenceEquals(menu.MenuFlyoutPresenterStyle, temporaryStyle))
            {
                menu.MenuFlyoutPresenterStyle = originalStyle;
            }

            temporaryStyle = null;
            originalStyle = null;
        }

        private static void OnInstall(DependencyObject owner, DependencyPropertyChangedEventArgs args)
        {
            if (owner is MenuFlyoutPresenter presenter && args.NewValue is InstallRequest request && request.Scope.Source != null)
            {
                request.Scope.Install(presenter);
            }
        }

        private void Install(MenuFlyoutPresenter presenter)
        {
            if (Source is not { } resources)
            {
                return;
            }

            if (installed.TryGetValue(presenter, out ResourceDictionary? previous))
            {
                presenter.Resources.MergedDictionaries.Remove(previous);
                installed.Remove(presenter);
            }
            ResourceDictionary copy = ThemeResourceFactory.Copy(resources);
            presenter.Resources.MergedDictionaries.Add(copy);
            installed.Add(presenter, copy);
        }

        private sealed class InstallRequest(ResourceScope scope)
        {
            internal ResourceScope Scope { get; } = scope;
        }
    }

    internal static void Show(MenuFlyout menu, FrameworkElement target, object? context, FlyoutShowOptions options)
    {
        FrameworkElement source = target;
        XamlRoot stableRoot = States.TryGetValue(menu, out State? previousState) ? previousState.Anchor?.XamlRoot ?? menu.XamlRoot : menu.XamlRoot;
        stableRoot ??= (context as LayoutItem)?.LayoutElement?.Root?.Manager?.XamlRoot ?? target.XamlRoot;
        if (stableRoot != null && !ReferenceEquals(stableRoot, target.XamlRoot) && stableRoot.Content is FrameworkElement anchor && anchor.IsLoaded)
        {
            // WinUI FlyoutBase fixes XamlRoot after first use. Keep the same menu and
            // command instances, placing its native popup at the secondary-root point.
            Point point = options.Position ?? new Point(0, target.ActualHeight);
            PointInt32 screen = target.XamlRoot.CoordinateConverter.ConvertLocalToScreen(target.TransformToVisual(null).TransformPoint(point));
            Point local = PlatformServices.Coordinates.ToElementLocal(anchor, new Point(screen.X, screen.Y));
            menu.ShouldConstrainToRootBounds = false;
            options = new FlyoutShowOptions { Position = local, Placement = options.Placement, ShowMode = options.ShowMode };
            target = anchor;
        }
        SetContext(menu, context);
        States.GetValue(menu, static value => new State(value)).ObserveTarget(source);
        if (menu.XamlRoot == null)
        {
            menu.XamlRoot = target.XamlRoot;
        }

        menu.ShowAt(target, options);
        States.GetValue(menu, static value => new State(value)).Anchor = target;
    }
    internal static void SetContext(MenuFlyout menu, object? context)
    {
        State state = States.GetValue(menu, static value => new State(value));
        state.Context = context;
        state.Apply();
    }
    internal static object? GetContext(MenuFlyout menu) => States.TryGetValue(menu, out State? state) ? state.Context : null;
    private sealed class State
    {
        private static readonly AnchorableContextMenuHideVisibilityConverter HideVisibility = new();
        private static readonly BoolToVisibilityConverter EnabledVisibility = new();
        private readonly MenuFlyout menu;
        private readonly Dictionary<FrameworkElement, object?> assigned = [];
        private readonly Dictionary<MenuFlyoutItem, long> policyTokens = [];
        private LayoutItem? policyContext;
        private long closableToken;
        private FrameworkElement? source;
        internal object? Context;
        internal FrameworkElement? Anchor;
        internal State(MenuFlyout menu)
        {
            this.menu = menu;
            menu.Opening += (_, _) => Apply();
            menu.Closed += (_, _) => Clear();
        }
        internal void Apply()
        {
            ClearPolicy();
            policyContext = Context as LayoutItem;
            foreach (MenuFlyoutItemBase entry in Descendants(menu.Items))
            {
                object local = entry.ReadLocalValue(FrameworkElement.DataContextProperty);
                if (local == DependencyProperty.UnsetValue || assigned.TryGetValue(entry, out object? previous) && ReferenceEquals(local, previous))
                {
                    entry.DataContext = Context;
                    assigned[entry] = Context;
                }
                if (entry is MenuFlyoutItem item && item.Tag is string tag && tag.StartsWith("AvalonDock.Menu.", StringComparison.Ordinal))
                {
                    policyTokens[item] = item.RegisterPropertyChangedCallback(Control.IsEnabledProperty, (_, _) => RefreshPolicy());
                }
            }
            if (policyContext != null && policyTokens.Keys.Any(item => Equals(item.Tag, "AvalonDock.Menu.Hide")))
            {
                closableToken = policyContext.RegisterPropertyChangedCallback(LayoutItem.CanCloseProperty, (_, _) => RefreshPolicy());
            }

            RefreshPolicy();
        }
        internal void ObserveTarget(FrameworkElement target)
        {
            if (source != null)
            {
                source.Unloaded -= OnTargetUnloaded;
            }

            source = target;
            source.Unloaded += OnTargetUnloaded;
        }
        private void OnTargetUnloaded(object? sender, RoutedEventArgs args) => menu.Hide();
        private void RefreshPolicy()
        {
            foreach (MenuFlyoutItem item in policyTokens.Keys)
            {
                object enabled = EnabledVisibility.Convert(item.IsEnabled, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture);
                item.Visibility = Equals(item.Tag, "AvalonDock.Menu.Hide")
                    ? (Visibility)HideVisibility.Convert([enabled, policyContext is null ? DependencyProperty.UnsetValue : policyContext.CanClose],
                        typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
                    : (Visibility)enabled;
            }
        }
        private void ClearPolicy()
        {
            foreach (KeyValuePair<MenuFlyoutItem, long> pair in policyTokens)
            {
                pair.Key.UnregisterPropertyChangedCallback(Control.IsEnabledProperty, pair.Value);
            }

            policyTokens.Clear();
            if (policyContext != null && closableToken != 0)
            {
                policyContext.UnregisterPropertyChangedCallback(LayoutItem.CanCloseProperty, closableToken);
            }

            policyContext = null;
            closableToken = 0;
        }
        private void Clear()
        {
            if (source != null)
            {
                source.Unloaded -= OnTargetUnloaded;
            }

            source = null;
            ClearPolicy();
            foreach (KeyValuePair<FrameworkElement, object?> pair in assigned)
            {
                if (ReferenceEquals(pair.Key.ReadLocalValue(FrameworkElement.DataContextProperty), pair.Value))
                {
                    pair.Key.ClearValue(FrameworkElement.DataContextProperty);
                }
            }

            assigned.Clear();
            Context = null;
        }
        private static IEnumerable<MenuFlyoutItemBase> Descendants(IList<MenuFlyoutItemBase> items)
        {
            foreach (MenuFlyoutItemBase item in items)
            {
                yield return item;
                if (item is MenuFlyoutSubItem sub)
                {
                    foreach (MenuFlyoutItemBase child in Descendants(sub.Items))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
