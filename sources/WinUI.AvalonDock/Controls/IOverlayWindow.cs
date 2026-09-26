// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/IOverlayWindow.cs.
namespace AvalonDock.Controls;

internal interface IOverlayWindow
{
    IEnumerable<IDropTarget> GetTargets();
    void DragEnter(LayoutFloatingWindowControl floatingWindow);
    void DragLeave(LayoutFloatingWindowControl floatingWindow);
    void DragEnter(IDropArea area);
    void DragLeave(IDropArea area);
    void DragEnter(IDropTarget target);
    void DragLeave(IDropTarget target);
    void DragDrop(IDropTarget target);
}
