namespace AvalonDock.Controls;

/// <summary>Windows-only native message extension points for floating windows.</summary>
public abstract partial class LayoutFloatingWindowControl
{
    protected virtual nint FilterMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) => 0;

    internal nint ProcessNativeMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) =>
        FilterMessage(hwnd, msg, wParam, lParam, ref handled);
}
