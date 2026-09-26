using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AvalonDock.Controls;

// WPFUI's independent hover and press triggers require independent native
// visual-state groups; ButtonBase moves only its exclusive CommonStates.
internal sealed class ChromeButton : Button
{
    public ChromeButton()
    {
        RegisterPropertyChangedCallback(IsPointerOverProperty, (_, _) => UpdateHover());
        RegisterPropertyChangedCallback(IsPressedProperty, (_, _) => UpdateHover());
        PointerEntered += (_, _) => UpdateHover();
        PointerExited += (_, _) => UpdateHover();
        AddHandler(PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => UpdateHover()), true);
        AddHandler(PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => UpdateHover()), true);
        PointerCaptureLost += (_, _) => UpdateHover();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateHover();
    }

    private void UpdateHover()
    {
        if (!DispatcherQueue.TryEnqueue(() =>
            VisualStateManager.GoToState(this, IsPointerOver && IsEnabled ? "HoverForeground" : "NotHovered", false)))
        {
            VisualStateManager.GoToState(this, IsPointerOver && IsEnabled ? "HoverForeground" : "NotHovered", false);
        }
    }
}
