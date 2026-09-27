using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace WinUI.AvalonDock.Experiments.Shared;

public static class SampleStyles
{
    public static StyleSelector CreateItemStyleSelector()
    {
        return new SampleItemStyleSelector
        {
            DocumentStyle = CreateItemStyle(typeof(LayoutDocumentItem)),
            ToolStyle = CreateItemStyle(typeof(LayoutAnchorableItem))
        };
    }

    private static Style CreateItemStyle(Type targetType)
    {
        Style style = new(targetType);
        style.Setters.Add(new Setter(LayoutItem.TitleProperty,
            new Binding { Path = new PropertyPath("Model.Title"), Mode = BindingMode.TwoWay }));
        style.Setters.Add(new Setter(LayoutItem.ContentIdProperty,
            new Binding { Path = new PropertyPath("Model.Id") }));
        return style;
    }
}

public sealed class SampleItemStyleSelector : StyleSelector
{
    public required Style DocumentStyle
    {
        get; init;
    }

    public required Style ToolStyle
    {
        get; init;
    }

    protected override Style SelectStyleCore(object item, DependencyObject container)
        => container is LayoutAnchorableItem ? ToolStyle : DocumentStyle;
}
