// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager window policy properties.
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace AvalonDock;

public partial class DockingManager
{
    public static readonly DependencyProperty ShowSystemMenuProperty = DependencyProperty.Register(
        nameof(ShowSystemMenu), typeof(bool), typeof(DockingManager), new PropertyMetadata(true));

    [Bindable(true), Description("Gets or sets a value indicating whether floating windows should show the system menu when a custom context menu is not defined."), Category("FloatingWindow")]
    public bool ShowSystemMenu
    {
        get => (bool)GetValue(ShowSystemMenuProperty);
        set => SetValue(ShowSystemMenuProperty, value);
    }

    public static readonly DependencyProperty AutoWindowSizeWhenOpenedProperty = DependencyProperty.Register(
        nameof(AutoWindowSizeWhenOpened), typeof(bool), typeof(DockingManager), new PropertyMetadata(false));

    [Bindable(true), Description("Gets or sets a value indicating whether the floating window is auto sized when it is dragged out."), Category("FloatingWindow")]
    public bool AutoWindowSizeWhenOpened
    {
        get => (bool)GetValue(AutoWindowSizeWhenOpenedProperty);
        set => SetValue(AutoWindowSizeWhenOpenedProperty, value);
    }
}
