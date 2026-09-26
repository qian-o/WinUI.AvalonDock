using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsKeyboardInputService : IKeyboardInputService
{
    public bool IsKeyDown(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    public VirtualKeyModifiers GetModifiers()
    {
        VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;
        if (IsKeyDown(VirtualKey.Control))
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if (IsKeyDown(VirtualKey.Shift))
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if (IsKeyDown(VirtualKey.Menu))
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows))
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }

        return modifiers;
    }

    public VirtualKey NormalizeKey(VirtualKey key, VirtualKeyModifiers modifiers, CorePhysicalKeyStatus status) => key switch
    {
        VirtualKey.Shift => status.ScanCode == 0x36 ? VirtualKey.RightShift : VirtualKey.LeftShift,
        VirtualKey.Control => status.IsExtendedKey ? VirtualKey.RightControl : VirtualKey.LeftControl,
        VirtualKey.Menu => status.IsExtendedKey ? VirtualKey.RightMenu : VirtualKey.LeftMenu,
        _ => key,
    };
}
