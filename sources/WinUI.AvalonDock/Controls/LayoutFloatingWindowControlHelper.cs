// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutFloatingWindowControlHelper.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

internal static class LayoutFloatingWindowControlHelper
{
    private const string NotSupportedFloatingWindowTypeMessage = "Not Supported Floating Window Type: {0}";

    public static void ActiveTheContentOfSinglePane<T>(T fwc, bool isActive)
        where T : LayoutFloatingWindowControl
    {
        ILayoutContentSelector? selector = null;
        if (fwc is LayoutAnchorableFloatingWindowControl)
        {
            selector = fwc.Model
                .Descendents()
                    .OfType<LayoutAnchorablePane>()
                        .FirstOrDefault(p => p.ChildrenCount > 0 && p.SelectedContent != null);
        }
        else if (fwc is LayoutDocumentFloatingWindowControl)
        {
            selector = fwc.Model
                .Descendents()
                    .OfType<LayoutDocumentPane>()
                        .FirstOrDefault(p => p.ChildrenCount > 0 && p.SelectedContent != null);
        }
        else
        {
            throw new NotSupportedException(string.Format(NotSupportedFloatingWindowTypeMessage, fwc.GetType()));
        }

        if (selector?.SelectedContent is { } selectedContent)
        {
            selectedContent.IsActive = isActive;
        }
        else
        {
            ActiveTheLastActivedContent(fwc, isActive);
        }
    }

    public static void ActiveTheContentOfMultiPane<T>(T fwc, bool isActive)
        where T : LayoutFloatingWindowControl
    {
        if (isActive)
        {
            if (fwc is LayoutAnchorableFloatingWindowControl)
            {
                LayoutAnchorablePaneControl? paneControl = GetLayoutControlByMousePosition<LayoutAnchorablePaneControl>(fwc);
                if (paneControl != null && paneControl.Model is LayoutAnchorablePane pane)
                {
                    if (pane.SelectedContent != null)
                    {
                        pane.SelectedContent.IsActive = true;
                    }
                    else
                    {
                        ActiveTheLastActivedContentOfPane(pane);
                    }

                    return;
                }
            }
            else if (fwc is LayoutDocumentFloatingWindowControl)
            {
                LayoutDocumentPaneControl? paneControl = GetLayoutControlByMousePosition<LayoutDocumentPaneControl>(fwc);
                if (paneControl != null && paneControl.Model is LayoutDocumentPane pane)
                {
                    if (pane.SelectedContent != null)
                    {
                        pane.SelectedContent.IsActive = true;
                    }
                    else
                    {
                        ActiveTheLastActivedContentOfPane(pane);
                    }

                    return;
                }
            }
            else
            {
                throw new NotSupportedException(string.Format(NotSupportedFloatingWindowTypeMessage, fwc.GetType()));
            }
        }

        ActiveTheLastActivedContent(fwc, isActive);
    }

    public static void ActiveTheLastActivedContent(LayoutFloatingWindowControl fwc, bool isActive)
    {
        List<LayoutContent> items = fwc.Model.Descendents().OfType<LayoutContent>().ToList();
        int index = IndexOfLastActivedContent(items);
        if (index != -1)
        {
            items[index].IsActive = isActive;
        }
    }

    public static void ActiveTheLastActivedContentOfPane(LayoutAnchorablePane anchorablePane) =>
        ActivateLastContentOfPane(anchorablePane, anchorablePane.Children);

    public static void ActiveTheLastActivedContentOfPane(LayoutDocumentPane documentPane) =>
        ActivateLastContentOfPane(documentPane, documentPane.Children);

    private static void ActivateLastContentOfPane<T>(ILayoutContentSelector selector, IList<T> children)
        where T : LayoutContent
    {
        int index = IndexOfLastActivedContent(children);
        if (index != -1)
        {
            selector.SelectedContentIndex = index;
            if (selector.SelectedContent is { IsActive: false } content)
            {
                content.IsActive = true;
            }
        }
    }

    private static T? GetLayoutControlByMousePosition<T>(LayoutFloatingWindowControl fwc)
        where T : FrameworkElement, ILayoutControl
    {
        FrameworkElement? rootVisual = fwc.HostedRoot;
        if (rootVisual == null)
        {
            return null;
        }

        if (!PlatformServices.Coordinates.TryGetPointerPosition(out Point mousePosition))
        {
            return null;
        }

        foreach (T areaHost in rootVisual.FindVisualChildren<T>())
        {
            if (PlatformServices.Coordinates.TryGetScreenBounds(areaHost, out Rect rect) && rect.Contains(mousePosition))
            {
                return areaHost;
            }
        }

        return null;
    }

    private static int IndexOfLastActivedContent<T>(IList<T> list)
        where T : LayoutContent
    {
        if (list.Count > 0)
        {
            int index = 0;
            if (list.Count > 1)
            {
                DateTime? tmpTimeStamp = list[0].LastActivationTimeStamp;
                for (int i = 1; i < list.Count; i++)
                {
                    T item = list[i];
                    if (item.LastActivationTimeStamp > tmpTimeStamp)
                    {
                        tmpTimeStamp = item.LastActivationTimeStamp;
                        index = i;
                    }
                }
            }

            return index;
        }

        return -1;
    }
}
