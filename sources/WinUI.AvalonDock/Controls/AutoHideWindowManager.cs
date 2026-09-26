// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/AutoHideWindowManager.cs
using System;
using AvalonDock.Layout;
using Microsoft.UI.Dispatching;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the auto Hide Window Manager.
/// </summary>
internal class AutoHideWindowManager
{
    private DockingManager manager;
    private WeakReference? currentAutohiddenAnchor;
    private readonly DispatcherQueueTimer closeTimer;
    private bool hidingWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoHideWindowManager"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    internal AutoHideWindowManager(DockingManager manager)
    {
        this.manager = manager;
        closeTimer = manager.DispatcherQueue.CreateTimer();
        SetupCloseTimer();
    }

    /// <summary>
    /// Executes the show Auto Hide Window operation.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    public void ShowAutoHideWindow(LayoutAnchorControl anchor)
    {
        if (hidingWindow || manager.AutoHideWindow is not { } autoHideWindow)
        {
            return;
        }

        if ((currentAutohiddenAnchor?.Target as LayoutAnchorControl) != anchor)
        {
            StopCloseTimer();
            currentAutohiddenAnchor = new WeakReference(anchor);
            autoHideWindow.Show(anchor);
            StartCloseTimer();
        }
    }

    /// <summary>
    /// Executes the hide Auto Window operation.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    public void HideAutoWindow(LayoutAnchorControl? anchor = null)
    {
        if (hidingWindow)
        {
            return;
        }

        if (anchor == null ||
            anchor == (currentAutohiddenAnchor?.Target as LayoutAnchorControl))
        {
            StopCloseTimer();
        }
        else
        {
            System.Diagnostics.Debug.Assert(false);
        }
    }

    private void SetupCloseTimer()
    {
        UpdateCloseDelay(manager.AutoHideDelay);
        closeTimer.Tick += (s, e) =>
        {
            if (manager.AutoHideWindow is { } window && (window.IsPointerWithin ||
                window.Model is LayoutAnchorable { IsActive: true } || window.IsResizing))
            {
                return;
            }

            StopCloseTimer();
        };
    }

    internal void UpdateCloseDelay(int delay) => closeTimer.Interval = TimeSpan.FromMilliseconds(delay);

    private void StartCloseTimer()
    {
        closeTimer.Start();
    }

    private void StopCloseTimer()
    {
        // Closing a native child root can synchronously unload its anchor and reenter Hide.
        if (hidingWindow)
        {
            return;
        }

        hidingWindow = true;
        try
        {
            closeTimer.Stop();
            manager.AutoHideWindow?.Hide();
            currentAutohiddenAnchor = null;
        }
        finally { hidingWindow = false; }
    }
}
