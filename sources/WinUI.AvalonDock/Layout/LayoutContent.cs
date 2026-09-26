// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutContent.cs

using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Layout;

/// <summary>
/// Provides a base class for layout content.
/// </summary>
[ContentProperty(Name = nameof(Content))]
[Serializable]
public abstract class LayoutContent : LayoutElement, ILayoutElementForFloatingWindow, IComparable<LayoutContent>, ILayoutPreviousContainer, Core.Serialization.ISerializableLayoutContent, Core.Serialization.ISerializablePreviousContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutContent"/> class.
    /// </summary>
    internal LayoutContent()
    {
    }

    /// <summary>
    /// Occurs when the closed event is raised.
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Occurs when the closing event is raised.
    /// </summary>
    public event EventHandler<CancelEventArgs>? Closing;

    /// <summary>
    /// Occurs when the floating properties updated event is raised.
    /// </summary>
    public event EventHandler? FloatingPropertiesUpdated;

    /// <summary>
    /// Identifies the <see cref="Title"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(LayoutContent), new PropertyMetadata(null, OnTitlePropertyChanged));

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Reports title notifications for every WinUI dependency-property update path.
    /// </summary>
    /// <param name="obj">The object instance.</param>
    /// <param name="args">The event arguments.</param>
    private static void OnTitlePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        // WinUI has no coercion callback; both notifications run after the DP value changes.
        LayoutContent content = (LayoutContent)obj;
        content.RaisePropertyChanging(nameof(Title));
        content.RaisePropertyChanged(nameof(Title));
    }

    [NonSerialized]
    private object? storedContent;

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    [XmlIgnore]
    public object? Content
    {
        get => storedContent;
        set
        {
            if (value == storedContent)
            {
                return;
            }

            RaisePropertyChanging(nameof(Content));
            storedContent = value;
            RaisePropertyChanged(nameof(Content));
            if (ContentId == null)
            {
                SetContentIdFromContent();
            }
        }
    }

    /// <summary>
    /// Identifies the <see cref="ContentId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ContentIdProperty = DependencyProperty.Register(nameof(ContentId), typeof(string), typeof(LayoutContent), new PropertyMetadata(null, OnContentIdPropertyChanged));

    /// <summary>
    /// Gets or sets the content id.
    /// </summary>
    public string? ContentId
    {
        get
        {
            string? value = (string?)GetValue(ContentIdProperty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
            // #83 - if Content.Name is empty at setting content and will be set later, ContentId will stay null.
            SetContentIdFromContent();
            return (string?)GetValue(ContentIdProperty);
        }
        set => SetValue(ContentIdProperty, value);
    }

    /// <summary>
    /// Executes the on content id property changed operation.
    /// </summary>
    /// <param name="obj">The object instance.</param>
    /// <param name="args">The event arguments.</param>
    private static void OnContentIdPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is LayoutContent layoutContent)
        {
            layoutContent.OnContentIdPropertyChanged((string?)args.OldValue, (string?)args.NewValue);
        }
    }

    /// <summary>
    /// Executes the on content id property changed operation.
    /// </summary>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    private void OnContentIdPropertyChanged(string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
        {
            RaisePropertyChanged(nameof(ContentId));
        }
    }

    /// <summary>
    /// Sets the content id from content.
    /// </summary>
    private void SetContentIdFromContent()
    {
        FrameworkElement? contentAsControl = storedContent as FrameworkElement;
        // WinUI has no SetCurrentValue; replace a blank local value but not a binding expression.
        object? localValue = ReadLocalValue(ContentIdProperty);
        if (!string.IsNullOrWhiteSpace(contentAsControl?.Name) &&
            (localValue is string || localValue is null || ReferenceEquals(localValue, DependencyProperty.UnsetValue)))
        {
            SetValue(ContentIdProperty, contentAsControl.Name);
        }
    }

    private bool isSelected = false;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is selected.
    /// </summary>
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (value == isSelected)
            {
                return;
            }

            bool oldValue = isSelected;
            RaisePropertyChanging(nameof(IsSelected));
            isSelected = value;
            if (Parent is ILayoutContentSelector parentSelector)
            {
                parentSelector.SelectedContentIndex = isSelected ? parentSelector.IndexOf(this) : -1;
            }

            OnIsSelectedChanged(oldValue, value);
            RaisePropertyChanged(nameof(IsSelected));
            LayoutAnchorableTabItem.CancelMouseLeave();
        }
    }

    /// <summary>
    /// Executes the on is selected changed operation.
    /// </summary>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    protected virtual void OnIsSelectedChanged(bool oldValue, bool newValue) => IsSelectedChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Occurs when the is selected changed event is raised.
    /// </summary>
    public event EventHandler? IsSelectedChanged;

    [field: NonSerialized]
    private bool isActive = false;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is active.
    /// </summary>
    [XmlIgnore]
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (value == isActive)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsActive));
            bool oldValue = isActive;
            isActive = value;
            ILayoutRoot? root = Root;
            if (root != null)
            {
                if (root.ActiveContent != this && value)
                {
                    root.ActiveContent = this;
                }

                if (isActive && root.ActiveContent != this)
                {
                    root.ActiveContent = this;
                }
            }

            if (isActive)
            {
                IsSelected = true;
            }

            OnIsActiveChanged(oldValue, value);
            RaisePropertyChanged(nameof(IsActive));
        }
    }

    /// <summary>
    /// Executes the on is active changed operation.
    /// </summary>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    protected virtual void OnIsActiveChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            LastActivationTimeStamp = DateTime.Now;
        }

        IsActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the is active changed event is raised.
    /// </summary>
    public event EventHandler? IsActiveChanged;

    private bool isLastFocusedDocument = false;

    /// <summary>
    /// Gets a value indicating whether this instance is the last focused document.
    /// </summary>
    public bool IsLastFocusedDocument
    {
        get => isLastFocusedDocument;
        internal set
        {
            if (value == isLastFocusedDocument)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsLastFocusedDocument));
            isLastFocusedDocument = value;
            RaisePropertyChanged(nameof(IsLastFocusedDocument));
        }
    }

    [field: NonSerialized]
    private ILayoutContainer? previousContainer;

    /// <inheritdoc/>
    [XmlIgnore]
    ILayoutContainer? ILayoutPreviousContainer.PreviousContainer
    {
        get => previousContainer;
        set
        {
            if (value == previousContainer)
            {
                return;
            }

            previousContainer = value;
            RaisePropertyChanged(nameof(PreviousContainer));
            if (previousContainer is ILayoutPaneSerializable paneSerializable && paneSerializable.Id == null)
            {
                paneSerializable.Id = Guid.NewGuid().ToString();
            }
        }
    }

    /// <summary>
    /// Gets or sets the previous container.
    /// </summary>
    protected ILayoutContainer? PreviousContainer
    {
        get => ((ILayoutPreviousContainer)this).PreviousContainer;
        set => ((ILayoutPreviousContainer)this).PreviousContainer = value;
    }

    /// <inheritdoc/>
    [XmlIgnore]
    string? ILayoutPreviousContainer.PreviousContainerId
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the previous container id.
    /// </summary>
    protected string? PreviousContainerId
    {
        get => ((ILayoutPreviousContainer)this).PreviousContainerId;
        set => ((ILayoutPreviousContainer)this).PreviousContainerId = value;
    }

    [field: NonSerialized]
    private int previousContainerIndex = -1;

    /// <summary>
    /// Gets or sets the previous container index.
    /// </summary>
    [XmlIgnore]
    public int PreviousContainerIndex
    {
        get => previousContainerIndex;
        set
        {
            if (value == previousContainerIndex)
            {
                return;
            }

            previousContainerIndex = value;
            RaisePropertyChanged(nameof(PreviousContainerIndex));
        }
    }

    private DateTime? lastActivationTimeStamp = null;

    /// <summary>
    /// Gets or sets the last activation time stamp.
    /// </summary>
    public DateTime? LastActivationTimeStamp
    {
        get => lastActivationTimeStamp;
        set
        {
            if (value == lastActivationTimeStamp)
            {
                return;
            }

            lastActivationTimeStamp = value;
            RaisePropertyChanged(nameof(LastActivationTimeStamp));
        }
    }

    private double floatingWidth = 0.0;

    /// <summary>
    /// Gets or sets the floating width.
    /// </summary>
    public double FloatingWidth
    {
        get => floatingWidth;
        set
        {
            if (value == floatingWidth)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingWidth));
            floatingWidth = value;
            RaisePropertyChanged(nameof(FloatingWidth));
        }
    }

    private double floatingHeight = 0.0;

    /// <summary>
    /// Gets or sets the floating height.
    /// </summary>
    public double FloatingHeight
    {
        get => floatingHeight;
        set
        {
            if (value == floatingHeight)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingHeight));
            floatingHeight = value;
            RaisePropertyChanged(nameof(FloatingHeight));
        }
    }

    private double floatingLeft = 0.0;

    /// <summary>
    /// Gets or sets the floating left.
    /// </summary>
    public double FloatingLeft
    {
        get => floatingLeft;
        set
        {
            if (value == floatingLeft)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingLeft));
            floatingLeft = value;
            RaisePropertyChanged(nameof(FloatingLeft));
        }
    }

    private double floatingTop = 0.0;

    /// <summary>
    /// Gets or sets the floating top.
    /// </summary>
    public double FloatingTop
    {
        get => floatingTop;
        set
        {
            if (value == floatingTop)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingTop));
            floatingTop = value;
            RaisePropertyChanged(nameof(FloatingTop));
        }
    }

    private bool isMaximized = false;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is maximized.
    /// </summary>
    public bool IsMaximized
    {
        get => isMaximized;
        set
        {
            if (value == isMaximized)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsMaximized));
            isMaximized = value;
            RaisePropertyChanged(nameof(IsMaximized));
        }
    }

    private object? toolTip;

    /// <summary>
    /// Gets or sets the tool tip.
    /// </summary>
    public object? ToolTip
    {
        get => toolTip;
        set
        {
            if (value == toolTip)
            {
                return;
            }

            toolTip = value;
            RaisePropertyChanged(nameof(ToolTip));
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is floating.
    /// </summary>
    [Bindable(true)]
    [Description("Gets whether the content is currently floating or not.")]
    [Category("Other")]
    public bool IsFloating => this.FindParent<LayoutFloatingWindow>() != null;

    private ImageSource? iconSource;

    /// <summary>
    /// Gets or sets the icon source.
    /// </summary>
    public ImageSource? IconSource
    {
        get => iconSource;
        set
        {
            if (value == iconSource)
            {
                return;
            }

            iconSource = value;
            RaisePropertyChanged(nameof(IconSource));
        }
    }

    // BD: 14.08.2020 added canCloseDefault to properly implement inverting canClose default value in inheritors (e.g. LayoutAnchorable)
    //     Thus CanClose property will be serialized only when not equal to its default for given class
    //     With previous code it was not possible to serialize CanClose if set to true for LayoutAnchorable instance

    /// <summary>
    /// Stores the current close capability value.
    /// </summary>
    internal bool canClose = true;

    // BD: 14.08.2020 added canCloseDefault to properly implement inverting canClose default value in inheritors (e.g. LayoutAnchorable)
    //     Thus CanClose property will be serialized only when not equal to its default for given class
    //     With previous code it was not possible to serialize CanClose if set to true for LayoutAnchorable instance

    /// <summary>
    /// Stores the default close capability value for serialization comparisons.
    /// </summary>
    internal bool canCloseDefault = true;

    /// <summary>
    /// Gets or sets a value indicating whether this instance can close.
    /// </summary>
    public bool CanClose
    {
        get => canClose;
        set
        {
            if (canClose == value)
            {
                return;
            }

            canClose = value;
            RaisePropertyChanged(nameof(CanClose));
        }
    }

    private bool canFloat = true;

    /// <summary>
    /// Gets or sets a value indicating whether this instance can float.
    /// </summary>
    public bool CanFloat
    {
        get => canFloat;
        set
        {
            if (value == canFloat)
            {
                return;
            }

            canFloat = value;
            RaisePropertyChanged(nameof(CanFloat));
        }
    }

    private bool canShowOnHover = true;

    /// <summary>
    /// Gets or sets a value indicating whether this instance can show on hover.
    /// </summary>
    public bool CanShowOnHover
    {
        get => canShowOnHover;
        set
        {
            if (value == canShowOnHover)
            {
                return;
            }

            canShowOnHover = value;
            RaisePropertyChanged(nameof(CanShowOnHover));
        }
    }

    private bool isEnabled = true;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (value == isEnabled)
            {
                return;
            }

            isEnabled = value;
            RaisePropertyChanged(nameof(IsEnabled));
        }
    }

    /// <summary>
    /// Gets the tab item.
    /// </summary>
    public LayoutDocumentTabItem? TabItem
    {
        get; internal set;
    }

    /// <summary>
    /// Executes the close operation.
    /// </summary>
    public abstract void Close();

    /// <summary>
    /// Executes the compare to operation.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The resulting value.</returns>
    public int CompareTo(LayoutContent? other)
    {
        if (other == null)
        {
            return 1;
        }

        if (Content is IComparable contentAsComparable)
        {
            return contentAsComparable.CompareTo(other.Content);
        }

        return string.Compare(Title, other.Title);
    }

    /// <summary>
    /// Executes the float operation.
    /// </summary>
    public void Float()
    {
        if (Root is not { } root)
        {
            return;
        }

        if (PreviousContainer is ILayoutGroup previousContainerAsLayoutGroup && previousContainerAsLayoutGroup.FindParent<LayoutFloatingWindow>() != null
            && (this is not LayoutAnchorable tool || root.Manager?.IsDetached(tool) != true))
        {
            DockingManager? manager = root.Manager;
            if (manager != null && !manager.TryBeginContentFloating(this))
            {
                return;
            }

            ILayoutGroup? currentContainer = Parent as ILayoutGroup;
            int currentContainerIndex = currentContainer?.IndexOfChild(this) ?? -1;

            if (PreviousContainerIndex < previousContainerAsLayoutGroup.ChildrenCount)
            {
                previousContainerAsLayoutGroup.InsertChildAt(PreviousContainerIndex, this);
            }
            else
            {
                previousContainerAsLayoutGroup.InsertChildAt(previousContainerAsLayoutGroup.ChildrenCount, this);
            }

            PreviousContainer = currentContainer;
            PreviousContainerIndex = currentContainerIndex;
            IsSelected = true;
            IsActive = true;
            root.CollectGarbage();
            manager?.RaiseContentFloated(this);
        }
        else
        {
            if (root.Manager is not { } manager)
            {
                return;
            }

            manager.StartDraggingFloatingWindowForContent(this, false);
            if (!IsFloating)
            {
                return;
            }

            IsSelected = true;
            IsActive = true;
        }

        // BD: 14.08.2020 raise IsFloating property changed
        RaisePropertyChanged(nameof(IsFloating));
    }

    /// <summary>
    /// Executes the dock as document operation.
    /// </summary>
    public void DockAsDocument()
    {
        if (!(Root is LayoutRoot root))
        {
            throw new InvalidOperationException();
        }

        bool wasFloating = IsFloating;

        if (PreviousContainer is LayoutDocumentPane previousDocumentPane &&
            previousDocumentPane.FindParent<LayoutDocumentFloatingWindow>() == null)
        {
            Dock();
            return;
        }

        LayoutDocumentPane? newParentPane = null;
        if (root.LastFocusedDocument is { } lastDocument &&
            lastDocument != this &&
            lastDocument.FindParent<LayoutDocumentFloatingWindow>() == null)
        {
            newParentPane = lastDocument.Parent as LayoutDocumentPane;
        }

        if (newParentPane == null)
        {
            newParentPane = root.Descendents()
                .OfType<LayoutDocumentPane>()
                .FirstOrDefault(pane => pane.FindParent<LayoutDocumentFloatingWindow>() == null);
        }

        if (newParentPane == null)
        {
            newParentPane = root.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        }

        if (newParentPane != null)
        {
            newParentPane.Children.Add(this);
            root.CollectGarbage();
        }

        IsSelected = true;
        IsActive = true;

        // BD: 14.08.2020 raise IsFloating property changed
        RaisePropertyChanged(nameof(IsFloating));

        if (wasFloating && !IsFloating)
        {
            root.Manager?.RaiseContentDocked(this);
        }
    }

    /// <summary>
    /// Executes the dock operation.
    /// </summary>
    public void Dock()
    {
        bool wasFloating = IsFloating;

        if (PreviousContainer is ILayoutGroup previousContainerAsLayoutGroup)
        {
            ILayoutContainer? currentContainer = Parent;
            int currentContainerIndex = currentContainer is ILayoutGroup currentGroup ? currentGroup.IndexOfChild(this) : -1;

            if (PreviousContainerIndex < previousContainerAsLayoutGroup.ChildrenCount)
            {
                previousContainerAsLayoutGroup.InsertChildAt(PreviousContainerIndex, this);
            }
            else
            {
                previousContainerAsLayoutGroup.InsertChildAt(previousContainerAsLayoutGroup.ChildrenCount, this);
            }

            if (currentContainerIndex > -1)
            {
                PreviousContainer = currentContainer;
                PreviousContainerIndex = currentContainerIndex;
            }
            else
            {
                PreviousContainer = null;
                PreviousContainerIndex = 0;
            }

            IsSelected = true;
            IsActive = true;
        }
        else
        {
            InternalDock();
        }

        Root?.CollectGarbage();

        // BD: 14.08.2020 raise IsFloating property changed
        RaisePropertyChanged(nameof(IsFloating));

        if (wasFloating && !IsFloating)
        {
            Root?.Manager?.RaiseContentDocked(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        if (oldValue != null)
        {
            IsSelected = false;
        }

        base.OnParentChanging(oldValue, newValue);
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        if (IsSelected && Parent is ILayoutContentSelector parentSelector)
        {
            parentSelector.SelectedContentIndex = parentSelector.IndexOf(this);
        }

        base.OnParentChanged(oldValue, newValue);
    }

    /// <summary>
    /// Executes the test can close operation.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    internal bool TestCanClose()
    {
        CancelEventArgs args = new();
        OnClosing(args);
        return !args.Cancel;
    }

    /// <summary>
    /// Executes the close internal operation.
    /// </summary>
    internal void CloseInternal()
    {
        ILayoutRoot? root = Root;
        ILayoutContainer? parentAsContainer = Parent;
        if (parentAsContainer == null)
        {
            return;
        }

        if (PreviousContainer == null)
        {
            ILayoutGroup? parentAsGroup = Parent as ILayoutGroup;
            PreviousContainer = parentAsContainer;
            if (parentAsGroup != null)
            {
                PreviousContainerIndex = parentAsGroup.IndexOfChild(this);
            }

            if (parentAsGroup is ILayoutPaneSerializable layoutPaneSerializable)
            {
                PreviousContainerId = layoutPaneSerializable.Id;
                // This parentAsGroup will be removed in the GarbageCollection below
                if (parentAsGroup.Children.Count() == 1 && parentAsGroup.Parent != null && root?.Manager is { } manager)
                {
                    Parent = manager.Layout;
                    PreviousContainer = parentAsGroup.Parent;
                    PreviousContainerIndex = -1;

                    if (parentAsGroup.Parent is ILayoutPaneSerializable paneSerializable)
                    {
                        PreviousContainerId = paneSerializable.Id;
                    }
                    else
                    {
                        PreviousContainerId = null;
                    }
                }
            }
        }

        parentAsContainer.RemoveChild(this);
        root?.CollectGarbage();
        OnClosed();
    }

    /// <summary>
    /// Executes the on closed operation.
    /// </summary>
    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Executes the on closing operation.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    protected virtual void OnClosing(CancelEventArgs args) => Closing?.Invoke(this, args);

    /// <summary>
    /// Executes the internal dock operation.
    /// </summary>
    protected virtual void InternalDock()
    {
    }

    /// <inheritdoc/>
    void ILayoutElementForFloatingWindow.RaiseFloatingPropertiesUpdated() => FloatingPropertiesUpdated?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    object? Core.Serialization.ISerializableLayoutContent.IconSource
    {
        get => IconSource;
        set => IconSource = value as Microsoft.UI.Xaml.Media.ImageSource;
    }

    /// <inheritdoc/>
    Core.Serialization.ISerializableLayoutContainer? Core.Serialization.ISerializablePreviousContainer.PreviousContainer
    {
        get => previousContainer as Core.Serialization.ISerializableLayoutContainer;
        set => ((ILayoutPreviousContainer)this).PreviousContainer = value as ILayoutContainer;
    }

    /// <inheritdoc/>
    string? Core.Serialization.ISerializablePreviousContainer.PreviousContainerId
    {
        get => ((ILayoutPreviousContainer)this).PreviousContainerId;
        set => ((ILayoutPreviousContainer)this).PreviousContainerId = value;
    }
}
