using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Platforms;

internal interface IWindowHostService
{
    IDockingWindowHost Attach(Window window, UIElement? content, string title, Rect bounds, bool owned,
        WindowPlacement placement = WindowPlacement.ConstrainToWorkArea);
}

internal enum WindowPlacement
{
    ConstrainToWorkArea, RestoreOrCenterScreen, PreserveBounds
}

internal interface IDockingWindowHost : IDisposable
{
    event EventHandler<CancelEventArgs>? Closing;
    event EventHandler? Closed;
    /// <summary>Reports destruction of the independent window's originating host.</summary>
    event EventHandler? OwnerClosed;
    event EventHandler<WindowGeometry>? GeometryChanged;
    /// <summary>Reports an operating-system window move loop without exposing its native messages.</summary>
    event EventHandler<WindowMoveUpdate>? MoveChanged;
    /// <summary>Reports the start of an actual user resize without exposing native messages.</summary>
    event EventHandler? UserResizeStarted;
    /// <summary>报告平台窗口移动或调整大小操作已经结束。</summary>
    event EventHandler? InteractionCompleted;
    /// <summary>Requests the library caption menu at a point in the platform's declared coordinate space.</summary>
    event EventHandler<WindowCaptionContextRequest>? CaptionContextRequested;
    bool IsClosed
    {
        get;
    }
    bool IsVisible
    {
        get;
    }
    WindowGeometry Geometry
    {
        get;
    }
    void Show();
    void Hide();
    void SetMaximized(bool maximized);
    void BeginMove();
    /// <summary>Positions the window relative to a pointer reported in the platform's declared coordinate space.</summary>
    void MoveWithPointer(DragInputPosition pointer, Point anchorOffset);
    /// <summary>Returns the pointer's logical offset from the current outer window bounds.</summary>
    Point GetPointerAnchor(DragInputPosition pointer);
    void Close();
    void RequestClose();
    void SetOptions(bool owned, bool allowMinimize, Size contentMinimum, Thickness resizeBorder);
    void SetBounds(Rect bounds);
    Thickness GetFrameThickness();
    WindowCaptionMetrics GetCaptionMetrics();
    /// <summary>Sets caption/interactive rectangles in logical client coordinates, or restores the system caption.</summary>
    void SetCaptionLayout(WindowCaptionLayout? layout);
}

internal sealed record WindowCaptionLayout(Rect DragRegion, IReadOnlyList<Rect> InteractiveRegions, bool IsDark, bool HasClientButtons = false);
internal readonly record struct WindowCaptionMetrics(double Height, double LeftInset, double RightInset);
internal sealed class WindowCaptionContextRequest(DragInputPosition position) : EventArgs
{
    internal DragInputPosition Position { get; } = position;
    internal bool Handled
    {
        get; set;
    }
    internal bool ShowSystemMenu
    {
        get; set;
    }
}

/// <summary>
/// Floating bounds in the host's current 96-DPI logical coordinate space. Bounds retains the
/// last normal rectangle while ActualSize reports the current outer window size for the
/// original floating control's SizeChanged model notification.
/// </summary>
internal sealed record WindowGeometry(Rect Bounds, Size ActualSize, bool IsMaximized, bool IsMinimized = false);

internal enum WindowMovePhase
{
    Started, Moved, Released, Cancelled
}

internal sealed record WindowMoveUpdate(DragInputPosition Position, WindowMovePhase Phase, long Sequence);
