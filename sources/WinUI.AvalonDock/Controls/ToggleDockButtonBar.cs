// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ToggleDockButtonBar.cs.
using System.Globalization;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Controls;

public sealed class NullToFalseConverter : IValueConverter
{
    public static readonly NullToFalseConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    object IValueConverter.Convert(object value, Type targetType, object parameter, string language) => Convert(value, targetType, parameter, CultureInfo.InvariantCulture);
    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language) => ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture);
}

public class ToggleDockButtonBar : ItemsControl
{
    public ToggleDockButtonBar()
    {
        DefaultStyleKey = typeof(ToggleDockButtonBar);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/Toggle.xaml");
        UpdatePanel();
        RegisterPropertyChangedCallback(OrientationProperty, (_, _) => UpdatePanel());
    }
    public static readonly DependencyProperty ZoneProperty = DependencyProperty.Register(nameof(Zone), typeof(DockZone), typeof(ToggleDockButtonBar), new PropertyMetadata(DockZone.LeftTop));
    public DockZone Zone
    {
        get => (DockZone)GetValue(ZoneProperty); set => SetValue(ZoneProperty, value);
    }
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ToggleDockButtonBar), new PropertyMetadata(Orientation.Vertical));
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value);
    }
    public void SetAnchorables(IEnumerable<LayoutAnchorable> anchorables, DockZone zone)
    {
        foreach (ToggleDockButton button in Items.OfType<ToggleDockButton>())
        {
            button.Release();
        }

        Items.Clear();
        foreach (LayoutAnchorable tool in anchorables)
        {
            Items.Add(new ToggleDockButton { Anchorable = tool, Zone = zone });
        }
    }
    internal bool ContainsAnchorable(LayoutAnchorable tool) => Items.OfType<ToggleDockButton>().Any(button => ReferenceEquals(button.Anchorable, tool));
    private void UpdatePanel() => ItemsPanel = (ItemsPanelTemplate)XamlReader.Load($"<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='{Orientation}'/></ItemsPanelTemplate>");
}
