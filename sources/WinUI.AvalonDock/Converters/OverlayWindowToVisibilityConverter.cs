// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Converters/OverlayWindowToVisibilityConverter.cs
using System.Globalization;
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Converters;

/// <summary>Hides the large docking targets when the overlay belongs to a floating window.</summary>
public class OverlayWindowToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isHostedInFloatingWindow = value is OverlayWindow { IsHostedInFloatingWindow: true };
        bool isLarge = parameter != null && bool.TryParse(parameter.ToString(), out bool parsed) && parsed;
        // WinUI has no Visibility.Hidden. Target grids use fixed layout extents, so collapsing
        // an unavailable glyph preserves the surrounding target geometry.
        return isHostedInFloatingWindow && isLarge ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    public virtual object ProvideValue(IServiceProvider? serviceProvider) => ConverterCreater.Get<OverlayWindowToVisibilityConverter>();
    protected override object ProvideValue() => ProvideValue(null);
    object IValueConverter.Convert(object value, Type targetType, object? parameter, string language) => Convert(value, targetType, parameter, CultureInfo.InvariantCulture);
    object IValueConverter.ConvertBack(object value, Type targetType, object? parameter, string language) => ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture);
}
