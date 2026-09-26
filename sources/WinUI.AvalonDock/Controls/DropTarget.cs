// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DropTarget.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the drop target.
/// </summary>
/// <typeparam name="T">The type of t.</typeparam>
internal abstract class DropTarget<T> : DropTargetBase, IDropTarget
    where T : FrameworkElement
{
    private Rect[] detectionRect;
    private T targetElement;
    private DropTargetType type;

    /// <summary>
    /// Initializes a new instance of the <see cref="DropTarget{T}"/> class.
    /// </summary>
    /// <param name="targetElement">The target element.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    protected DropTarget(T targetElement, Rect detectionRect, DropTargetType type)
    {
        this.targetElement = targetElement;
        this.detectionRect = new Rect[] { detectionRect };
        this.type = type;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DropTarget{T}"/> class.
    /// </summary>
    /// <param name="targetElement">The target element.</param>
    /// <param name="detectionRects">The detection rectangles.</param>
    /// <param name="type">The drop target type.</param>
    protected DropTarget(T targetElement, IEnumerable<Rect> detectionRects, DropTargetType type)
    {
        this.targetElement = targetElement;
        detectionRect = detectionRects.ToArray();
        this.type = type;
    }

    /// <summary>
    /// Gets the detection rects.
    /// </summary>
    public Rect[] DetectionRects
    {
        get
        {
            return detectionRect;
        }
    }

    /// <summary>
    /// Gets the target element.
    /// </summary>
    public T TargetElement
    {
        get
        {
            return targetElement;
        }
    }

    /// <summary>
    /// Gets the type.
    /// </summary>
    public override DropTargetType Type
    {
        get
        {
            return type;
        }
    }

    /// <summary>
    /// Drops the specified floating window onto the target.
    /// </summary>
    /// <param name="floatingWindow">The floating window.</param>
    protected virtual void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
    }

    /// <summary>
    /// Drops the specified floating window onto the target.
    /// </summary>
    /// <param name="floatingWindow">The floating window.</param>
    protected virtual void Drop(LayoutDocumentFloatingWindow floatingWindow)
    {
    }

    /// <summary>
    /// Determines whether the specified point intersects the target.
    /// </summary>
    /// <param name="dragPoint">The drag point.</param>
    /// <returns>true if the specified point intersects the target; otherwise, false.</returns>
    public override bool HitTestScreen(Point dragPoint)
    {
        // The native overlay already supplies detection rectangles in desktop pixels.
        return targetElement.IsLoaded && HitTest(dragPoint);
    }

    /// <summary>
    /// Drops the specified floating window onto the target.
    /// </summary>
    /// <param name="floatingWindow">The floating window.</param>
    public override void Drop(LayoutFloatingWindow floatingWindow)
    {
        if (CanCommit != null && !CanCommit(floatingWindow))
        {
            return;
        }

        ILayoutRoot? root = floatingWindow.Root;
        if (root?.Manager is not { } manager)
        {
            return;
        }

        LayoutContent? currentActiveContent = ActiveContent ?? root.ActiveContent;

        // A drop onto a target that itself lives in a floating window merely re-hosts the contents;
        // they stay floating, so it is not a docking operation and must not raise the docking events.
        bool isDockingDrop = (targetElement as ILayoutControl)?.Model?.FindParent<LayoutFloatingWindow>() == null;

        // Check ContentDocking before any layout mutation starts - the only point where the operation
        // can still be cancelled atomically. The matching ContentDocked is raised after the drop has
        // settled, for each content that actually left its floating window.
        LayoutContent[]? droppedContents = null;
        if (isDockingDrop)
        {
            droppedContents = floatingWindow.Descendents().OfType<LayoutContent>().ToArray();
            foreach (LayoutContent content in droppedContents)
            {
                if (!manager.RaiseContentDocking(content))
                {
                    return;
                }
            }
        }

        // Application callbacks may change permissions, source membership or target ownership.
        if (CanCommit != null && !CanCommit(floatingWindow))
        {
            return;
        }

        if (floatingWindow is LayoutAnchorableFloatingWindow fwAsAnchorable)
        {
            Drop(fwAsAnchorable);
        }
        else if (floatingWindow is LayoutDocumentFloatingWindow fwAsDocument)
        {
            Drop(fwAsDocument);
        }
        else
        {
            return;
        }

        if (droppedContents != null)
        {
            foreach (LayoutContent content in droppedContents)
            {
                if (!content.IsFloating && content.Root == root)
                {
                    manager.RaiseContentDocked(content);
                }
            }
        }

        if (currentActiveContent == null)
        {
            return;
        }

        targetElement.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (!ReferenceEquals(currentActiveContent.Root, root))
            {
                return;
            }
            // Native TabView selection also reparents content; toggling selection off here
            // can unload the destination again. Restore focus after input/layout at background priority.
            manager.FocusDroppedContent(currentActiveContent);
        });
    }

    /// <summary>
    /// Determines whether the specified point intersects the target.
    /// </summary>
    /// <param name="dragPoint">The drag point.</param>
    /// <returns>true if the specified point intersects the target; otherwise, false.</returns>
    public virtual bool HitTest(Point dragPoint)
    {
        return detectionRect.Any(dr => dr.Contains(dragPoint));
    }

    /// <summary>
    /// Gets the preview path.
    /// </summary>
    /// <param name="overlayWindow">The overlay window.</param>
    /// <param name="floatingWindow">The floating window.</param>
    /// <returns>The preview path.</returns>
    public abstract override Geometry? GetPreviewPath(OverlayWindow overlayWindow, LayoutFloatingWindow floatingWindow);

    /// <summary>
    /// Drag enter.
    /// </summary>
    public override void DragEnter()
    {
        SetIsDraggingOver(TargetElement, true);
    }

    /// <summary>
    /// Drag leave.
    /// </summary>
    public override void DragLeave()
    {
        SetIsDraggingOver(TargetElement, false);
    }
}
