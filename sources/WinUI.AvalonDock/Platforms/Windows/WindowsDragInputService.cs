using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace AvalonDock.Platforms.Windows;

/// <summary>
/// Tracks a mouse docking operation through Win32 capture. WinUI supplies only the originating
/// host and dispatcher; this service does not use data drag-and-drop or tab tear-out APIs.
/// </summary>
internal sealed class WindowsDragInputService(FrameworkElement origin) : IDragInputService
{
    public IDragInputSession? TryBegin(Action<DragInputUpdate> onUpdate, Point? pressPosition = null)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        if (!origin.DispatcherQueue.HasThreadAccess)
        {
            throw new InvalidOperationException("Drag input must start on the originating host's UI thread.");
        }

        XamlRoot? xamlRoot = origin.XamlRoot;
        if (xamlRoot is null)
        {
            return null;
        }

        nint window = Win32Interop.GetWindowFromWindowId(xamlRoot.ContentIslandEnvironment.AppWindowId);
        if (window == 0 || !NativeMethods.IsWindow(window) || !NativeMethods.IsLeftButtonDown())
        {
            return null;
        }

        // WinUI's pointer capture must be relinquished before the native top-level host owns it.
        origin.ReleasePointerCaptures();
        try
        {
            DragInputPosition? initialPosition = null;
            if (pressPosition is Point localPoint)
            {
                Point rootPoint = origin.TransformToVisual(null).TransformPoint(localPoint);
                PointInt32 screenPoint = xamlRoot.CoordinateConverter.ConvertLocalToScreen(rootPoint);
                initialPosition = new DragInputPosition(screenPoint.X, screenPoint.Y, DragCoordinateSpace.DesktopPhysicalPixels);
            }
            return Session.TryCreate(window, origin.DispatcherQueue, onUpdate, initialPosition);
        }
        catch (EntryPointNotFoundException exception)
        {
            // The desktop host must activate Common Controls v6 for the supported subclass APIs.
            Debug.WriteLine($"Native docking input is unavailable: {exception.Message}");
            return null;
        }
    }

    private sealed class Session : IDragInputSession
    {
        private const uint MouseMove = 0x0200;
        private const uint LeftButtonUp = 0x0202;
        private const uint CaptureChanged = 0x0215;
        private const uint CancelMode = 0x001F;
        private const uint KeyDown = 0x0100;
        private const uint NonClientDestroy = 0x0082;
        private const uint EscapeKey = 0x1B;
        private const nuint SubclassId = 0x57414449;
        private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(5);
        private static readonly NativeMethods.SubclassProcedure WindowProcedure = OnWindowMessage;

        private readonly nint window;
        private readonly DispatcherQueue dispatcher;
        private readonly DispatcherQueueTimer timer;
        private readonly Action<DragInputUpdate> onUpdate;
        private readonly long startedAt = Stopwatch.GetTimestamp();
        private GCHandle selfHandle;
        private bool subclassInstalled;
        private bool captureOwned;
        private bool callbacksEnabled;

        private Session(nint window, DispatcherQueue dispatcher, Action<DragInputUpdate> onUpdate, DragInputPosition position)
        {
            this.window = window;
            this.dispatcher = dispatcher;
            this.onUpdate = onUpdate;
            Position = position;
            timer = dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(16);
            timer.Tick += OnTimerTick;
        }

        public DragInputPosition Position
        {
            get; private set;
        }
        public bool IsCompleted
        {
            get; private set;
        }
        public Exception? Failure
        {
            get; private set;
        }

        public static Session? TryCreate(nint window, DispatcherQueue dispatcher, Action<DragInputUpdate> onUpdate,
            DragInputPosition? initialPosition)
        {
            if (NativeMethods.GetWindowSubclass(window, WindowProcedure, SubclassId, out _))
            {
                return null;
            }

            DragInputPosition position;
            if (initialPosition.HasValue)
            {
                position = initialPosition.Value;
            }
            else if (!TryGetPosition(out position))
            {
                return null;
            }

            Session session = new(window, dispatcher, onUpdate, position);
            session.selfHandle = GCHandle.Alloc(session);
            nuint reference = unchecked((nuint)GCHandle.ToIntPtr(session.selfHandle));
            if (!NativeMethods.SetWindowSubclass(window, WindowProcedure, SubclassId, reference))
            {
                session.Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.InputUnavailable);
                return null;
            }

            session.subclassInstalled = true;
            session.captureOwned = true;
            NativeMethods.SetCapture(window);
            if (session.IsCompleted || NativeMethods.GetCapture() != window)
            {
                session.Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.InputUnavailable);
                return null;
            }

            try
            {
                session.timer.Start();
            }
            catch (Exception exception)
            {
                session.Fail(exception);
                return null;
            }
            session.callbacksEnabled = true;
            return session;
        }

        public void Cancel()
        {
            VerifyThread();
            Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Interrupted);
        }

        public void Dispose()
        {
            VerifyThread();
            Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Disposed);
        }

        private void VerifyThread()
        {
            if (!dispatcher.HasThreadAccess)
            {
                throw new InvalidOperationException("Drag input must end on the originating host's UI thread.");
            }
        }

        private static nint OnWindowMessage(nint window, uint message, nuint wParam, nint lParam, nuint subclassId, nuint reference)
        {
            Session? session = null;
            try
            {
                session = GCHandle.FromIntPtr(unchecked((nint)reference)).Target as Session;
                session?.HandleMessage(message, wParam, lParam);
                if (session is { IsCompleted: true })
                {
                    session.ReleaseSubclass(message == NonClientDestroy);
                }
            }
            catch (Exception exception)
            {
                // No managed exception may escape through the unmanaged subclass callback.
                try
                {
                    session?.Fail(exception);
                }
                catch (Exception cleanupFailure)
                {
                    if (session is not null)
                    {
                        session.Failure = new AggregateException(exception, cleanupFailure);
                    }
                }
            }

            return NativeMethods.DefSubclassProc(window, message, wParam, lParam);
        }

        private void HandleMessage(uint message, nuint wParam, nint lParam)
        {
            if (IsCompleted)
            {
                return;
            }

            switch (message)
            {
                case MouseMove:
                    PollInput();
                    break;
                case LeftButtonUp:
                    CompleteRelease();
                    break;
                case KeyDown when wParam == EscapeKey:
                    Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Escape);
                    break;
                case CaptureChanged when lParam != window:
                    Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.CaptureLost);
                    break;
                case CancelMode:
                    Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Interrupted);
                    break;
                case NonClientDestroy:
                    Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.HostClosed);
                    break;
            }
        }

        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                PollInput();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void PollInput()
        {
            if (IsCompleted)
            {
                return;
            }

            if (!NativeMethods.IsWindow(window))
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.HostClosed);
            }
            else if (NativeMethods.GetCapture() != window)
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.CaptureLost);
            }
            else if ((NativeMethods.GetAsyncKeyState((int)EscapeKey) & 0x8000) != 0)
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Escape);
            }
            else if (Stopwatch.GetElapsedTime(startedAt) > MaximumDuration)
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.TimedOut);
            }
            else if (!NativeMethods.IsLeftButtonDown())
            {
                CompleteRelease();
            }
            else if (TryGetPosition(out DragInputPosition position))
            {
                if (position != Position)
                {
                    Position = position;
                    Notify(new DragInputUpdate(Position, DragInputUpdateKind.Moved));
                }
            }
            else
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.InputUnavailable);
            }
        }

        private void CompleteRelease()
        {
            if ((NativeMethods.GetAsyncKeyState((int)EscapeKey) & 0x8000) != 0)
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.Escape);
            }
            else if (TryGetPosition(out DragInputPosition position))
            {
                Position = position;
                Finish(DragInputUpdateKind.Released, DragInputEndReason.None);
            }
            else
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.InputUnavailable);
            }
        }

        private static bool TryGetPosition(out DragInputPosition position)
        {
            if (NativeMethods.GetCursorPos(out NativeMethods.NativePoint point))
            {
                position = new DragInputPosition(point.X, point.Y, DragCoordinateSpace.DesktopPhysicalPixels);
                return true;
            }

            position = default;
            return false;
        }

        private void Finish(DragInputUpdateKind kind, DragInputEndReason reason)
        {
            if (IsCompleted)
            {
                return;
            }

            // Mark completion first: ReleaseCapture may synchronously reenter the subclass.
            IsCompleted = true;
            try
            {
                timer.Stop();
                timer.Tick -= OnTimerTick;
            }
            catch (Exception exception)
            {
                Failure = Failure is null ? exception : new AggregateException(Failure, exception);
                kind = DragInputUpdateKind.Cancelled;
                reason = DragInputEndReason.InputUnavailable;
            }

            if (!ReleaseSubclass())
            {
                Failure = new InvalidOperationException("The docking input subclass could not be removed; its callback remains rooted until the host releases it.");
                kind = DragInputUpdateKind.Cancelled;
                reason = DragInputEndReason.InputUnavailable;
            }

            if (captureOwned && NativeMethods.GetCapture() == window)
            {
                if (!NativeMethods.ReleaseCapture())
                {
                    Failure = new InvalidOperationException("The docking input capture could not be released.");
                    kind = DragInputUpdateKind.Cancelled;
                    reason = DragInputEndReason.InputUnavailable;
                }
            }

            captureOwned = false;
            Notify(new DragInputUpdate(Position, kind, reason, Failure));
        }

        private bool ReleaseSubclass(bool windowDestroyed = false)
        {
            if (subclassInstalled &&
                (NativeMethods.RemoveWindowSubclass(window, WindowProcedure, SubclassId) || windowDestroyed || !NativeMethods.IsWindow(window)))
            {
                subclassInstalled = false;
            }

            // Never free callback state while native code can still refer to it. On a rare failed
            // removal, subsequent messages retry cleanup and WM_NCDESTROY releases the final root.
            if (!subclassInstalled && selfHandle.IsAllocated)
            {
                selfHandle.Free();
            }

            return !subclassInstalled;
        }

        private void Fail(Exception exception)
        {
            Failure = Failure is null ? exception : new AggregateException(Failure, exception);
            if (!IsCompleted)
            {
                Finish(DragInputUpdateKind.Cancelled, DragInputEndReason.InputUnavailable);
            }
        }

        private void Notify(DragInputUpdate update)
        {
            if (!callbacksEnabled)
            {
                return;
            }

            try
            {
                onUpdate(update);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate nint SubclassProcedure(nint window, uint message, nuint wParam, nint lParam, nuint subclassId, nuint reference);

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativePoint
        {
            internal int X;
            internal int Y;
        }

        internal static bool IsLeftButtonDown() => (GetAsyncKeyState(1) & 0x8000) != 0;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(nint window);

        [DllImport("user32.dll")]
        internal static extern nint SetCapture(nint window);

        [DllImport("user32.dll")]
        internal static extern nint GetCapture();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out NativePoint point);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowSubclass(nint window, SubclassProcedure procedure, nuint subclassId, nuint reference);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowSubclass(nint window, SubclassProcedure procedure, nuint subclassId, out nuint reference);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveWindowSubclass(nint window, SubclassProcedure procedure, nuint subclassId);

        [DllImport("comctl32.dll")]
        internal static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
    }
}
