// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/FocusElementManager.cs
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AvalonDock.Controls;

internal static class FocusElementManager
{
    private static readonly List<DockingManager> Managers = new();
    private static readonly FullWeakDictionary<ILayoutElement, UIElement> ModelFocusedElement = new();
    private static readonly WeakDictionary<ILayoutElement, INativeFocusTarget> ModelFocusedWindowHandle = new();
    private static IDisposable? windowHandler;
    private static WeakReference? lastFocusedElement;

    internal static void SetupFocusManagement(DockingManager manager)
    {
        if (Managers.Contains(manager))
        {
            return;
        }
        // A WinUI manager's routed events cannot cross secondary XamlRoots. The global
        // before-focus event supplies the original new-focus/ancestor path in every island.
        if (Managers.Count == 0)
        {
            windowHandler = PlatformServices.Focus.ObserveNativeFocus(WindowFocusChanging);
            FocusManager.GettingFocus += OnManagerPreviewGotKeyboardFocus;
        }
        Managers.Add(manager);
    }
    internal static void FinalizeFocusManagement(DockingManager manager)
    {
        Managers.Remove(manager);
        if (Managers.Count == 0)
        {
            FocusManager.GettingFocus -= OnManagerPreviewGotKeyboardFocus;
            windowHandler?.Dispose();
            windowHandler = null;
        }
    }

    internal static UIElement? GetLastFocusedElement(ILayoutElement model)
    {
        if (ModelFocusedElement.GetValue(model, out UIElement? objectWithFocus))
        {
            return objectWithFocus;
        }

        return null;
    }

    internal static void SetFocusOnLastElement(ILayoutElement model)
    {
        bool focused = false;
        if (ModelFocusedElement.GetValue(model, out UIElement? objectToFocus))
        {
            focused = FocusNativeElement(objectToFocus);
        }

        if (ModelFocusedWindowHandle.GetValue(model, out INativeFocusTarget? handleToFocus))
        {
            focused = handleToFocus.Focus();
        }

        if (focused)
        {
            lastFocusedElement = new WeakReference(model);
        }
    }

    internal static INativeFocusTarget? GetLastWindowHandle(ILayoutElement model)
    {
        if (ModelFocusedWindowHandle.GetValue(model, out INativeFocusTarget? handleWithFocus))
        {
            return handleWithFocus;
        }

        return null;
    }

    private static void WindowFocusChanging(UIElement hostContainingFocusedHandle, INativeFocusTarget focusedWindow)
    {
        foreach (DockingManager manager in Managers)
        {
            LayoutAnchorableControl? parentAnchorable = hostContainingFocusedHandle.FindVisualAncestor<LayoutAnchorableControl>();
            if (parentAnchorable?.Model is { } anchorable && ReferenceEquals(anchorable.Root?.Manager, manager))
            {
                ModelFocusedWindowHandle[anchorable] = focusedWindow;
                anchorable.IsActive = true;
            }
            else
            {
                LayoutDocumentControl? parentDocument = hostContainingFocusedHandle.FindVisualAncestor<LayoutDocumentControl>();
                if (parentDocument?.Model is { } document && ReferenceEquals(document.Root?.Manager, manager))
                {
                    ModelFocusedWindowHandle[document] = focusedWindow;
                    document.IsActive = true;
                }
            }
        }
    }

    private static bool FocusNativeElement(UIElement element)
    {
        return PlatformServices.Focus.Focus(element, FocusState.Programmatic);
    }

    private static void OnManagerPreviewGotKeyboardFocus(object? sender, GettingFocusEventArgs e)
    {
        UIElement? focusedElement = e.NewFocusedElement as UIElement;
        if (focusedElement != null &&
            !(focusedElement is LayoutAnchorableTabItem || focusedElement is LayoutDocumentTabItem))
        // Avoid tracking focus for elements like this
        {
            LayoutAnchorableControl? parentAnchorable = focusedElement.FindVisualAncestor<LayoutAnchorableControl>();
            if (parentAnchorable?.Model is { } anchorable)
            {
                if (anchorable.Root?.Manager is { } manager && Managers.Contains(manager))
                {
                    ModelFocusedElement[anchorable] = focusedElement;
                }
            }
            else
            {
                LayoutDocumentControl? parentDocument = focusedElement.FindVisualAncestor<LayoutDocumentControl>();
                if (parentDocument?.Model is { } document)
                {
                    if (document.Root?.Manager is { } manager && Managers.Contains(manager))
                    {
                        ModelFocusedElement[document] = focusedElement;
                    }
                }
            }
        }
    }
}
