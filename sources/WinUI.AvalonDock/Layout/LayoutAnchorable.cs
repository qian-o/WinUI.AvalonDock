// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorable.cs

using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout anchorable.
/// </summary>
[Serializable]
public class LayoutAnchorable : LayoutContent, Core.Serialization.ISerializableLayoutAnchorable
{
    private double autohideWidth = 0.0;
    private double autohideMinWidth = 100.0;
    private double autohideHeight = 0.0;
    private double autohideMinHeight = 100.0;
    private bool canHide = true;
    private bool canAutoHide = true;
    private bool isDetached;
    private bool canDockAsTabbedDocument = true;
    private bool canMove = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorable"/> class.
    /// </summary>
    public LayoutAnchorable()
    {
        // LayoutAnchorable will hide by default, not close.
        // BD: 14.08.2020 Inverting both canClose and canCloseDefault to false as anchorables are only hidden but not closed
        //     That would allow CanClose to be properly serialized if set to true for an instance of LayoutAnchorable
        canClose = canCloseDefault = false;
    }

    /// <summary>
    /// Occurs when the is visible changed event is raised.
    /// </summary>
    public event EventHandler? IsVisibleChanged;

    /// <summary>
    /// Occurs when the hiding event is raised.
    /// </summary>
    public event EventHandler<CancelEventArgs>? Hiding;

    /// <summary>
    /// Gets or sets the auto hide width.
    /// </summary>
    public double AutoHideWidth
    {
        get => autohideWidth;
        set
        {
            if (value == autohideWidth)
            {
                return;
            }

            RaisePropertyChanging(nameof(AutoHideWidth));
            value = Math.Max(value, autohideMinWidth);
            autohideWidth = value;
            RaisePropertyChanged(nameof(AutoHideWidth));
        }
    }

    /// <summary>
    /// Gets or sets the auto hide min width.
    /// </summary>
    public double AutoHideMinWidth
    {
        get => autohideMinWidth;
        set
        {
            if (value == autohideMinWidth)
            {
                return;
            }

            RaisePropertyChanging(nameof(AutoHideMinWidth));
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("Negative value is not allowed.", nameof(value));
            }

            autohideMinWidth = value;
            RaisePropertyChanged(nameof(AutoHideMinWidth));
        }
    }

    /// <summary>
    /// Gets or sets the auto hide height.
    /// </summary>
    public double AutoHideHeight
    {
        get => autohideHeight;
        set
        {
            if (value == autohideHeight)
            {
                return;
            }

            RaisePropertyChanging(nameof(AutoHideHeight));
            value = Math.Max(value, autohideMinHeight);
            autohideHeight = value;
            RaisePropertyChanged(nameof(AutoHideHeight));
        }
    }

    /// <summary>
    /// Gets or sets the auto hide min height.
    /// </summary>
    public double AutoHideMinHeight
    {
        get => autohideMinHeight;
        set
        {
            if (value == autohideMinHeight)
            {
                return;
            }

            RaisePropertyChanging(nameof(AutoHideMinHeight));
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("Negative value is not allowed.", nameof(value));
            }

            autohideMinHeight = value;
            RaisePropertyChanged(nameof(AutoHideMinHeight));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can hide.
    /// </summary>
    public bool CanHide
    {
        get => canHide;
        set
        {
            if (value == canHide)
            {
                return;
            }

            canHide = value;
            RaisePropertyChanged(nameof(CanHide));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can auto hide.
    /// </summary>
    public bool CanAutoHide
    {
        get => canAutoHide;
        set
        {
            if (value == canAutoHide)
            {
                return;
            }

            canAutoHide = value;
            RaisePropertyChanged(nameof(CanAutoHide));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can dock as tabbed document.
    /// </summary>
    public bool CanDockAsTabbedDocument
    {
        get => canDockAsTabbedDocument;
        set
        {
            if (canDockAsTabbedDocument == value)
            {
                return;
            }

            canDockAsTabbedDocument = value;
            RaisePropertyChanged(nameof(CanDockAsTabbedDocument));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can move.
    /// </summary>
    public bool CanMove
    {
        get => canMove;
        set
        {
            if (value == canMove)
            {
                return;
            }

            canMove = value;
            RaisePropertyChanged(nameof(CanMove));
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is auto hidden.
    /// </summary>
    public bool IsAutoHidden => Parent is LayoutAnchorGroup;

    /// <summary>
    /// Gets a value indicating whether this instance is hidden.
    /// </summary>
    [XmlIgnore]
    public bool IsHidden => Parent is LayoutRoot;

    /// <summary>
    /// Gets or sets a value indicating whether the content of this anchorable is currently hosted by
    /// a standalone window outside of the docking layout.
    /// </summary>
    /// <remarks>
    /// Maintained by <see cref="DockingManager.DetachAnchorableToWindow"/> and
    /// <see cref="DockingManager.ReattachAnchorable"/>. It takes part in layout serialization so that
    /// a restored layout can recreate the window; setting it by hand does not detach anything.
    /// The window geometry rides on the <see cref="LayoutContent.FloatingLeft"/> family of properties.
    /// </remarks>
    public bool IsDetached
    {
        get => isDetached;
        set
        {
            if (isDetached == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsDetached));
            isDetached = value;
            RaisePropertyChanged(nameof(IsDetached));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is visible.
    /// </summary>
    [XmlIgnore]
    public bool IsVisible
    {
        get => Parent != null && !(Parent is LayoutRoot);
        set
        {
            if (value)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        UpdateParentVisibility();
        RaisePropertyChanged(nameof(IsVisible));
        NotifyIsVisibleChanged();
        RaisePropertyChanged(nameof(IsHidden));
        RaisePropertyChanged(nameof(IsAutoHidden));
        base.OnParentChanged(oldValue, newValue);
    }

    /// <inheritdoc/>
    protected override void InternalDock()
    {
        if (Root is not LayoutRoot root)
        {
            throw new InvalidOperationException();
        }

        LayoutAnchorablePane? anchorablePane = null;

        // look for active content parent pane
        if (root.ActiveContent is { } activeContent && activeContent != this)
        {
            anchorablePane = activeContent.Parent as LayoutAnchorablePane;
        }
        // look for a pane on the right side
        if (anchorablePane == null)
        {
            anchorablePane = root.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(pane => !pane.IsHostedInFloatingWindow && pane.GetSide() == AnchorSide.Right);
        }
        // look for an available pane
        if (anchorablePane == null)
        {
            anchorablePane = root.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
        }

        bool added = false;
        ILayoutUpdateStrategy? strategy = root.Manager?.LayoutUpdateStrategy;
        if (strategy != null)
        {
            added = strategy.BeforeInsertAnchorable(root, this, anchorablePane);
        }

        if (!added)
        {
            if (anchorablePane == null)
            {
                LayoutPanel mainLayoutPanel = new()
                {
                    Orientation = Orientation.Horizontal
                };
                if (root.RootPanel != null)
                {
                    mainLayoutPanel.Children.Add(root.RootPanel);
                }

                root.RootPanel = mainLayoutPanel;
                anchorablePane = new LayoutAnchorablePane { DockWidth = new GridLength(200.0, GridUnitType.Pixel) };
                mainLayoutPanel.Children.Add(anchorablePane);
            }

            anchorablePane.Children.Add(this);
        }

        strategy?.AfterInsertAnchorable(root, this);
        base.InternalDock();
    }

    /// <inheritdoc/>
    public override void Close()
    {
        if (Root?.Manager is { } dockingManager)
        {
            dockingManager.ExecuteCloseCommand(this);
        }
        else
        {
            CloseAnchorable();
        }
    }

    /// <summary>
    /// Executes the on hiding operation.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    protected virtual void OnHiding(CancelEventArgs args) => Hiding?.Invoke(this, args);

    internal bool TestCanHide()
    {
        CancelEventArgs args = new();
        OnHiding(args);
        return !args.Cancel;
    }

    /// <summary>
    /// Executes the hide operation.
    /// </summary>
    public void Hide()
    {
        if (Root?.Manager is DockingManager dockingManager)
        {
            dockingManager.ExecuteHideCommand(this);
        }
        else
        {
            HideAnchorable(true);
        }
    }

    /// <summary>
    /// Executes the hide anchorable operation.
    /// </summary>
    /// <param name="cancelable">The cancelable.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    public bool HideAnchorable(bool cancelable)
    {
        if (!IsVisible)
        {
            IsSelected = true;
            IsActive = true;
            return false;
        }

        if (cancelable)
        {
            if (!TestCanHide())
            {
                return false;
            }
        }

        RaisePropertyChanging(nameof(IsHidden));
        RaisePropertyChanging(nameof(IsVisible));
        if (Parent is ILayoutGroup parentAsGroup)
        {
            PreviousContainer = parentAsGroup;
            PreviousContainerIndex = parentAsGroup.IndexOfChild(this);
        }

        Root?.Hidden?.Add(this);
        RaisePropertyChanged(nameof(IsVisible));
        RaisePropertyChanged(nameof(IsHidden));
        NotifyIsVisibleChanged();

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Hiding detaches the anchorable from whatever pane or anchor group holds it, through the same
    /// path the rest of the layout uses; dropping it from the hidden list afterwards leaves it
    /// attached to nothing. <see cref="Close"/> is deliberately not used: for an auto hidden
    /// anchorable it first calls <see cref="ToggleAutoHide"/>, which would drag the whole anchor
    /// group - siblings included - back into the docked area on the way out.
    /// </remarks>
    public void RemoveFromLayout()
    {
        if (!IsHidden)
        {
            HideAnchorable(false);
        }

        Root?.Hidden?.Remove(this);
    }

    /// <summary>
    /// Executes the show operation.
    /// </summary>
    public void Show()
    {
        if (IsVisible)
        {
            return;
        }

        if (!IsHidden)
        {
            throw new InvalidOperationException();
        }

        RaisePropertyChanging(nameof(IsHidden));
        RaisePropertyChanging(nameof(IsVisible));
        bool added = false;
        LayoutRoot? root = Root as LayoutRoot;
        ILayoutUpdateStrategy? strategy = root?.Manager?.LayoutUpdateStrategy;
        if (root != null && strategy != null)
        {
            added = strategy.BeforeInsertAnchorable(root, this, PreviousContainer);
        }

        // The pane an anchorable was hidden from is not guaranteed to be there when it comes back.
        // A layout restored from file replaces the docked area wholesale, which leaves the
        // reference pointing off the tree, and CollectGarbage nulls it outright once that has
        // happened. Without a fallback Show() then walked past the branch below and returned
        // having done nothing - no move, no exception - leaving the anchorable invisible.
        ILayoutGroup? container = PreviousContainer as ILayoutGroup;
        bool restoresToPreviousContainer = container != null && ReferenceEquals(container.Root, root);
        if (!added && !restoresToPreviousContainer)
        {
            container = FindContainerForShow(root);
        }

        if (!added && container != null)
        {
            // The remembered index belongs to the remembered pane; in any other one it means nothing.
            if (restoresToPreviousContainer && PreviousContainerIndex >= 0 && PreviousContainerIndex < container.ChildrenCount)
            {
                container.InsertChildAt(PreviousContainerIndex, this);
            }
            else
            {
                container.InsertChildAt(container.ChildrenCount, this);
            }

            Parent = container;
            IsSelected = true;
            IsActive = true;
        }

        if (root != null)
        {
            strategy?.AfterInsertAnchorable(root, this);
        }

        PreviousContainer = null;
        PreviousContainerIndex = -1;
        RaisePropertyChanged(nameof(IsVisible));
        RaisePropertyChanged(nameof(IsHidden));
        NotifyIsVisibleChanged();
    }

    /// <summary>
    /// Finds the pane to show an anchorable in when the one it remembers is gone.
    /// </summary>
    /// <param name="root">The layout root the anchorable belongs to.</param>
    /// <returns>
    /// A pane of the docked area, creating one if the layout holds none, or <c>null</c> if there is
    /// no layout to add to.
    /// </returns>
    /// <remarks>
    /// Only the docked area is searched. A pane inside a floating window or parked on an auto hide
    /// side would take the anchorable somewhere the user never asked for, which is worse than the
    /// fresh pane created here.
    /// </remarks>
    private static ILayoutGroup? FindContainerForShow(LayoutRoot? root)
    {
        LayoutPanel? panel = root?.RootPanel;
        if (panel == null)
        {
            return null;
        }

        LayoutAnchorablePane? pane = panel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
        if (pane != null)
        {
            return pane;
        }

        pane = new LayoutAnchorablePane();
        panel.Children.Add(pane);
        return pane;
    }

    /// <summary>
    /// Executes the add to layout operation.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="strategy">The strategy.</param>
    public void AddToLayout(DockingManager manager, AnchorableShowStrategy strategy)
    {
        if (IsVisible || IsHidden)
        {
            throw new InvalidOperationException();
        }

        bool most = (strategy & AnchorableShowStrategy.Most) == AnchorableShowStrategy.Most;
        bool left = (strategy & AnchorableShowStrategy.Left) == AnchorableShowStrategy.Left;
        bool right = (strategy & AnchorableShowStrategy.Right) == AnchorableShowStrategy.Right;
        bool top = (strategy & AnchorableShowStrategy.Top) == AnchorableShowStrategy.Top;
        bool bottom = (strategy & AnchorableShowStrategy.Bottom) == AnchorableShowStrategy.Bottom;

        if (!most)
        {
            AnchorSide side = AnchorSide.Left;
            if (left)
            {
                side = AnchorSide.Left;
            }

            if (right)
            {
                side = AnchorSide.Right;
            }

            if (top)
            {
                side = AnchorSide.Top;
            }

            if (bottom)
            {
                side = AnchorSide.Bottom;
            }

            LayoutAnchorablePane? anchorablePane = manager.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == side);
            if (anchorablePane != null)
            {
                anchorablePane.Children.Add(this);
            }
            else
            {
                most = true;
            }
        }

        if (!most)
        {
            return;
        }

        if (manager.Layout.RootPanel == null)
        {
            manager.Layout.RootPanel = new LayoutPanel { Orientation = left || right ? Orientation.Horizontal : Orientation.Vertical };
        }

        if (left || right)
        {
            if (manager.Layout.RootPanel.Orientation == Orientation.Vertical && manager.Layout.RootPanel.ChildrenCount > 1)
            {
                manager.Layout.RootPanel = new LayoutPanel(manager.Layout.RootPanel);
            }

            manager.Layout.RootPanel.Orientation = Orientation.Horizontal;
            if (left)
            {
                manager.Layout.RootPanel.Children.Insert(0, new LayoutAnchorablePane(this));
            }
            else
            {
                manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(this));
            }
        }
        else
        {
            if (manager.Layout.RootPanel.Orientation == Orientation.Horizontal && manager.Layout.RootPanel.ChildrenCount > 1)
            {
                manager.Layout.RootPanel = new LayoutPanel(manager.Layout.RootPanel);
            }

            manager.Layout.RootPanel.Orientation = Orientation.Vertical;
            if (top)
            {
                manager.Layout.RootPanel.Children.Insert(0, new LayoutAnchorablePane(this));
            }
            else
            {
                manager.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(this));
            }
        }
    }

    /// <summary>
    /// Executes the toggle auto hide operation.
    /// </summary>
    public void ToggleAutoHide()
    {
        if (Root is not LayoutRoot root)
        {
            return;
        }

        if (Parent is LayoutAnchorGroup parentGroup)
        {
            if (parentGroup.Parent is not LayoutAnchorSide parentSide)
            {
                return;
            }

            LayoutAnchorablePane? previousContainer = ((ILayoutPreviousContainer)parentGroup).PreviousContainer as LayoutAnchorablePane;

            if (previousContainer == null)
            {
                AnchorSide side = parentSide.Side;
                previousContainer = new LayoutAnchorablePane
                {
                    DockMinWidth = AutoHideMinWidth,
                    DockMinHeight = AutoHideMinHeight
                };

                ILayoutEngine? engine = root.Manager?.LayoutEngine;
                if (engine != null)
                {
                    engine.InsertPane(root, previousContainer, side);
                }
                else
                {
                    // Fallback when no manager is available (e.g. in tests)
                    new DefaultLayoutEngine().InsertPane(root, previousContainer, side);
                }
            }
            else
            {
                // I'm about to remove parentGroup, redirect any content (ie hidden contents) that point to it
                // to previousContainer
                foreach (ILayoutPreviousContainer? cnt in root.Descendents().OfType<ILayoutPreviousContainer>().Where(c => ReferenceEquals(c.PreviousContainer, parentGroup)))
                {
                    cnt.PreviousContainer = previousContainer;
                }
            }

            int selectedIndex = -1;
            LayoutAnchorable? selectedItem = parentGroup.Children.FirstOrDefault(x => x.IsActive);
            if (selectedItem != null)
            {
                selectedIndex = parentGroup.Children.IndexOf(selectedItem);
            }

            foreach (LayoutAnchorable? anchorableToToggle in parentGroup.Children.ToArray())
            {
                previousContainer.Children.Add(anchorableToToggle);
            }

            if (selectedIndex != -1)
            {
                previousContainer.SelectedContentIndex = selectedIndex;
            }

            parentSide.Children.Remove(parentGroup);

            LayoutGroupBase? parent = previousContainer.Parent as LayoutGroupBase;
            while (parent != null)
            {
                if (parent is LayoutGroup<ILayoutPanelElement> layoutGroup)
                {
                    layoutGroup.ComputeVisibility();
                }

                parent = parent.Parent as LayoutGroupBase;
            }
        }
        else if (Parent is LayoutAnchorablePane parentPane)
        {
            LayoutAnchorGroup newAnchorGroup = new();
            ((ILayoutPreviousContainer)newAnchorGroup).PreviousContainer = parentPane;

            foreach (LayoutAnchorable? anchorableToImport in parentPane.Children.ToArray())
            {
                newAnchorGroup.Children.Add(anchorableToImport);
            }

            // detect anchor side for the pane
            AnchorSide anchorSide = parentPane.GetSide();

            switch (anchorSide)
            {
                case AnchorSide.Right:
                    root.RightSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Left:
                    root.LeftSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Top:
                    root.TopSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Bottom:
                    root.BottomSide?.Children.Add(newAnchorGroup);
                    break;
            }
        }
    }

    /// <summary>
    /// Executes the toggle single auto hide operation.
    /// </summary>
    public void ToggleSingleAutoHide()
    {
        if (Root is not LayoutRoot root)
        {
            return;
        }

        if (IsAutoHidden)
        {
            // Move from LayoutAnchorGroup back to a docked pane (same logic as ToggleAutoHide for single item)
            LayoutAnchorGroup? parentGroup = Parent as LayoutAnchorGroup;
            if (parentGroup == null)
            {
                return;
            }

            LayoutAnchorSide? parentSide = parentGroup.Parent as LayoutAnchorSide;
            if (parentSide == null)
            {
                return;
            }

            LayoutAnchorablePane? previousContainer = ((ILayoutPreviousContainer)parentGroup).PreviousContainer as LayoutAnchorablePane;

            // If previousContainer was removed from the tree (detached), treat as null
            if (previousContainer != null && previousContainer.Root == null)
            {
                previousContainer = null;
            }

            if (previousContainer == null)
            {
                AnchorSide side = parentSide.Side;
                previousContainer = new LayoutAnchorablePane
                {
                    DockMinWidth = AutoHideMinWidth,
                    DockMinHeight = AutoHideMinHeight
                };

                switch (side)
                {
                    case AnchorSide.Right:
                        if (root.RootPanel.Orientation == Orientation.Horizontal)
                        {
                            root.RootPanel.Children.Add(previousContainer);
                        }
                        else
                        {
                            LayoutPanel panel = new()
                            {
                                Orientation = Orientation.Horizontal
                            };
                            LayoutPanel oldRootPanel = root.RootPanel;
                            root.RootPanel = panel;
                            panel.Children.Add(oldRootPanel);
                            panel.Children.Add(previousContainer);
                        }

                        break;
                    case AnchorSide.Left:
                        if (root.RootPanel.Orientation == Orientation.Horizontal)
                        {
                            root.RootPanel.Children.Insert(0, previousContainer);
                        }
                        else
                        {
                            LayoutPanel panel = new()
                            {
                                Orientation = Orientation.Horizontal
                            };
                            LayoutPanel oldRootPanel = root.RootPanel;
                            root.RootPanel = panel;
                            panel.Children.Add(previousContainer);
                            panel.Children.Add(oldRootPanel);
                        }

                        break;
                    case AnchorSide.Top:
                        if (root.RootPanel.Orientation == Orientation.Vertical)
                        {
                            root.RootPanel.Children.Insert(0, previousContainer);
                        }
                        else
                        {
                            LayoutPanel panel = new()
                            {
                                Orientation = Orientation.Vertical
                            };
                            LayoutPanel oldRootPanel = root.RootPanel;
                            root.RootPanel = panel;
                            panel.Children.Add(previousContainer);
                            panel.Children.Add(oldRootPanel);
                        }

                        break;
                    case AnchorSide.Bottom:
                        if (root.RootPanel.Orientation == Orientation.Vertical)
                        {
                            root.RootPanel.Children.Add(previousContainer);
                        }
                        else
                        {
                            LayoutPanel panel = new()
                            {
                                Orientation = Orientation.Vertical
                            };
                            LayoutPanel oldRootPanel = root.RootPanel;
                            root.RootPanel = panel;
                            panel.Children.Add(oldRootPanel);
                            panel.Children.Add(previousContainer);
                        }

                        break;
                }
            }

            // Move only THIS anchorable (not siblings)
            parentGroup.Children.Remove(this);
            previousContainer.Children.Add(this);

            // Clean up empty group
            if (parentGroup.Children.Count == 0)
            {
                parentSide.Children.Remove(parentGroup);
            }
        }
        else if (Parent is LayoutAnchorablePane parentPane)
        {
            // Move from docked pane to auto-hide anchor group (only this one)
            AnchorSide anchorSide = parentPane.GetSide();
            LayoutAnchorGroup newAnchorGroup = new();
            ((ILayoutPreviousContainer)newAnchorGroup).PreviousContainer = parentPane;

            parentPane.Children.Remove(this);
            newAnchorGroup.Children.Add(this);

            switch (anchorSide)
            {
                case AnchorSide.Right:
                    root.RightSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Left:
                    root.LeftSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Top:
                    root.TopSide?.Children.Add(newAnchorGroup);
                    break;
                case AnchorSide.Bottom:
                    root.BottomSide?.Children.Add(newAnchorGroup);
                    break;
            }
        }
    }

    /// <summary>
    /// Executes the close anchorable operation.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    internal bool CloseAnchorable(bool closingChecked = false)
    {
        if (!closingChecked && !TestCanClose())
        {
            return false;
        }

        if (IsAutoHidden)
        {
            ToggleAutoHide();
        }

        CloseInternal();
        return true;
    }

    /// <summary>
    /// Executes the notify is visible changed operation.
    /// </summary>
    private void NotifyIsVisibleChanged() => IsVisibleChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Updates the parent visibility.
    /// </summary>
    private void UpdateParentVisibility()
    {
        // PreviousContainer is a docking destination, not current ownership. Restoring
        // Parent without inserting into Children leaves closed or removed tools in the
        // old Root and prevents active-content and layout-item cleanup.
        if ((Parent ?? PreviousContainer) is ILayoutElementWithVisibility parentPane)
        {
            parentPane.ComputeVisibility();
        }
    }
}
