// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Converters/NullToDoNothingConverter.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Converters;

public class NullToDoNothingConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return DependencyProperty.UnsetValue;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException("This converter supports one-way bindings only.");
    public virtual object ProvideValue(IServiceProvider? serviceProvider) => ConverterCreater.Get<NullToDoNothingConverter>();
    protected override object ProvideValue() => ProvideValue(null);
    object IValueConverter.Convert(object value, Type targetType, object parameter, string language) => Convert(value, targetType, parameter, CultureInfo.InvariantCulture);
    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language) => ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture);
}
