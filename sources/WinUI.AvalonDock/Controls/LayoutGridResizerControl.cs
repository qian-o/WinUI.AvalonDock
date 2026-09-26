// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutGridResizerControl.cs
using System.ComponentModel;
using AvalonDock.Platforms;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays the splitter between neighboring layout panes.</summary>
// WinUI Thumb is sealed. Preserve its inherited drag event pattern on this Control;
// the platform input service owns native capture independently of the control template.
public class LayoutGridResizerControl : Control
{
    private Orientation? cursorOrientation;
    private IDragInputSession? dragSession;
    private DragInputPosition dragStartPosition;
    private DragInputPosition dragCurrentPosition;
    private double dragScale;

    public event DragStartedEventHandler? DragStarted;
    public event DragDeltaEventHandler? DragDelta;
    public event DragCompletedEventHandler? DragCompleted;

    internal DragInputPosition DragStartPosition => dragStartPosition;
    internal DragInputPosition DragCurrentPosition => dragCurrentPosition;

    public static readonly DependencyProperty IsDraggingProperty = ReadOnlyPropertyGuard.Register(
        nameof(IsDragging), typeof(bool), typeof(LayoutGridResizerControl), false);
    public bool IsDragging => (bool?)GetValue(IsDraggingProperty) ?? false;

    private static void VerifyWritable(DependencyProperty property) => ReadOnlyPropertyGuard.VerifyWritable(property, IsDraggingProperty);
    public new void SetValue(DependencyProperty property, object value)
    {
        VerifyWritable(property);
        base.SetValue(property, value);
    }
    public new void ClearValue(DependencyProperty property)
    {
        VerifyWritable(property);
        base.ClearValue(property);
    }
    public new void SetBinding(DependencyProperty property, BindingBase binding)
    {
        VerifyWritable(property);
        base.SetBinding(property, binding);
    }

    public LayoutGridResizerControl()
    {
        DefaultStyleKey = typeof(LayoutGridResizerControl);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = true;
        // WPF Thumb's inherited default excludes keyboard focus.
        IsTabStop = false;
        AddHandler(PointerPressedEvent, (PointerEventHandler)OnPointerPressed, true);
        Unloaded += (_, _) => { if (!IsLoaded) { CancelDrag(); } };
    }

    public static readonly DependencyProperty BackgroundWhileDraggingProperty = DependencyProperty.Register(
        nameof(BackgroundWhileDragging), typeof(Brush), typeof(LayoutGridResizerControl),
        PropertyMetadata.Create(() => new SolidColorBrush(Microsoft.UI.Colors.Black)));

    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the background brush of the control being dragged.")]
    [Category("Other")]
    public Brush BackgroundWhileDragging
    {
        get => (Brush)GetValue(BackgroundWhileDraggingProperty);
        set => SetValue(BackgroundWhileDraggingProperty, value);
    }

    public static readonly DependencyProperty OpacityWhileDraggingProperty = DependencyProperty.Register(
        nameof(OpacityWhileDragging), typeof(double), typeof(LayoutGridResizerControl), new PropertyMetadata(0.5));

    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the opacity while the control is being dragged.")]
    [Category("Other")]
    public double OpacityWhileDragging
    {
        get => (double?)GetValue(OpacityWhileDraggingProperty) ?? 0;
        set => SetValue(OpacityWhileDraggingProperty, value);
    }

    internal void SetResizeOrientation(Orientation orientation)
    {
        if (cursorOrientation == orientation)
        {
            return;
        }

        cursorOrientation = orientation;
        ProtectedCursor = InputSystemCursor.Create(orientation == Orientation.Horizontal
            ? InputSystemCursorShape.SizeWestEast
            : InputSystemCursorShape.SizeNorthSouth);
    }

    public void CancelDrag() => dragSession?.Cancel();

    private void OnPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (dragSession != null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        IDragInputSession? input = PlatformServices.CreateDragInputService(this).TryBegin(OnDragInput);
        if (input == null)
        {
            return;
        }

        dragSession = input;
        dragStartPosition = dragCurrentPosition = input.Position;
        dragScale = XamlRoot?.RasterizationScale ?? 1;
        e.Handled = true;
        Point offset = e.GetCurrentPoint(this).Position;
        try
        {
            ReadOnlyPropertyGuard.Set(this, IsDraggingProperty, true);
            DragStarted?.Invoke(this, new DragStartedEventArgs(offset.X, offset.Y));
        }
        catch { CancelDrag(); throw; }
    }

    private void OnDragInput(DragInputUpdate update)
    {
        DragInputPosition previous = dragCurrentPosition;
        dragCurrentPosition = update.Position;
        if (update.Kind == DragInputUpdateKind.Moved)
        {
            DragDelta?.Invoke(this, new DragDeltaEventArgs(
                (dragCurrentPosition.X - previous.X) / dragScale,
                (dragCurrentPosition.Y - previous.Y) / dragScale));
            return;
        }

        IDragInputSession? input = dragSession;
        dragSession = null;
        try
        {
            ReadOnlyPropertyGuard.Set(this, IsDraggingProperty, false);
            DragCompleted?.Invoke(this, new DragCompletedEventArgs(
                (dragCurrentPosition.X - dragStartPosition.X) / dragScale,
                (dragCurrentPosition.Y - dragStartPosition.Y) / dragScale,
                update.Kind == DragInputUpdateKind.Cancelled));
        }
        finally { input?.Dispose(); }
    }
}
