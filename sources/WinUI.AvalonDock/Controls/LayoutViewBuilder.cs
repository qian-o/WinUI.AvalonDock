// Native initialization and release for the original typed layout controls.
// WinUI has no WPF OnInitialized callback before loading into an island.
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

internal static class LayoutViewBuilder
{
    internal static UIElement Initialize<T>(LayoutGridControl<T> control) where T : class, ILayoutPanelElement
    {
        control.InitializeView();
        return control;
    }

    internal static void Release(UIElement? view)
    {
        switch (view)
        {
            case LayoutPanelControl panel:
                panel.ReleaseView();
                break;
            case LayoutDocumentPaneGroupControl documents:
                documents.ReleaseView();
                break;
            case LayoutAnchorablePaneGroupControl tools:
                tools.ReleaseView();
                break;
            case LayoutDocumentPaneControl pane:
                pane.ReleaseView();
                break;
            case LayoutAnchorablePaneControl pane:
                pane.ReleaseView();
                break;
            case LayoutAnchorSideControl side:
                side.ReleaseView();
                break;
            case LayoutAnchorGroupControl group:
                group.ReleaseView();
                break;
            case LayoutAnchorControl anchor:
                anchor.ReleaseView();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

}
