// Ported from qian-o/AvalonDock.Themes.WPFUI e8a3da4 (MIT, qian-o).
// Original: Converters/ModelToMarginConverter.cs.
// License: https://github.com/qian-o/AvalonDock.Themes.WPFUI/blob/e8a3da4e9761ff549e5a2afca2bdf891a681baf2/LICENSE
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Themes;

internal sealed class ModelToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is LayoutAnchorable layoutAnchorable
            && layoutAnchorable.FindParent<LayoutAnchorSide>() is LayoutAnchorSide layoutAnchorSide
            && layoutAnchorable.Root?.Manager is DockingManager manager)
        {
            switch (layoutAnchorSide.Side)
            {
                case AnchorSide.Left:
                    return new Thickness(0, 0, -manager.GridSplitterWidth, 0);
                case AnchorSide.Top:
                    return new Thickness(0, 0, 0, -manager.GridSplitterHeight);
                case AnchorSide.Right:
                    return new Thickness(-manager.GridSplitterWidth, 0, 0, 0);
                case AnchorSide.Bottom:
                    return new Thickness(0, -manager.GridSplitterHeight, 0, 0);
            }
        }

        return new Thickness();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException("This converter supports one-way bindings only.");
}
