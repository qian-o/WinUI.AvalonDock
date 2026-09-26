using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>Distinguishes native keyboard focus from a XAML root's remembered element.</summary>
internal interface IFocusService
{
    bool HasKeyboardFocus(UIElement element);
    bool Focus(UIElement element, FocusState state);
    IDisposable ObserveNativeFocus(Action<UIElement, INativeFocusTarget> focusChanged);
}

/// <summary>A remembered native child focus target with platform-owned lifetime validation.</summary>
internal interface INativeFocusTarget
{
    bool Focus();
}
