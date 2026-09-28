// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using System.ComponentModel;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private readonly Dictionary<IToolbox, LayoutAnchorable> toolboxToAnchorable = [];
    private int syncDepth;

    private void ApplyInitialToolboxState()
    {
        if (Layout == null)
        {
            return;
        }

        foreach (LayoutAnchorable? anchorable in Layout.Descendents().OfType<LayoutAnchorable>().ToList())
        {
            if (!(anchorable.Content is IToolbox toolbox))
            {
                continue;
            }

            if (!toolbox.IsOpen && !toolbox.IsOpenByDefault)
            {
                continue;
            }

            if (anchorable.IsAutoHidden && !IsDetached(anchorable))
            {
                ToggleAnchorable(anchorable, toolbox.Zone);
            }
            else
            {
                // Already on screen - only the toolbox still has to be told, which is what turns
                // IsOpenByDefault into an IsOpen the application can read back.
                SetToolboxIsOpen(anchorable);
            }
        }
    }
    private void SyncToolboxStateToLayout()
    {
        foreach (LayoutAnchorable? anchorable in toolboxToAnchorable.Values.ToList())
        {
            SetToolboxIsOpen(anchorable);
        }
    }
    private void RegisterToolboxesFromBars()
    {
        foreach (ToggleDockButtonBar bar in Bars)
        {
            foreach (object? item in bar.Items)
            {
                if (item is ToggleDockButton btn && btn.Anchorable?.Content is IToolbox toolbox)
                {
                    RegisterToolbox(toolbox, btn.Anchorable);
                }
            }
        }
    }
    internal void RegisterToolbox(IToolbox toolbox, LayoutAnchorable anchorable)
    {
        if (IsDisposed)
        {
            return;
        }

        if (toolboxToAnchorable.ContainsKey(toolbox))
        {
            toolboxToAnchorable[toolbox] = anchorable;
            return;
        }

        toolboxToAnchorable[toolbox] = anchorable;

        if (toolbox is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnToolboxPropertyChanged;
        }

        RefreshShortcuts();
    }
    internal void UnregisterToolbox(IToolbox toolbox)
    {
        toolboxToAnchorable.Remove(toolbox);

        if (toolbox is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= OnToolboxPropertyChanged;
        }

        RefreshShortcuts();
    }
    private void OnToolboxPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed || e.PropertyName != nameof(IToolbox.IsOpen) || syncDepth > 0)
        {
            return;
        }

        if (!(sender is IToolbox toolbox) || !toolboxToAnchorable.TryGetValue(toolbox, out LayoutAnchorable? anchorable))
        {
            return;
        }

        // A detached anchorable sits collapsed on its stripe while its content is on screen in a
        // standalone window, so IsAutoHidden on its own does not say whether the toolbox is showing.
        bool isOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);

        if (toolbox.IsOpen == isOpen)
        {
            return;
        }

        // ToggleAnchorable is the one implementation of this transition: it carries the zone
        // bookkeeping, the layout priority handling and the detached window case, and it writes the
        // resulting state back onto the toolbox. Duplicating it here is what let the two paths drift
        // apart. The syncDepth guard keeps that write-back from re-entering this handler.
        syncDepth++;
        try
        {
            ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
        }
        finally
        {
            syncDepth--;
        }
    }
    private void SetToolboxIsOpen(LayoutAnchorable anchorable)
    {
        if (!(anchorable.Content is IToolbox toolbox))
        {
            return;
        }

        syncDepth++;
        try
        {
            // A detached anchorable is collapsed onto its stripe but its content is on screen in a
            // standalone window, so it counts as open.
            toolbox.IsOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);
        }
        finally
        {
            syncDepth--;
        }
    }
}
