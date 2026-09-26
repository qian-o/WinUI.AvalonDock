// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DocumentPaneDropAsAnchorableTarget.cs
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>将浮动工具窗格停靠在文档窗格旁。</summary>
internal class DocumentPaneDropAsAnchorableTarget : DropTarget<LayoutDocumentPaneControl>
{
    private readonly LayoutDocumentPaneControl targetPane;

    internal DocumentPaneDropAsAnchorableTarget(LayoutDocumentPaneControl paneControl, Rect detectionRect, DropTargetType type)
        : base(paneControl, detectionRect, type) => targetPane = paneControl;

    public override bool HitTestScreen(Point dragPoint) => GetPlacement() is not null
        && TryGetParent(out _, out _, out _) && base.HitTestScreen(dragPoint);

    public override void Drop(LayoutFloatingWindow floatingWindow)
    {
        if (floatingWindow is not LayoutAnchorableFloatingWindow { RootPanel: not null }
            || GetPlacement() is null || !TryGetParent(out _, out _, out _))
        {
            return;
        }

        base.Drop(floatingWindow);
    }

    protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
        if (GetPlacement() is not { } placement || !TryGetParent(out _, out LayoutPanel? parentPanel, out ILayoutPanelElement? target)
            || floatingWindow.RootPanel is not { } floatingPanel)
        {
            return;
        }

        (Orientation orientation, bool insertAfter) = placement;
        int targetIndex = parentPanel.IndexOfChild(target);
        if (parentPanel.ChildrenCount == 1)
        {
            parentPanel.Orientation = orientation;
        }

        if (parentPanel.Orientation == orientation)
        {
            parentPanel.Children.Insert(targetIndex + (insertAfter ? 1 : 0), floatingPanel);
            return;
        }

        LayoutPanel newPanel = new()
        {
            Orientation = orientation
        };
        parentPanel.ReplaceChild(target, newPanel);
        newPanel.Children.Add(target);
        newPanel.Children.Insert(insertAfter ? 1 : 0, floatingPanel);
    }

    public override Geometry? GetPreviewPath(OverlayWindow overlayWindow, LayoutFloatingWindow floatingWindowModel)
    {
        if (GetPlacement() is not { } placement || !TryGetParent(out LayoutDocumentPaneGroup? parentGroup, out LayoutPanel? parentPanel, out _))
        {
            return null;
        }

        ILayoutPanelElement previewModel = parentGroup is null ? parentPanel : parentGroup;
        FrameworkElement? visual = overlayWindow.GetLayoutControls()
            .FirstOrDefault(control => ReferenceEquals(control.Model, previewModel)) as FrameworkElement;
        if (visual is null)
        {
            return null;
        }

        Rect bounds = overlayWindow.GetPreviewBounds(visual);
        (Orientation orientation, bool insertAfter) = placement;
        if (orientation == Orientation.Vertical)
        {
            if (insertAfter)
            {
                bounds.Y += bounds.Height - bounds.Height / 3.0;
            }

            bounds.Height /= 3.0;
        }
        else
        {
            if (insertAfter)
            {
                bounds.X += bounds.Width - bounds.Width / 3.0;
            }

            bounds.Width /= 3.0;
        }
        return new RectangleGeometry { Rect = bounds };
    }

    private (Orientation Orientation, bool InsertAfter)? GetPlacement() => Type switch
    {
        DropTargetType.DocumentPaneDockAsAnchorableBottom => (Orientation.Vertical, true),
        DropTargetType.DocumentPaneDockAsAnchorableTop => (Orientation.Vertical, false),
        DropTargetType.DocumentPaneDockAsAnchorableRight => (Orientation.Horizontal, true),
        DropTargetType.DocumentPaneDockAsAnchorableLeft => (Orientation.Horizontal, false),
        _ => null,
    };

    private bool TryGetParent(out LayoutDocumentPaneGroup? parentGroup, [NotNullWhen(true)] out LayoutPanel? parentPanel,
        [NotNullWhen(true)] out ILayoutPanelElement? target)
    {
        parentGroup = null;
        parentPanel = null;
        target = null;
        if (targetPane.Model is not ILayoutDocumentPane pane)
        {
            return false;
        }

        if (pane.Parent is LayoutPanel panel)
        {
            parentPanel = panel;
            target = pane;
            return panel.IndexOfChild(target) >= 0;
        }

        for (LayoutDocumentPaneGroup? group = pane.Parent as LayoutDocumentPaneGroup; group is not null;
             group = group.Parent as LayoutDocumentPaneGroup)
        {
            if (group.Parent is not LayoutPanel owner)
            {
                continue;
            }

            parentGroup = group;
            parentPanel = owner;
            target = group;
            return owner.IndexOfChild(target) >= 0;
        }

        // 目标若已离开布局树，拖放指示和提交都不可用。
        return false;
    }
}
