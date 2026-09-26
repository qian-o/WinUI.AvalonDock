// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Converters/AnchorSideToOrientationConverter.cs.
using System.Globalization;
using AvalonDock.Layout;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Converters;

public class AnchorSideToOrientationConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) => (AnchorSide)value is AnchorSide.Left or AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException("This converter supports one-way bindings only.");
    public virtual object ProvideValue(IServiceProvider? serviceProvider) => ConverterCreater.Get<AnchorSideToOrientationConverter>();
    protected override object ProvideValue() => ProvideValue(null);
    object IValueConverter.Convert(object value, Type targetType, object? parameter, string language) => Convert(value, targetType, parameter, CultureInfo.InvariantCulture);
    object IValueConverter.ConvertBack(object value, Type targetType, object? parameter, string language) => ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture);
}
