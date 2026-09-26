// Adapts the null-icon DataTemplate trigger in WPFUI Styles/Common.xaml (MIT, qian-o).
// WinUI DataTemplate does not provide WPF DataTemplate.Triggers.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Themes;

internal sealed class HeaderIconMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value == null ? new Thickness(0) : new Thickness(4, 0, 0, 0);

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
