// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutGridControl.cs
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>Renders an upstream layout group and commits splitter sizes to its model.</summary>
public abstract class LayoutGridControl<T> : Grid, ILayoutControl, IAdjustableSizeLayout
    where T : class, ILayoutPanelElement
{
    private readonly LayoutPositionableGroup<T> model;
    private readonly List<ChildView> childViews = [];
    private readonly List<LayoutGridResizerControl> splitters = [];
    private OverlayWindow? resizePreviewWindow;
    private readonly ReentrantFlag fixingLengths = new();
    private readonly List<(DependencyProperty Property, long Token)> managerCallbacks = [];
    private DockingManager? manager;
    private bool attached;
    private bool released;
    private bool initialized;
    private bool layoutInitialized;
    private bool updatingChildren;
    private ChildrenTreeChange? asyncRefreshCalled;
    private bool AsyncRefreshCalled => asyncRefreshCalled != null;
    private bool updatingDefinitions;
    private bool resizingFixedChildren;
    private LayoutGridResizerControl? activeSplitter;
    private ResizeState? resize;
    private IDisposable? dropRegistration;

    internal LayoutGridControl(LayoutPositionableGroup<T> model, Orientation orientation)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        FlowDirection = FlowDirection.LeftToRight;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public ILayoutElement Model => model;
    public Orientation Orientation => ((ILayoutOrientableGroup)model).Orientation;

    /// <summary>Runs when the control first enters its WinUI host.</summary>
    /// <remarks>WinUI has no FrameworkElement.OnInitialized override; the same hook is invoked from Loaded.</remarks>
    protected virtual void OnInitialized(EventArgs e)
    {
        Attach();
    }

    protected void FixChildrenDockLengths()
    {
        using (fixingLengths.Enter())
        {
            OnFixChildrenDockLengths();
        }
    }

    protected abstract void OnFixChildrenDockLengths();

    internal void InitializeView() => Attach();

    internal void ReleaseView()
    {
        if (released)
        {
            return;
        }

        released = true;
        Detach();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (!initialized)
        {
            initialized = true;
            OnInitialized(EventArgs.Empty);
        }
        else
        {
            Attach();
        }

        UpdateRowColDefinitions();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Reattaching the same view to a new XAML island can deliver the former
        // island's Unloaded after the current Loaded. Keep the live attachment.
        if (IsLoaded)
        {
            return;
        }

        Detach();
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !released && !attached) { Attach(); UpdateRowColDefinitions(); } });
    }

    private void Attach()
    {
        if (attached || released)
        {
            return;
        }

        attached = true;
        model.ChildrenTreeChanged += OnChildrenTreeChanged;
        model.PropertyChanged += OnModelChanged;
        manager = model.Root?.Manager;
        if (manager != null)
        {
            if (model is LayoutDocumentPaneGroup)
            {
                dropRegistration = manager.RegisterDockTarget(model, this);
            }

            foreach (DependencyProperty? property in new[]
            {
                DockingManager.GridSplitterWidthProperty, DockingManager.GridSplitterHeightProperty,
                DockingManager.GridSplitterVerticalStyleProperty, DockingManager.GridSplitterHorizontalStyleProperty
            })
            {
                managerCallbacks.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => UpdateRowColDefinitions())));
            }
        }
        UpdateChildren();
    }

    private void Detach()
    {
        EndResize(false);
        if (!attached)
        {
            return;
        }

        attached = false;
        model.ChildrenTreeChanged -= OnChildrenTreeChanged;
        model.PropertyChanged -= OnModelChanged;
        if (manager != null)
        {
            foreach ((DependencyProperty Property, long Token) callback in managerCallbacks)
            {
                manager.UnregisterPropertyChangedCallback(callback.Property, callback.Token);
            }
        }
        managerCallbacks.Clear();
        dropRegistration?.Dispose();
        dropRegistration = null;
        manager = null;
        ClearSplitters();
        foreach (ChildView child in childViews)
        {
            child.Model.PropertyChanged -= OnChildModelPropertyChanged;
            ReleaseChild(child.View);
        }
        childViews.Clear();
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();
    }

    private static void ReleaseChild(UIElement view)
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
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private void OnChildrenTreeChanged(object? sender, ChildrenTreeChangedEventArgs args)
    {
        if (args.Change != ChildrenTreeChange.DirectChildrenChanged)
        {
            return;
        }

        if (asyncRefreshCalled.HasValue && asyncRefreshCalled.Value == args.Change)
        {
            return;
        }

        asyncRefreshCalled = args.Change;
        if (resize != null)
        {
            EndResize(false);
        }

        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            asyncRefreshCalled = null;
            // A native root may be released while its original coalesced refresh is queued.
            if (attached && !released)
            {
                UpdateChildren();
            }
        }))
        {
            asyncRefreshCalled = null;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ILayoutOrientableGroup.Orientation))
        {
            EndResize(false);
            UpdateRowColDefinitions();
        }
    }

    private void OnChildModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (AsyncRefreshCalled || !attached || updatingDefinitions || updatingChildren)
        {
            return;
        }

        if (resize != null && e.PropertyName == nameof(ILayoutPositionableElement.IsVisible))
        {
            EndResize(false);
        }

        List<FrameworkElement> layoutChildren = GetLayoutChildren();
        if (fixingLengths.CanEnter && e.PropertyName == nameof(ILayoutPositionableElement.DockWidth) && Orientation == Orientation.Horizontal)
        {
            if (ColumnDefinitions.Count != layoutChildren.Count)
            {
                return;
            }

            if (sender is not ILayoutPositionableElement changedElement)
            {
                return;
            }
            int indexOfChild = layoutChildren.FindIndex(child => child is ILayoutControl control && ReferenceEquals(control.Model, changedElement));
            if (indexOfChild < 0)
            {
                return;
            }
            ColumnDefinitions[indexOfChild].Width = changedElement.DockWidth;
        }
        else if (fixingLengths.CanEnter && e.PropertyName == nameof(ILayoutPositionableElement.DockHeight) && Orientation == Orientation.Vertical)
        {
            if (RowDefinitions.Count != layoutChildren.Count)
            {
                return;
            }

            if (sender is not ILayoutPositionableElement changedElement)
            {
                return;
            }
            int indexOfChild = layoutChildren.FindIndex(child => child is ILayoutControl control && ReferenceEquals(control.Model, changedElement));
            if (indexOfChild < 0)
            {
                return;
            }
            RowDefinitions[indexOfChild].Height = changedElement.DockHeight;
        }
        else if (e.PropertyName == nameof(ILayoutPositionableElement.IsVisible))
        {
            UpdateRowColDefinitions();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ILayoutPositionableElementWithActualSize modelWithAtcualSize = (ILayoutPositionableElementWithActualSize)model;
        modelWithAtcualSize.ActualWidth = ActualWidth;
        modelWithAtcualSize.ActualHeight = ActualHeight;
        if (!layoutInitialized)
        {
            layoutInitialized = true;
            UpdateChildren();
        }

        AdjustFixedChildrenPanelSizes();
    }

    private void UpdateChildren()
    {
        if (updatingChildren)
        {
            return;
        }

        updatingChildren = true;
        try
        {

            EndResize(false);
            ClearSplitters();
            ChildView[] old = childViews.ToArray();
            childViews.Clear();
            foreach (T child in model.Children)
            {
                ChildView? retained = old.FirstOrDefault(entry => ReferenceEquals(entry.Model, child));
                FrameworkElement? view = retained?.View ?? manager?.CreateUIElementForModel(child) as FrameworkElement;
                if (view == null)
                {
                    continue;
                }

                if (retained == null)
                {
                    child.PropertyChanged += OnChildModelPropertyChanged;
                    Children.Add(view);
                }
                childViews.Add(retained ?? new ChildView(child, view));
            }
            foreach (ChildView? removed in old.Where(entry => !childViews.Contains(entry)))
            {
                removed.Model.PropertyChanged -= OnChildModelPropertyChanged;
                Children.Remove(removed.View);
                ReleaseChild(removed.View);
            }
            for (int index = 0; index + 1 < childViews.Count; index++)
            {
                LayoutGridResizerControl splitter = new();
                splitter.DragStarted += OnSplitterDragStarted;
                splitter.DragDelta += OnSplitterDragDelta;
                splitter.DragCompleted += OnSplitterDragCompleted;
                splitters.Add(splitter);
                Children.Add(splitter);
            }
            UpdateRowColDefinitions();

        }
        finally { updatingChildren = false; }
    }

    private void ClearSplitters()
    {
        foreach (LayoutGridResizerControl splitter in splitters)
        {
            splitter.DragStarted -= OnSplitterDragStarted;
            splitter.DragDelta -= OnSplitterDragDelta;
            splitter.DragCompleted -= OnSplitterDragCompleted;
            splitter.CancelDrag();
            Children.Remove(splitter);
        }
        splitters.Clear();
    }

    private void UpdateRowColDefinitions()
    {
        if (!attached || updatingDefinitions)
        {
            return;
        }

        updatingDefinitions = true;
        try
        {
            List<FrameworkElement> layoutChildren = GetLayoutChildren();

            ILayoutRoot? root = model.Root;
            DockingManager? manager = root?.Manager;
            if (manager == null)
            {
                return;
            }

            FixChildrenDockLengths();
            // Debug.Assert(layoutChildren.Count == model.ChildrenCount + (model.ChildrenCount - 1));
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            if (Orientation == Orientation.Horizontal)
            {
                int iColumn = 0;
                int iChild = 0;
                // BD: 24.08.2020 added check for iChild against layoutChildren.Count
                for (int iChildModel = 0; iChildModel < model.Children.Count && iChild < layoutChildren.Count; iChildModel++, iColumn++, iChild++)
                {
                    ILayoutPositionableElement childModel = (ILayoutPositionableElement)model.Children[iChildModel];
                    ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = childModel.IsVisible ? childModel.DockWidth : new GridLength(0.0, GridUnitType.Pixel),
                        MinWidth = childModel.IsVisible ? childModel.CalculatedDockMinWidth() : 0.0
                    });
                    Grid.SetColumn(layoutChildren[iChild], iColumn);

                    // append column for splitter
                    if (iChild >= layoutChildren.Count - 1)
                    {
                        continue;
                    }

                    iChild++;
                    iColumn++;

                    bool nextChildModelVisibleExist = false;
                    for (int i = iChildModel + 1; i < model.Children.Count; i++)
                    {
                        ILayoutPositionableElement nextChildModel = (ILayoutPositionableElement)model.Children[i];
                        if (!nextChildModel.IsVisible)
                        {
                            continue;
                        }

                        nextChildModelVisibleExist = true;
                        break;
                    }

                    ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = childModel.IsVisible && nextChildModelVisibleExist ? new GridLength(manager.GridSplitterWidth) : new GridLength(0.0, GridUnitType.Pixel)
                    });
                    Grid.SetColumn(layoutChildren[iChild], iColumn);
                }
            }
            else // if (model.Orientation == Orientation.Vertical)
            {
                int iRow = 0;
                int iChild = 0;
                // BD: 24.08.2020 added check for iChild against layoutChildren.Count
                for (int iChildModel = 0; iChildModel < model.Children.Count && iChild < layoutChildren.Count; iChildModel++, iRow++, iChild++)
                {
                    ILayoutPositionableElement childModel = (ILayoutPositionableElement)model.Children[iChildModel];
                    RowDefinitions.Add(new RowDefinition
                    {
                        Height = childModel.IsVisible ? childModel.DockHeight : new GridLength(0.0, GridUnitType.Pixel),
                        MinHeight = childModel.IsVisible ? childModel.CalculatedDockMinHeight() : 0.0
                    });
                    Grid.SetRow(layoutChildren[iChild], iRow);

                    // if (RowDefinitions.Last().Height.Value == 0.0)
                    //    System.Diagnostics.Debugger.Break();

                    // append row for splitter (if necessary)
                    if (iChild >= layoutChildren.Count - 1)
                    {
                        continue;
                    }

                    iChild++;
                    iRow++;

                    bool nextChildModelVisibleExist = false;
                    for (int i = iChildModel + 1; i < model.Children.Count; i++)
                    {
                        ILayoutPositionableElement nextChildModel = (ILayoutPositionableElement)model.Children[i];
                        if (!nextChildModel.IsVisible)
                        {
                            continue;
                        }

                        nextChildModelVisibleExist = true;
                        break;
                    }

                    RowDefinitions.Add(new RowDefinition
                    {
                        Height = childModel.IsVisible && nextChildModelVisibleExist ? new GridLength(manager.GridSplitterHeight) : new GridLength(0.0, GridUnitType.Pixel)
                    });
                    // if (RowDefinitions.Last().Height.Value == 0.0)
                    //    System.Diagnostics.Debugger.Break();
                    Grid.SetRow(layoutChildren[iChild], iRow);
                }
            }

            // Native retained views keep stable parents. Reset the unused axis and
            // mirror zero-sized definitions to input/visibility; splitter cursor and
            // style live on the existing native resizer rather than WPF Thumb.
            for (int index = 0; index < layoutChildren.Count; index++)
            {
                FrameworkElement child = layoutChildren[index];
                if (Orientation == Orientation.Horizontal)
                {
                    SetRow(child, 0);
                }
                else
                {
                    SetColumn(child, 0);
                }

                child.Visibility = IsChildVisible(index) ? Visibility.Visible : Visibility.Collapsed;
                if (child is LayoutGridResizerControl splitter)
                {
                    splitter.SetResizeOrientation(Orientation);
                    splitter.Style = Orientation == Orientation.Horizontal ? manager.GridSplitterVerticalStyle : manager.GridSplitterHorizontalStyle;
                }
            }
        }
        finally { updatingDefinitions = false; }
    }

    private List<FrameworkElement> GetLayoutChildren()
    {
        // WPF interleaves child controls and splitters. Removing/reinserting surviving
        // WinUI visuals unloads their editors, so expose the original order as a snapshot.
        List<FrameworkElement> result = new(childViews.Count + splitters.Count);
        for (int index = 0; index < childViews.Count; index++)
        {
            result.Add(childViews[index].View);
            if (index < splitters.Count)
            {
                result.Add(splitters[index]);
            }
        }
        return result;
    }

    private void OnSplitterDragStarted(object? sender, DragStartedEventArgs e)
    {
        if (sender is not LayoutGridResizerControl splitter)
        {
            return;
        }

        if (resize != null)
        {
            splitter.CancelDrag();
            return;
        }
        int index = splitters.IndexOf(splitter);
        if (index < 0)
        {
            splitter.CancelDrag();
            return;
        }
        FrameworkElement left = childViews[index].View;
        FrameworkElement? right = GetNextVisibleChild(index * 2 + 1);
        if (left is not ILayoutControl { Model: ILayoutPositionableElement first }
            || right is not ILayoutControl { Model: ILayoutPositionableElement second })
        {
            splitter.CancelDrag();
            return;
        }
        bool horizontal = Orientation == Orientation.Horizontal;
        if (!PlatformServices.Coordinates.TryGetScreenBounds(left, out Rect firstBounds)
            || !PlatformServices.Coordinates.TryGetScreenBounds(right, out Rect secondBounds)
            || !PlatformServices.Coordinates.TryGetScreenBounds(splitter, out Rect splitterBounds))
        {
            splitter.CancelDrag();
            return;
        }
        double scale = XamlRoot.RasterizationScale;
        // Original TransformActualSizeToAncestor supplies sizes in the window's logical
        // coordinate space, including Viewbox/ancestor transforms but excluding DPI.
        Size firstSize = new(firstBounds.Width / scale, firstBounds.Height / scale);
        Size secondSize = new(secondBounds.Width / scale, secondBounds.Height / scale);
        Rect previewBounds = new(splitterBounds.X, splitterBounds.Y,
            horizontal ? splitter.ActualWidth * scale : firstBounds.Width,
            horizontal ? secondBounds.Height : splitter.ActualHeight * scale);
        activeSplitter = splitter;
        resize = new ResizeState(first, second, firstSize, secondSize, splitter.DragStartPosition,
            scale, horizontal, previewBounds, splitter.BackgroundWhileDragging, splitter.OpacityWhileDragging);
        try
        {
            resizePreviewWindow = new OverlayWindow(this, model.FindParent<LayoutFloatingWindow>() != null);
            resizePreviewWindow.ShowResizePreview(previewBounds, resize.Fill, resize.Opacity);
        }
        catch { EndResize(false); throw; }
    }

    private void OnSplitterDragDelta(object? sender, DragDeltaEventArgs e)
    {
        if (activeSplitter is { } splitter && ReferenceEquals(sender, splitter))
        {
            UpdateResize(splitter.DragCurrentPosition, false);
        }
    }

    private void OnSplitterDragCompleted(object? sender, DragCompletedEventArgs e)
    {
        if (activeSplitter is not { } splitter || !ReferenceEquals(sender, splitter))
        {
            return;
        }

        if (e.Canceled)
        {
            EndResize(false);
        }
        else
        {
            UpdateResize(splitter.DragCurrentPosition, true);
        }
    }

    private void UpdateResize(DragInputPosition position, bool released)
    {
        if (resize == null)
        {
            return;
        }

        if (!ReferenceEquals(resize.First.Parent, model) || !ReferenceEquals(resize.Second.Parent, model))
        {
            EndResize(false);
            return;
        }
        double distance = resize.Horizontal ? position.X - resize.Start.X : position.Y - resize.Start.Y;
        double minimumFirst = resize.Horizontal ? resize.First.CalculatedDockMinWidth() : resize.First.CalculatedDockMinHeight();
        double minimumSecond = resize.Horizontal ? resize.Second.CalculatedDockMinWidth() : resize.Second.CalculatedDockMinHeight();
        double firstExtent = resize.Horizontal ? resize.FirstSize.Width : resize.FirstSize.Height;
        double secondExtent = resize.Horizontal ? resize.SecondSize.Width : resize.SecondSize.Height;
        resize.Delta = MathHelper.MinMax(distance / resize.Scale,
            minimumFirst - firstExtent, secondExtent - minimumSecond);
        Rect bounds = resize.PreviewBounds;
        bounds.X += resize.Horizontal ? resize.Delta * resize.Scale : 0;
        bounds.Y += resize.Horizontal ? 0 : resize.Delta * resize.Scale;
        if (!released)
        {
            resizePreviewWindow?.ShowResizePreview(bounds, resize.Fill, resize.Opacity);
        }

        if (released)
        {
            EndResize(true);
        }
    }

    private void EndResize(bool commit)
    {
        LayoutGridResizerControl? splitter = activeSplitter;
        ResizeState? state = resize;
        activeSplitter = null;
        resize = null;
        try
        {
            if (!commit || state == null)
            {
                return;
            }

            ILayoutPositionableElement prevChildModel = state.First;
            ILayoutPositionableElement nextChildModel = state.Second;
            Size prevChildActualSize = state.FirstSize;
            Size nextChildActualSize = state.SecondSize;
            double delta = state.Delta;
            if (state.Horizontal)
            {
                if (prevChildModel.DockWidth.IsStar)
                {
                    prevChildModel.DockWidth = new GridLength(prevChildModel.DockWidth.Value * (prevChildActualSize.Width + delta) / prevChildActualSize.Width, GridUnitType.Star);
                }
                else
                {
                    double width = prevChildModel.DockWidth.IsAuto ? prevChildActualSize.Width : prevChildModel.DockWidth.Value;
                    double resizedWidth = width + delta;
                    prevChildModel.DockWidth = new GridLength(double.IsNaN(resizedWidth) ? width : resizedWidth, GridUnitType.Pixel);
                }

                if (nextChildModel.DockWidth.IsStar)
                {
                    nextChildModel.DockWidth = new GridLength(nextChildModel.DockWidth.Value * (nextChildActualSize.Width - delta) / nextChildActualSize.Width, GridUnitType.Star);
                }
                else
                {
                    double width = nextChildModel.DockWidth.IsAuto ? nextChildActualSize.Width : nextChildModel.DockWidth.Value;
                    double resizedWidth = width - delta;
                    nextChildModel.DockWidth = new GridLength(double.IsNaN(resizedWidth) ? width : resizedWidth, GridUnitType.Pixel);
                }
            }
            else
            {
                if (prevChildModel.DockHeight.IsStar)
                {
                    prevChildModel.DockHeight = new GridLength(prevChildModel.DockHeight.Value * (prevChildActualSize.Height + delta) / prevChildActualSize.Height, GridUnitType.Star);
                }
                else
                {
                    double height = prevChildModel.DockHeight.IsAuto ? prevChildActualSize.Height : prevChildModel.DockHeight.Value;
                    double resizedHeight = height + delta;
                    prevChildModel.DockHeight = new GridLength(double.IsNaN(resizedHeight) ? height : resizedHeight, GridUnitType.Pixel);
                }

                if (nextChildModel.DockHeight.IsStar)
                {
                    nextChildModel.DockHeight = new GridLength(nextChildModel.DockHeight.Value * (nextChildActualSize.Height - delta) / nextChildActualSize.Height, GridUnitType.Star);
                }
                else
                {
                    double height = nextChildModel.DockHeight.IsAuto ? nextChildActualSize.Height : nextChildModel.DockHeight.Value;
                    double resizedHeight = height - delta;
                    nextChildModel.DockHeight = new GridLength(double.IsNaN(resizedHeight) ? height : resizedHeight, GridUnitType.Pixel);
                }
            }

        }
        finally
        {
            OverlayWindow? window = resizePreviewWindow;
            resizePreviewWindow = null;
            try
            {
                window?.CloseHost();
            }
            finally { splitter?.CancelDrag(); }
        }
    }

    public virtual void AdjustFixedChildrenPanelSizes(Size? parentSize = null)
    {
        // Native definition notifications are synchronous; keep the existing guard
        // around the original allocation and recursive parent-size propagation.
        if (resizingFixedChildren || childViews.Count == 0 || !IsLoaded)
        {
            return;
        }

        resizingFixedChildren = true;
        try
        {

            List<FrameworkElement> visibleChildren = GetVisibleChildren();
            if (visibleChildren.Count == 0)
            {
                return;
            }

            List<ILayoutPositionableElementWithActualSize> layoutChildrenModels = visibleChildren.OfType<ILayoutControl>()
              .Select(child => child.Model)
              .OfType<ILayoutPositionableElementWithActualSize>()
              .ToList();

            List<LayoutGridResizerControl> splitterChildren = visibleChildren.OfType<LayoutGridResizerControl>().ToList();
            List<ILayoutPositionableElementWithActualSize> fixedPanels;
            List<ILayoutPositionableElementWithActualSize> relativePanels;

            // Get current available size of panel.
            Size availableSize = parentSize ?? new Size(ActualWidth, ActualHeight);

            // Calculate minimum required size and current size of children.
            Size minimumSize = new(0, 0);
            Size currentSize = new(0, 0);
            Size preferredMinimumSize = new(0, 0);
            if (Orientation == Orientation.Vertical)
            {
                fixedPanels = layoutChildrenModels.Where(child => child.DockHeight.IsAbsolute).ToList();
                relativePanels = layoutChildrenModels.Where(child => !child.DockHeight.IsAbsolute).ToList();
                minimumSize.Width += layoutChildrenModels.Max(child => child.CalculatedDockMinWidth());
                minimumSize.Height += layoutChildrenModels.Sum(child => child.CalculatedDockMinHeight());
                minimumSize.Height += splitterChildren.Sum(child => child.ActualHeight);
                currentSize.Width += layoutChildrenModels.Max(child => child.ActualWidth);
                currentSize.Height += layoutChildrenModels.Sum(child => child.ActualHeight);
                currentSize.Height += splitterChildren.Sum(child => child.ActualHeight);
                preferredMinimumSize.Width += layoutChildrenModels.Max(child => child.CalculatedDockMinWidth());
                preferredMinimumSize.Height += minimumSize.Height + fixedPanels.Sum(child => child.FixedDockHeight) - fixedPanels.Sum(child => child.CalculatedDockMinHeight());
            }
            else
            {
                fixedPanels = layoutChildrenModels.Where(child => child.DockWidth.IsAbsolute).ToList();
                relativePanels = layoutChildrenModels.Where(child => !child.DockWidth.IsAbsolute).ToList();
                minimumSize.Width += layoutChildrenModels.Sum(child => child.CalculatedDockMinWidth());
                minimumSize.Height += layoutChildrenModels.Max(child => child.CalculatedDockMinHeight());
                minimumSize.Width += splitterChildren.Sum(child => child.ActualWidth);
                currentSize.Width += layoutChildrenModels.Sum(child => child.ActualWidth);
                currentSize.Height += layoutChildrenModels.Max(child => child.ActualHeight);
                currentSize.Width += splitterChildren.Sum(child => child.ActualWidth);
                preferredMinimumSize.Height += layoutChildrenModels.Max(child => child.CalculatedDockMinHeight());
                preferredMinimumSize.Width += minimumSize.Width + fixedPanels.Sum(child => child.FixedDockWidth) - fixedPanels.Sum(child => child.CalculatedDockMinWidth());
            }

            // Apply corrected sizes for fixed panels.
            if (Orientation == Orientation.Vertical)
            {
                double delta = availableSize.Height - currentSize.Height;
                double relativeDelta = relativePanels.Sum(child => child.ActualHeight - child.CalculatedDockMinHeight());
                delta += relativeDelta;
                foreach (ILayoutPositionableElementWithActualSize fixedChild in fixedPanels)
                {
                    if (minimumSize.Height >= availableSize.Height)
                    {
                        fixedChild.ResizableAbsoluteDockHeight = fixedChild.CalculatedDockMinHeight();
                    }
                    else if (preferredMinimumSize.Height <= availableSize.Height)
                    {
                        fixedChild.ResizableAbsoluteDockHeight = fixedChild.FixedDockHeight;
                    }
                    else if (relativePanels.All(child => Math.Abs(child.ActualHeight - child.CalculatedDockMinHeight()) <= 1))
                    {
                        double panelFraction;
                        int indexOfChild = fixedPanels.IndexOf(fixedChild);
                        if (delta < 0)
                        {
                            double availableHeightLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
                              .Sum(child => child.ActualHeight - child.CalculatedDockMinHeight());
                            panelFraction = (fixedChild.ActualHeight - fixedChild.CalculatedDockMinHeight()) / (availableHeightLeft > 0 ? availableHeightLeft : 1);
                        }
                        else
                        {
                            double fixedHeightLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
                              .Sum(child => child.FixedDockHeight);
                            panelFraction = fixedChild.FixedDockHeight / (fixedHeightLeft > 0 ? fixedHeightLeft : 1);
                        }

                        double childActualHeight = fixedChild.ActualHeight;
                        double heightToSet = Math.Max(Math.Round(delta * panelFraction + fixedChild.ActualHeight), fixedChild.CalculatedDockMinHeight());
                        fixedChild.ResizableAbsoluteDockHeight = heightToSet;
                        delta -= heightToSet - childActualHeight;
                    }
                }
            }
            else
            {
                double delta = availableSize.Width - currentSize.Width;
                double relativeDelta = relativePanels.Sum(child => child.ActualWidth - child.CalculatedDockMinWidth());
                delta += relativeDelta;
                foreach (ILayoutPositionableElementWithActualSize fixedChild in fixedPanels)
                {
                    if (minimumSize.Width >= availableSize.Width)
                    {
                        fixedChild.ResizableAbsoluteDockWidth = fixedChild.CalculatedDockMinWidth();
                    }
                    else if (preferredMinimumSize.Width <= availableSize.Width)
                    {
                        fixedChild.ResizableAbsoluteDockWidth = fixedChild.FixedDockWidth;
                    }
                    else
                    {
                        double panelFraction;
                        int indexOfChild = fixedPanels.IndexOf(fixedChild);
                        if (delta < 0)
                        {
                            double availableWidthLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
                              .Sum(child => child.ActualWidth - child.CalculatedDockMinWidth());
                            panelFraction = (fixedChild.ActualWidth - fixedChild.CalculatedDockMinWidth()) / (availableWidthLeft > 0 ? availableWidthLeft : 1);
                        }
                        else
                        {
                            double fixedWidthLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
                              .Sum(child => child.FixedDockWidth);
                            panelFraction = fixedChild.FixedDockWidth / (fixedWidthLeft > 0 ? fixedWidthLeft : 1);
                        }

                        double childActualWidth = fixedChild.ActualWidth;
                        double widthToSet = Math.Max(Math.Round(delta * panelFraction + fixedChild.ActualWidth), fixedChild.CalculatedDockMinWidth());
                        fixedChild.ResizableAbsoluteDockWidth = widthToSet;
                        delta -= widthToSet - childActualWidth;
                    }
                }
            }

            foreach (IAdjustableSizeLayout child in GetLayoutChildren().OfType<IAdjustableSizeLayout>())
            {
                child.AdjustFixedChildrenPanelSizes(availableSize);
            }
        }
        finally { resizingFixedChildren = false; }
    }

    private FrameworkElement? GetNextVisibleChild(int index)
    {
        List<FrameworkElement> layoutChildren = GetLayoutChildren();
        for (int i = index + 1; i < layoutChildren.Count; i++)
        {
            if (layoutChildren[i] is LayoutGridResizerControl)
            {
                continue;
            }

            if (IsChildVisible(i))
            {
                return layoutChildren[i];
            }
        }

        return null;
    }

    private List<FrameworkElement> GetVisibleChildren()
    {
        List<FrameworkElement> layoutChildren = GetLayoutChildren();
        List<FrameworkElement> visibleChildren = new();
        for (int i = 0; i < layoutChildren.Count; i++)
        {
            if (IsChildVisible(i) && layoutChildren[i] is FrameworkElement)
            {
                visibleChildren.Add(layoutChildren[i]);
            }
        }

        return visibleChildren;
    }

    private bool IsChildVisible(int index)
    {
        if (Orientation == Orientation.Horizontal)
        {
            if (index < ColumnDefinitions.Count)
            {
                return ColumnDefinitions[index].Width.IsStar || ColumnDefinitions[index].Width.Value > 0;
            }
        }
        else if (index < RowDefinitions.Count)
        {
            return RowDefinitions[index].Height.IsStar || RowDefinitions[index].Height.Value > 0;
        }

        return false;
    }

    private sealed record ChildView(ILayoutElement Model, FrameworkElement View);

    private sealed class ResizeState(ILayoutPositionableElement first, ILayoutPositionableElement second,
        Size firstSize, Size secondSize, DragInputPosition start, double scale, bool horizontal, Rect previewBounds, Brush fill, double opacity)
    {
        internal ILayoutPositionableElement First { get; } = first;
        internal ILayoutPositionableElement Second { get; } = second;
        internal Size FirstSize { get; } = firstSize;
        internal Size SecondSize { get; } = secondSize;
        internal DragInputPosition Start { get; } = start;
        internal double Scale { get; } = scale;
        internal bool Horizontal { get; } = horizontal;
        internal Rect PreviewBounds { get; } = previewBounds;
        internal Brush Fill { get; } = fill;
        internal double Opacity { get; } = opacity;
        internal double Delta
        {
            get; set;
        }
    }
}
