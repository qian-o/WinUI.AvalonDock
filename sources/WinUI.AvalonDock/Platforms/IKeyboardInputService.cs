using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace AvalonDock.Platforms;

/// <summary>Provides platform-specific keyboard state and key normalization.</summary>
internal interface IKeyboardInputService
{
    bool IsKeyDown(VirtualKey key);
    VirtualKeyModifiers GetModifiers();
    VirtualKey NormalizeKey(VirtualKey key, VirtualKeyModifiers modifiers, CorePhysicalKeyStatus status);
}
