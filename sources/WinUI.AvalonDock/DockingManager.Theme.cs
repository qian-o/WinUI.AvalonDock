// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using AvalonDock.Controls;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;

namespace AvalonDock;

public partial class DockingManager
{
    private ResourceDictionary? themeResources;

    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(Theme), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnThemeChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the Theme to be used for the controls in this framework.")]
    [System.ComponentModel.Category("Other")]
    public Theme? Theme
    {
        get => (Theme?)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    protected virtual void OnThemeChanged(DependencyPropertyChangedEventArgs e)
    {
        Theme? oldTheme = e.OldValue as Theme;
        Theme? newTheme = e.NewValue as Theme;
        ResourceDictionary resources = Resources;

        // WinUI loads Source immediately. Prepare it before altering the old theme so
        // an invalid URI cannot remove a still-working native resource dictionary.
        ResourceDictionary? candidate = newTheme is DictionaryTheme dictionary ? dictionary.ThemeResourceDictionary
            : newTheme?.GetResourceUri() is { } uri ? new ResourceDictionary { Source = uri } : null;

        // Detached windows have separate XamlRoots and do not inherit manager resources.
        foreach (DetachedEntry entry in detachedEntries.Values)
        {
            entry.Window.UpdateThemeResources(oldTheme, newTheme);
        }

        if (oldTheme != null)
        {
            if (oldTheme is DictionaryTheme)
            {
                if (themeResources != null)
                {
                    resources.MergedDictionaries.Remove(themeResources);
                    themeResources = null;
                }
            }
            else
            {
                ResourceDictionary? resourceDictionaryToRemove = resources.MergedDictionaries.FirstOrDefault(r => r.Source == oldTheme.GetResourceUri());
                if (resourceDictionaryToRemove != null)
                {
                    resources.MergedDictionaries.Remove(resourceDictionaryToRemove);
                }
            }
        }

        if (candidate != null)
        {
            if (newTheme is DictionaryTheme)
            {
                themeResources = candidate;
                resources.MergedDictionaries.Add(themeResources);
            }
            else
            {
                resources.MergedDictionaries.Add(candidate);
            }
        }

        // Floating controls own separate template/resource roots. The pinned manager updates
        // every registered floating control after changing its own resource dictionary; keep
        // that source responsibility instead of relying on an eventual template layout pass.
        foreach (LayoutFloatingWindowControl floating in floatingControls.ToArray())
        {
            floating.UpdateThemeResources(oldTheme);
        }

        RequestViewRefresh();
        // Native floating content observes ThemeProperty itself; the overlay and
        // navigator are separate windows and need their own resource update.
        dockingOverlay.UpdateThemeResources(newTheme);
        navigatorWindow?.UpdateThemeResources();
    }
}
