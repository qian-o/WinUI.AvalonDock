// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/ThemeManager.cs
using AvalonDock.Core;
using AvalonDock.Themes;

namespace AvalonDock;

/// <summary>Registers themes and applies them to an attached docking manager.</summary>
public class ThemeManager : IThemeManager
{
    private readonly List<Theme> themes = new();
    private DockingManager? dockingManager;
    private string? currentThemeName;

    /// <summary>Initializes the theme manager with an optional set of themes.</summary>
    /// <param name="themes">The initially available themes.</param>
    public ThemeManager(IEnumerable<Theme>? themes = null)
    {
        if (themes != null)
        {
            foreach (Theme theme in themes)
            {
                this.themes.Add(theme);
            }
        }
    }

    /// <summary>Gets the most recently applied theme name, or null before one is applied.</summary>
    public string? CurrentThemeName => currentThemeName;

    /// <summary>Gets a snapshot of the available themes.</summary>
    public IReadOnlyList<IThemeInfo> AvailableThemes => themes.Cast<IThemeInfo>().ToList().AsReadOnly();

    /// <summary>Occurs after a registered theme is applied.</summary>
    public event EventHandler? ThemeChanged;

    /// <summary>Associates this service with the docking manager that receives theme changes.</summary>
    /// <param name="dockingManager">The docking manager.</param>
    public void Attach(DockingManager dockingManager)
    {
        this.dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
    }

    /// <summary>Registers a theme unless its concrete type is already registered.</summary>
    /// <param name="theme">The theme to register.</param>
    public void RegisterTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!themes.Any(candidate => candidate.GetType() == theme.GetType()))
        {
            themes.Add(theme);
        }
    }

    /// <summary>Applies a registered theme by its case-insensitive name.</summary>
    /// <param name="themeName">The registered theme name.</param>
    /// <returns>True when the name identifies a registered theme; otherwise, false.</returns>
    public bool ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return false;
        }

        Theme? theme = themes.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, themeName, StringComparison.OrdinalIgnoreCase));
        if (theme == null)
        {
            return false;
        }

        if (dockingManager != null)
        {
            dockingManager.Theme = theme;
        }

        currentThemeName = theme.Name;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
