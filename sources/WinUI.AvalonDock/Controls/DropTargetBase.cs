// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DropTargetBase.cs
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the drop Target Base.
/// </summary>
internal abstract class DropTargetBase : DependencyObject, IDropTarget
{
    // Native overlay measurements and a source snapshot are supplied by the shared session.
    private OverlayTarget? target;
    internal OverlayTarget Target
    {
        get => target ?? throw new InvalidOperationException("拖放目标尚未初始化。");
        set => target = value;
    }
    internal int TabIndex { get; set; } = -1;
    internal Layout.LayoutContent? ActiveContent
    {
        get; set;
    }
    internal Func<Layout.LayoutFloatingWindow, bool>? CanCommit
    {
        get; set;
    }
    public abstract DropTargetType Type
    {
        get;
    }
    public abstract bool HitTestScreen(Windows.Foundation.Point dragPoint);
    public abstract Microsoft.UI.Xaml.Media.Geometry? GetPreviewPath(OverlayWindow overlayWindow, Layout.LayoutFloatingWindow floatingWindow);
    public abstract void Drop(Layout.LayoutFloatingWindow floatingWindow);
    public abstract void DragEnter();
    public abstract void DragLeave();

    /// <summary>
    /// IsDraggingOver attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsDraggingOverProperty = DependencyProperty.RegisterAttached("IsDraggingOver", typeof(bool), typeof(DropTargetBase),
            new PropertyMetadata((bool)false));

    /// <summary>
    /// Gets the get Is Dragging Over.
    /// </summary>
    /// <param name="d">The d.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    [Bindable(true)]
    [Description("Gets wether the user is dragging a window over the target element.")]
    [Category("Other")]
    public static bool GetIsDraggingOver(DependencyObject d)
    {
        return (bool)d.GetValue(IsDraggingOverProperty);
    }

    /// <summary>
    /// Sets the set Is Dragging Over.
    /// </summary>
    /// <param name="d">The d.</param>
    /// <param name="value">The value.</param>
    public static void SetIsDraggingOver(DependencyObject d, bool value)
    {
        d.SetValue(IsDraggingOverProperty, value);
    }
}
