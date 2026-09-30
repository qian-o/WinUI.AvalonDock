// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutAnchorableFloatingWindowControl.cs and LayoutDocumentFloatingWindowControl.cs
using System.Windows.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    internal bool CanExecuteContentCommand<TContent, TItem>(object parameter,
        Func<TContent, bool> contentAllowsCommand, Func<TItem, ICommand?> selectCommand)
        where TContent : LayoutContent
        where TItem : LayoutItem
    {
        DockingManager? manager = Model?.Root?.Manager;
        if (manager == null)
        {
            return false;
        }

        bool hasContent = false;
        foreach (TContent content in Model.Descendents().OfType<TContent>().ToArray())
        {
            if (!contentAllowsCommand(content)
                || manager.GetLayoutItemFromModel(content) is not TItem item
                || selectCommand(item) is not { } command
                || !command.CanExecute(parameter))
            {
                return false;
            }

            hasContent = true;
        }

        return hasContent;
    }

    internal bool ExecuteContentCommand<TContent, TItem>(object parameter, Func<TItem, ICommand?> selectCommand)
        where TContent : LayoutContent
        where TItem : LayoutItem
    {
        DockingManager? manager = Model.Root?.Manager;
        if (manager == null)
        {
            return false;
        }

        // Commands can remove or reparent content. Complete the snapshot before executing any.
        foreach (TContent content in Model.Descendents().OfType<TContent>().ToArray())
        {
            if (manager.GetLayoutItemFromModel(content) is TItem item)
            {
                selectCommand(item)?.Execute(parameter);
            }
        }

        return true;
    }
}
