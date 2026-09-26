// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / DockingManager.cs
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly List<LayoutFloatingWindowControl> floatingControls = [];
    private readonly Dictionary<IDockingWindowHost, LayoutFloatingWindowControl> controlsByHost = [];
    [System.ComponentModel.Bindable(false)]
    [System.ComponentModel.Description("Enumerates all LayoutFloatingWindowControls managed by this framework.")]
    [System.ComponentModel.Category("FloatingWindow")]
    public IEnumerable<LayoutFloatingWindowControl> FloatingWindows => floatingControls;
    public event EventHandler<LayoutFloatingWindowControlCreatedEventArgs>? LayoutFloatingWindowControlCreated;
    public event EventHandler<LayoutFloatingWindowControlClosedEventArgs>? LayoutFloatingWindowControlClosed;

    public LayoutFloatingWindowControl? CreateFloatingWindow(LayoutContent contentModel, bool isContentImmutable)
    {
        if (!AllowFloatingWindows)
        {
            return null;
        }

        if (contentModel is LayoutAnchorable anchorable)
        {
            if (!(contentModel.Parent is ILayoutPane))
            {
                ILayoutContainer? parent = anchorable.Parent;
                int index = (parent as ILayoutGroup)?.IndexOfChild(anchorable) ?? -1;
                LayoutRoot? oldRoot = anchorable.Root as LayoutRoot;
                int hiddenIndex = oldRoot?.Hidden.IndexOf(anchorable) ?? -1;
                ILayoutContainer? previousContainer = ((ILayoutPreviousContainer)anchorable).PreviousContainer;
                int previousIndex = anchorable.PreviousContainerIndex;
                try
                {

                    LayoutAnchorablePane pane = new(anchorable)
                    {
                        FloatingTop = contentModel.FloatingTop,
                        FloatingLeft = contentModel.FloatingLeft,
                        FloatingWidth = contentModel.FloatingWidth,
                        FloatingHeight = contentModel.FloatingHeight
                    };
                    return CreateFloatingWindowForLayoutAnchorableWithoutParent(pane, isContentImmutable);
                }
                catch
                {
                    anchorable.Parent?.RemoveChild(anchorable);
                    if (oldRoot != null && hiddenIndex >= 0)
                    {
                        oldRoot.Hidden.Insert(Math.Min(hiddenIndex, oldRoot.Hidden.Count), anchorable);
                    }
                    else if (parent is ILayoutGroup group)
                    {
                        group.InsertChildAt(Math.Clamp(index, 0, group.ChildrenCount), anchorable);
                    }

                    ((ILayoutPreviousContainer)anchorable).PreviousContainer = previousContainer;
                    anchorable.PreviousContainerIndex = previousIndex;
                    throw;
                }

            }
        }

        return CreateFloatingWindowCore(contentModel, isContentImmutable);
    }

    private LayoutFloatingWindowControl? CreateFloatingWindowForLayoutAnchorableWithoutParent(LayoutAnchorablePane paneModel, bool isContentImmutable)
    {
        if (!AllowFloatingWindows)
        {
            return null;
        }

        if (paneModel.Children.Any(c => !c.CanFloat))
        {
            return null;
        }

        LayoutAnchorable[] children = paneModel.Children.ToArray();
        (ILayoutContainer? PreviousContainer, int PreviousContainerIndex)[] previous = children.Select(child => (((ILayoutPreviousContainer)child).PreviousContainer, child.PreviousContainerIndex)).ToArray();
        int selectedIndex = paneModel.SelectedContentIndex;
        bool previousSync = synchronizingWindows;
        IDockingWindowHost? createdHost = null;
        LayoutFloatingWindow? fw = null;
        synchronizingWindows = true;
        try
        {

            ILayoutPositionableElement paneAsPositionableElement = paneModel;
            ILayoutPositionableElementWithActualSize paneAsWithActualSize = paneModel;

            double fwWidth = paneAsPositionableElement.FloatingWidth;
            double fwHeight = paneAsPositionableElement.FloatingHeight;
            double fwLeft = paneAsPositionableElement.FloatingLeft;
            double fwTop = paneAsPositionableElement.FloatingTop;

            if (fwWidth == 0.0)
            {
                fwWidth = paneAsWithActualSize.ActualWidth + 10;       // 10 includes BorderThickness and Margins inside LayoutAnchorableFloatingWindowControl.
            }

            if (fwHeight == 0.0)
            {
                fwHeight = paneAsWithActualSize.ActualHeight + 10;   // 10 includes BorderThickness and Margins inside LayoutAnchorableFloatingWindowControl.
            }

            LayoutAnchorablePane destPane = new()
            {
                DockWidth = paneAsPositionableElement.DockWidth,
                DockHeight = paneAsPositionableElement.DockHeight,
                DockMinHeight = paneAsPositionableElement.DockMinHeight,
                DockMinWidth = paneAsPositionableElement.DockMinWidth,
                FloatingLeft = paneAsPositionableElement.FloatingLeft,
                FloatingTop = paneAsPositionableElement.FloatingTop,
                FloatingWidth = paneAsPositionableElement.FloatingWidth,
                FloatingHeight = paneAsPositionableElement.FloatingHeight,
            };

            bool savePreviousContainer = paneModel.FindParent<LayoutFloatingWindow>() == null;
            int currentSelectedContentIndex = paneModel.SelectedContentIndex;
            while (paneModel.Children.Count > 0)
            {
                LayoutAnchorable contentModel = paneModel.Children[paneModel.Children.Count - 1];

                if (savePreviousContainer)
                {
                    ((ILayoutPreviousContainer)contentModel).PreviousContainer = paneModel;
                    contentModel.PreviousContainerIndex = paneModel.Children.Count - 1;
                }

                paneModel.RemoveChildAt(paneModel.Children.Count - 1);
                destPane.Children.Insert(0, contentModel);
            }

            if (destPane.Children.Count > 0)
            {
                destPane.SelectedContentIndex = currentSelectedContentIndex;
            }

            LayoutFloatingWindowControl fwc;
            fw = new LayoutAnchorableFloatingWindow
            {
                RootPanel = new LayoutAnchorablePaneGroup(destPane)
                {
                    DockHeight = destPane.DockHeight,
                    DockWidth = destPane.DockWidth,
                    DockMinHeight = destPane.DockMinHeight,
                    DockMinWidth = destPane.DockMinWidth,
                }
            };

            Layout.FloatingWindows.Add(fw);

            createdHost = CreatePublicWindowHost(fw, destPane.SelectedContent?.Title, new Rect(fwLeft, fwTop, fwWidth, fwHeight), isContentImmutable);
            fwc = controlsByHost[createdHost];
            // fwc.Owner = Window.GetWindow(this);
            // fwc.SetParentToMainWindowOf(this);
            RegisterFloatingHost(fw, createdHost);
            Layout.CollectGarbage();
            InvalidateArrange();
            return fwc;
        }
        catch
        {
            if (createdHost != null)
            {
                createdHost.Dispose();
            }

            if (fw != null)
            {
                floatingHosts.Remove(fw);
                Layout.FloatingWindows.Remove(fw);
            }
            for (int index = 0; index < children.Length; index++)
            {
                if (!ReferenceEquals(children[index].Parent, paneModel))
                {
                    paneModel.Children.Insert(Math.Min(index, paneModel.ChildrenCount), children[index]);
                }

                ((ILayoutPreviousContainer)children[index]).PreviousContainer = previous[index].PreviousContainer;
                children[index].PreviousContainerIndex = previous[index].PreviousContainerIndex;
            }
            paneModel.SelectedContentIndex = selectedIndex;
            throw;
        }
        finally { synchronizingWindows = previousSync; }

    }

    private LayoutFloatingWindowControl? CreateFloatingWindowCore(LayoutContent contentModel, bool isContentImmutable)
    {
        if (!AllowFloatingWindows)
        {
            return null;
        }

        if (!contentModel.CanFloat)
        {
            return null;
        }

        if (contentModel is LayoutAnchorable contentModelAsAnchorable && contentModelAsAnchorable.IsAutoHidden)
        {
            contentModelAsAnchorable.ToggleAutoHide();
        }

        if (contentModel.Parent is not ILayoutPane parentPane
            || parentPane is not ILayoutPositionableElement parentPaneAsPositionableElement
            || parentPane is not ILayoutPositionableElementWithActualSize parentPaneAsWithActualSize)
        {
            return null;
        }
        int contentModelParentChildrenIndex = parentPane.Children.ToList().IndexOf(contentModel);

        ILayoutContainer? previousContainer = ((ILayoutPreviousContainer)contentModel).PreviousContainer;
        int previousIndex = contentModel.PreviousContainerIndex;
        LayoutContent? selected = (parentPane as ILayoutContentSelector)?.SelectedContent;
        LayoutContent? active = Layout.ActiveContent;
        bool previousSync = synchronizingWindows;
        IDockingWindowHost? createdHost = null;
        LayoutFloatingWindow? fw = null;
        synchronizingWindows = true;
        try
        {

            if (contentModel.FindParent<LayoutFloatingWindow>() == null)
            {
                ((ILayoutPreviousContainer)contentModel).PreviousContainer = parentPane;
                contentModel.PreviousContainerIndex = contentModelParentChildrenIndex;
            }

            parentPane.RemoveChildAt(contentModelParentChildrenIndex);

            double fwWidth = contentModel.FloatingWidth;
            double fwHeight = contentModel.FloatingHeight;

            if (fwWidth == 0.0)
            {
                fwWidth = parentPaneAsPositionableElement.FloatingWidth;
            }

            if (fwHeight == 0.0)
            {
                fwHeight = parentPaneAsPositionableElement.FloatingHeight;
            }

            if (fwWidth == 0.0)
            {
                fwWidth = parentPaneAsWithActualSize.ActualWidth + 10;      // 10 includes BorderThickness and Margins inside LayoutDocumentFloatingWindowControl.
            }

            if (fwHeight == 0.0)
            {
                fwHeight = parentPaneAsWithActualSize.ActualHeight + 10;    // 10 includes BorderThickness and Margins inside LayoutDocumentFloatingWindowControl.
            }

            LayoutFloatingWindowControl fwc;
            if (contentModel is LayoutAnchorable anchorableContent)
            {

                fw = new LayoutAnchorableFloatingWindow
                {
                    RootPanel = new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(anchorableContent)
                    {
                        DockWidth = parentPaneAsPositionableElement.DockWidth,
                        DockHeight = parentPaneAsPositionableElement.DockHeight,
                        DockMinHeight = parentPaneAsPositionableElement.DockMinHeight,
                        DockMinWidth = parentPaneAsPositionableElement.DockMinWidth,
                        FloatingLeft = parentPaneAsPositionableElement.FloatingLeft,
                        FloatingTop = parentPaneAsPositionableElement.FloatingTop,
                        FloatingWidth = parentPaneAsPositionableElement.FloatingWidth,
                        FloatingHeight = parentPaneAsPositionableElement.FloatingHeight,
                    })
                };

                Layout.FloatingWindows.Add(fw);
                createdHost = CreatePublicWindowHost(fw, contentModel.Title, new Rect(contentModel.FloatingLeft, contentModel.FloatingTop, fwWidth, fwHeight), isContentImmutable);
                fwc = controlsByHost[createdHost];
            }
            else
            {
                LayoutContent anchorableDocument = contentModel;
                fw = new LayoutDocumentFloatingWindow
                {
                    RootPanel = new LayoutDocumentPaneGroup(new LayoutDocumentPane(anchorableDocument)
                    {
                        DockWidth = parentPaneAsPositionableElement.DockWidth,
                        DockHeight = parentPaneAsPositionableElement.DockHeight,
                        DockMinHeight = parentPaneAsPositionableElement.DockMinHeight,
                        DockMinWidth = parentPaneAsPositionableElement.DockMinWidth,
                        FloatingLeft = parentPaneAsPositionableElement.FloatingLeft,
                        FloatingTop = parentPaneAsPositionableElement.FloatingTop,
                        FloatingWidth = parentPaneAsPositionableElement.FloatingWidth,
                        FloatingHeight = parentPaneAsPositionableElement.FloatingHeight,
                    })
                };

                Layout.FloatingWindows.Add(fw);
                createdHost = CreatePublicWindowHost(fw, contentModel.Title, new Rect(contentModel.FloatingLeft, contentModel.FloatingTop, fwWidth, fwHeight), isContentImmutable);
                fwc = controlsByHost[createdHost];
            }

            // fwc.Owner = Window.GetWindow(this);
            // fwc.SetParentToMainWindowOf(this);
            RegisterFloatingHost(fw, createdHost);
            Layout.CollectGarbage();
            UpdateLayout();
            return fwc;
        }
        catch
        {
            if (createdHost != null)
            {
                createdHost.Dispose();
            }

            if (fw != null)
            {
                floatingHosts.Remove(fw);
                Layout.FloatingWindows.Remove(fw);
            }
            if (!ReferenceEquals(contentModel.Parent, parentPane))
            {
                ((ILayoutGroup)parentPane).InsertChildAt(Math.Clamp(contentModelParentChildrenIndex, 0, parentPane.ChildrenCount), contentModel);
            }

            ((ILayoutPreviousContainer)contentModel).PreviousContainer = previousContainer;
            contentModel.PreviousContainerIndex = previousIndex;
            if (selected?.Root?.Manager == this)
            {
                selected.IsSelected = true;
            }

            if (active?.Root?.Manager == this)
            {
                active.IsActive = true;
            }

            throw;
        }
        finally { synchronizingWindows = previousSync; }

    }

    private IDockingWindowHost CreatePublicWindowHost(LayoutFloatingWindow model, string? title, Rect bounds, bool immutable = false,
        WindowPlacement placement = WindowPlacement.ConstrainToWorkArea)
    {
        LayoutFloatingWindowControl control = model switch
        {
            LayoutDocumentFloatingWindow document => new LayoutDocumentFloatingWindowControl(document, immutable),
            LayoutAnchorableFloatingWindow tool => new LayoutAnchorableFloatingWindowControl(tool, immutable),
            _ => throw new ArgumentException("Unsupported floating layout model.", nameof(model))
        };
        UIElement? visual = null;
        IDockingWindowHost? host = null;
        try
        {
            visual = model.Children.FirstOrDefault() is { } root ? CreateUIElementForModel(root) : null;
            host = PlatformServices.CreateWindowHostService(this).Attach(control, visual, title ?? string.Empty, bounds, true, placement);
            control.AttachHost(host);
            floatingControls.Add(control);
            controlsByHost.Add(host, control);
            host.Closed += (_, _) => ReleaseFloatingControl(host);
            return host;
        }
        catch
        {
            if (host != null)
            {
                host.Dispose();
            }
            else
            {
                ((Window)control).Content = null;
                LayoutViewBuilder.Release(visual);
                ((Window)control).Close();
            }
            throw;
        }
    }

    private void RegisterFloatingHost(LayoutFloatingWindow model, IDockingWindowHost host)
    {
        floatingHosts.Add(model, host);
        AttachFloatingHost(model, host);
    }

    internal void RemoveFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        IDockingWindowHost? host = floatingWindow.WindowHost;
        if (host == null)
        {
            return;
        }

        if (!controlsByHost.Remove(host, out LayoutFloatingWindowControl? control))
        {
            return;
        }

        floatingControls.Remove(control);
        LayoutFloatingWindowControlClosed?.Invoke(this, new LayoutFloatingWindowControlClosedEventArgs(control));
    }

    private void ReleaseFloatingControl(IDockingWindowHost host)
    {
        // The derived OnClosed normally removes the control first. Native disposal can
        // also arrive after its model has lost the root that owns that source hook.
        if (!controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
        {
            return;
        }

        RemoveFloatingWindow(control);
        control.Model?.Root?.CollectGarbage();
    }

    private void NotifyFloatingControlCreated(IDockingWindowHost host)
    {
        if (controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
        {
            LayoutFloatingWindowControlCreated?.Invoke(this, new LayoutFloatingWindowControlCreatedEventArgs(control));
        }
    }

    public void DockAllFloatingWindows()
    {
        LayoutRoot layout = Layout;
        if (layout == null)
        {
            return;
        }

        // Materialised before anything moves: docking mutates both the floating window collection and
        // the trees hanging off it.
        LayoutContent[] floatingContents = layout.FloatingWindows
            .SelectMany(fw => fw.Descendents().OfType<LayoutContent>())
            .ToArray();

        foreach (LayoutContent? content in floatingContents)
        {
            // Docking one piece of content can carry others with it, so only what is still floating
            // is worth moving - and only that still has a Root for Dock to work with.
            if (!ReferenceEquals(content.Root, layout))
            {
                continue;
            }

            if (content.FindParent<LayoutFloatingWindow>() == null)
            {
                continue;
            }

            if (HasDockablePreviousContainer(content))
            {
                content.Dock();
            }
            else
            {
                DockOutsideFloatingWindows(content);
            }
        }

        layout.CollectGarbage();
        CloseEmptiedFloatingWindows(layout);

        // Runs again because closing the windows disconnects the panes that content may still name as
        // the one to float back into, and those references have to go with them.
        layout.CollectGarbage();
    }

    private void DockOutsideFloatingWindows(LayoutContent content)
    {
        LayoutRoot layout = Layout;
        if (layout == null)
        {
            return;
        }

        bool wasFloating = content.IsFloating;

        if (content is LayoutAnchorable anchorable)
        {
            LayoutAnchorablePane? anchorablePane = layout.Descendents().OfType<LayoutAnchorablePane>()
                .FirstOrDefault(pane => pane.FindParent<LayoutFloatingWindow>() == null);

            if (anchorablePane == null)
            {
                anchorablePane = new LayoutAnchorablePane { DockWidth = new GridLength(200.0, GridUnitType.Pixel) };
                EnsureRootPanel(layout).Children.Add(anchorablePane);
            }

            content.Parent?.RemoveChild(content);
            anchorablePane.Children.Add(anchorable);
        }
        else
        {
            LayoutDocumentPane? documentPane = layout.Descendents().OfType<LayoutDocumentPane>()
                .FirstOrDefault(pane => pane.FindParent<LayoutFloatingWindow>() == null);

            if (documentPane == null)
            {
                documentPane = new LayoutDocumentPane();
                EnsureRootPanel(layout).Children.Add(new LayoutDocumentPaneGroup(documentPane));
            }

            content.Parent?.RemoveChild(content);
            documentPane.Children.Add(content);
        }

        // The pane it came from is inside a window that is about to close, so it is no place to
        // float back into.
        ((ILayoutPreviousContainer)content).PreviousContainer = null;
        content.PreviousContainerIndex = -1;
        content.IsSelected = true;

        // Docking through LayoutContent.Dock raises this, and callers must not be able to tell which
        // of the two routes a piece of content took.
        if (wasFloating && !content.IsFloating)
        {
            RaiseContentDocked(content);
        }
    }

    private static LayoutPanel EnsureRootPanel(LayoutRoot layout)
    {
        if (layout.RootPanel != null)
        {
            return layout.RootPanel;
        }

        LayoutPanel panel = new()
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal
        };
        layout.RootPanel = panel;
        return panel;
    }

    private void CloseEmptiedFloatingWindows(LayoutRoot layout)
    {
        foreach (LayoutFloatingWindowControl fwc in floatingControls.ToArray())
        {
            if (fwc.Model == null || fwc.Model.Descendents().OfType<LayoutContent>().Any())
            {
                continue;
            }

            fwc.InternalClose();
        }

        // Models without a control of their own - a layout can be loaded with floating windows while
        // AllowFloatingWindows is off, in which case no control was ever created for them.
        foreach (LayoutFloatingWindow? fw in layout.FloatingWindows.ToArray())
        {
            if (fw.Descendents().OfType<LayoutContent>().Any())
            {
                continue;
            }

            layout.FloatingWindows.Remove(fw);
        }
    }

    private static bool HasDockablePreviousContainer(LayoutContent content) =>
        ((ILayoutPreviousContainer)content).PreviousContainer is ILayoutElement previous
        && ReferenceEquals(previous.Root, content.Root)
        && previous.FindParent<LayoutFloatingWindow>() == null;
}
