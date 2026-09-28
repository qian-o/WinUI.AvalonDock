namespace AvalonDock.Platforms;

/// <summary>
/// Starts a host-scoped input session on the host's UI thread. Implementations own input capture
/// and report input only; callers own docking candidates, layout changes, and commit decisions.
/// </summary>
internal interface IDragInputService
{
    /// <summary>
    /// Returns null when input capture cannot start. The initial position is available on the
    /// returned session. The optional press position is in the originating element's local XAML
    /// coordinates, so a later cursor update cannot replace the actual press location. Callbacks
    /// begin after this method returns. Dispose the session when the operation ends. Callback
    /// failures cancel capture and are retained by the session.
    /// </summary>
    IDragInputSession? TryBegin(Action<DragInputUpdate> onUpdate, global::Windows.Foundation.Point? pressPosition = null);
}

internal interface IDragInputSession : IDisposable
{
    DragInputPosition Position
    {
        get;
    }
    bool IsCompleted
    {
        get;
    }
    Exception? Failure
    {
        get;
    }
    void Cancel();
}

/// <summary>
/// Coordinates must be interpreted using their declared space. DesktopPhysicalPixels describes
/// a platform capability, not an assumption that every future backend has a global desktop.
/// Values may be negative on monitors to the left of or above the primary monitor.
/// </summary>
internal readonly record struct DragInputPosition(double X, double Y, DragCoordinateSpace Space);

internal enum DragCoordinateSpace
{
    DesktopPhysicalPixels
}

internal enum DragInputUpdateKind
{
    Moved,
    Released,
    Cancelled
}

internal enum DragInputEndReason
{
    None,
    Escape,
    CaptureLost,
    Interrupted,
    HostClosed,
    Disposed,
    InputUnavailable,
    TimedOut
}

internal readonly record struct DragInputUpdate(
    DragInputPosition Position,
    DragInputUpdateKind Kind,
    DragInputEndReason Reason = DragInputEndReason.None,
    Exception? Failure = null);
