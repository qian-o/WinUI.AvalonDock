using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>Supplies native front-to-back window order without exposing platform handles.</summary>
internal interface IWindowOrderService
{
    IReadOnlyList<WindowOrderEntry> GetWindowsByZOrder(FrameworkElement owner, IReadOnlyList<Window> windows);
    void BringToFront(Window window);
    void BringToFront(FrameworkElement element);
}

internal readonly record struct WindowOrderEntry(Window Window, bool AboveOwner);
