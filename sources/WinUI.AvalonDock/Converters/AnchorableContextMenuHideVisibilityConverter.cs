// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Converters/AnchorableContextMenuHideVisibilityConverter.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Converters;

/// <summary>Preserves the original close-versus-hide menu visibility rule.</summary>
public class AnchorableContextMenuHideVisibilityConverter : MarkupExtension
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if ((values.Count() == 2)
          && (values[0] != DependencyProperty.UnsetValue)
          && (values[1] != DependencyProperty.UnsetValue)
          && (values[1] is bool boolean))
        {
            bool canClose = boolean;

            return canClose ? Visibility.Collapsed : values[0];
        }
        else
        {
            return values[0];
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("This converter supports one-way bindings only.");
    }

    // WinUI has no IMultiValueConverter or IServiceProvider markup override.
    public virtual object ProvideValue(IServiceProvider? serviceProvider)
    {
        return ConverterCreater.Get<AnchorableContextMenuHideVisibilityConverter>();
    }

    protected override object ProvideValue() => ProvideValue(null);
}
