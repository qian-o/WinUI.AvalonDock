// Native target adapter for the original NullToDoNothingConverter in WPFUI Styles/Common.xaml.
// WinUI's converter UnsetValue invokes fallback instead of preserving Image.Source.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Themes;

internal static class RetainedIconSource
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(ImageSource), typeof(RetainedIconSource),
        new PropertyMetadata(null, (sender, args) =>
        {
            if (sender is Image image && args.NewValue is ImageSource source)
            {
                image.Source = source;
            }
        }));

    public static ImageSource? GetSource(DependencyObject target) => (ImageSource?)target.GetValue(SourceProperty);
    public static void SetSource(DependencyObject target, ImageSource? value) => target.SetValue(SourceProperty, value);
}
