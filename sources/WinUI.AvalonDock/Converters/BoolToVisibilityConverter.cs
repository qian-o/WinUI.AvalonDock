// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Converters/BoolToVisibilityConverter.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Converters;

/// <summary>
/// Represents the bool To Visibility Converter.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        switch (value)
        {
            case bool val when targetType == typeof(Visibility):
                if (val)
                {
                    return Visibility.Visible;
                }

                return parameter is Visibility ? parameter : Visibility.Collapsed;

            case null when parameter is Visibility:
                return parameter;

            case null:
                return Visibility.Collapsed;

            default:
                return Visibility.Visible;
                // throw new ArgumentException("Invalid argument/return type. Expected argument: bool and return type: Visibility");
        }
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (!(value is Visibility))
        {
            throw new ArgumentException("Invalid argument type. Expected argument: Visibility.", nameof(value));
        }

        if (targetType != typeof(bool))
        {
            throw new ArgumentException("Invalid return type. Expected type: bool", nameof(targetType));
        }

        return (Visibility)value == Visibility.Visible;
    }

    /// <inheritdoc/>
    public virtual object ProvideValue(IServiceProvider? serviceProvider)
    {
        return ConverterCreater.Get<BoolToVisibilityConverter>();
    }
    // WinUI's binding interface uses language tags and a parameterless markup hook.
    protected override object ProvideValue() => ProvideValue(null);
    object IValueConverter.Convert(object value, Type targetType, object? parameter, string language) =>
        Convert(value, targetType, parameter, System.Globalization.CultureInfo.InvariantCulture);
    object IValueConverter.ConvertBack(object value, Type targetType, object? parameter, string language) =>
        ConvertBack(value, targetType, parameter, System.Globalization.CultureInfo.InvariantCulture);

}
