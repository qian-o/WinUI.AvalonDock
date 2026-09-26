// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutAnchorablePaneControl.cs
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AvalonDock.Controls;

/// <summary>Displays an anchorable pane while the model retains selection and ownership.</summary>
public class LayoutAnchorablePaneControl : TabControlEx, ILayoutControl
{
    private readonly LayoutAnchorablePane model;
    private readonly LayoutPanePresenter presenter;
    private WeakReference? activeContentOnNativeFocus;

    internal LayoutAnchorablePaneControl(LayoutAnchorablePane model, bool IsVirtualizing, bool ignoreTabControlKeyBindingBindings = false)
        : base(IsVirtualizing, ignoreTabControlKeyBindingBindings)
    {
        DefaultStyleKey = typeof(LayoutAnchorablePaneControl);
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        IsTabStop = false;
        presenter = new LayoutPanePresenter(this, model);
        GettingFocus += (_, _) => activeContentOnNativeFocus = new WeakReference(model.Root?.ActiveContent);
        GotFocus += (_, e) =>
        {
            // An activation after synchronous GettingFocus supersedes queued GotFocus.
            if (IsLoaded && (ReferenceEquals(model.Root?.ActiveContent, activeContentOnNativeFocus.GetValueOrDefault<LayoutContent>()) || model.SelectedContent?.IsActive == true)
                && e.OriginalSource is UIElement element && Platforms.PlatformServices.Focus.HasKeyboardFocus(element))
            {
                OnGotKeyboardFocus(e);
            }
        };
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerInput), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerInput), true);
    }

    [Bindable(false)]
    [Description("Gets the layout model of this control.")]
    [Category("Other")]
    public ILayoutElement Model => model;

    protected virtual void OnGotKeyboardFocus(RoutedEventArgs e)
    {
        if (model?.SelectedContent != null)
        {
            model.SelectedContent.IsActive = true;
        }
    }

    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
    {
        if (!e.Handled && model.SelectedContent != null)
        {
            model.SelectedContent.IsActive = true;
        }
    }

    protected virtual void OnMouseRightButtonDown(PointerRoutedEventArgs e)
    {
        if (!e.Handled && model.SelectedContent != null)
        {
            model.SelectedContent.IsActive = true;
        }
    }

    internal void InitializeView() => presenter.Attach();
    internal void ReleaseView() => presenter.Dispose();

    private void OnPointerInput(object? sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        switch (properties.PointerUpdateKind)
        {
            case Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed:
                OnMouseLeftButtonDown(e);
                break;
            case Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed:
                OnMouseRightButtonDown(e);
                break;
        }
    }
}
