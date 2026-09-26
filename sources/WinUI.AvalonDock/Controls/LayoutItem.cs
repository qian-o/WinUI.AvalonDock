// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutItem.cs

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AvalonDock.Commands;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using BindableAttribute = System.ComponentModel.BindableAttribute;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout item.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public abstract partial class LayoutItem : FrameworkElement
{
    private ICommand? defaultCloseCommand;
    private ICommand? defaultFloatCommand;
    private ICommand? defaultDockAsDocumentCommand;
    private ICommand? defaultCloseAllButThisCommand;
    private ICommand? defaultCloseAllCommand;
    private ICommand? defaultActivateCommand;
    private ICommand? defaultNewVerticalTabGroupCommand;
    private ICommand? defaultNewHorizontalTabGroupCommand;
    private ICommand? defaultMoveToNextTabGroupCommand;
    private ICommand? defaultMoveToPreviousTabGroupCommand;
    private ContentPresenter? view;
    private DockingManager? logicalOwner;
    private UIElement? logicalContent;
    private readonly ReentrantFlag isSelectedReentrantFlag = new();
    private readonly ReentrantFlag isActiveReentrantFlag = new();



    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutItem"/> class.
    /// </summary>
    internal LayoutItem()
    {
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => OnVisibilityChanged());
        RegisterPropertyChangedCallback(ToolTipService.ToolTipProperty, (_, _) => OnToolTipChanged());
    }

    /// <summary>
    /// Gets the layout element.
    /// </summary>
    public LayoutContent? LayoutElement
    {
        get; private set;
    }

    /// <summary>
    /// Gets the model.
    /// </summary>
    public object? Model
    {
        get; private set;
    }

    /// <summary>
    /// Gets the view.
    /// </summary>
    public ContentPresenter View
    {
        get
        {
            if (view != null)
            {
                return view;
            }

            view = new ContentPresenter();
            view.SetBinding(ContentPresenter.ContentProperty, new Binding { Path = new PropertyPath(nameof(ContentPresenter.Content)), Source = LayoutElement });
            if (LayoutElement?.Root == null)
            {
                return view;
            }

            view.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.LayoutItemTemplate)), Source = LayoutElement.Root.Manager });
            view.SetBinding(ContentPresenter.ContentTemplateSelectorProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.LayoutItemTemplateSelector)), Source = LayoutElement.Root.Manager });
            logicalOwner?.InternalAddLogicalChild(view);
            return view;
        }
    }

    /// <summary>
    /// <see cref="Title"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(LayoutItem),
            new PropertyMetadata(null, OnTitleChanged));

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the the title of the element.")]
    [Category("Other")]
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Handles changes to the <see cref="Title"/> property.</summary>
    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnTitleChanged(e);

    /// <summary>
    /// Raises the title changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnTitleChanged(DependencyPropertyChangedEventArgs e)
    {
        if (LayoutElement != null)
        {
            LayoutElement.Title = (string?)e.NewValue;
        }
    }

    /// <summary>
    /// <see cref="IconSource"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(ImageSource), typeof(LayoutItem),
            new PropertyMetadata(null, OnIconSourceChanged));

    /// <summary>
    /// Gets or sets the icon source.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the icon associated with the item.")]
    [Category("Other")]
    public ImageSource? IconSource
    {
        get => (ImageSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    /// <summary>Handles changes to the <see cref="IconSource"/> property.</summary>
    private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnIconSourceChanged(e);

    /// <summary>
    /// Raises the icon source changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnIconSourceChanged(DependencyPropertyChangedEventArgs e)
    {
        if (LayoutElement != null)
        {
            LayoutElement.IconSource = IconSource;
        }
    }

    /// <summary>
    /// <see cref="ContentId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ContentIdProperty = DependencyProperty.Register(nameof(ContentId), typeof(string), typeof(LayoutItem),
            new PropertyMetadata(null, OnContentIdChanged));

    /// <summary>
    /// Gets or sets the content id.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the content id used to retrieve content when deserializing layouts.")]
    [Category("Other")]
    public string? ContentId
    {
        get => (string?)GetValue(ContentIdProperty);
        set => SetValue(ContentIdProperty, value);
    }

    /// <summary>Handles changes to the <see cref="ContentId"/> property.</summary>
    private static void OnContentIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnContentIdChanged(e);

    /// <summary>
    /// Raises the content id changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnContentIdChanged(DependencyPropertyChangedEventArgs e)
    {
        if (LayoutElement != null)
        {
            LayoutElement.ContentId = (string?)e.NewValue;
        }
    }

    /// <summary>
    /// <see cref="IsSelected"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(LayoutItem),
            new PropertyMetadata(false, OnIsSelectedChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance is selected.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets wether the item is selected inside its container or not.")]
    [Category("Other")]
    public bool IsSelected
    {
        get => (bool?)GetValue(IsSelectedProperty) ?? false;
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Handles changes to the <see cref="IsSelected"/> property.</summary>
    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnIsSelectedChanged(e);

    /// <summary>
    /// Raises the is selected changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnIsSelectedChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!isSelectedReentrantFlag.CanEnter)
        {
            return;
        }

        using (isSelectedReentrantFlag.Enter())
        {
            if (LayoutElement != null)
            {
                LayoutElement.IsSelected = (bool)e.NewValue;
            }
        }
    }

    /// <summary>
    /// <see cref="IsActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LayoutItem),
            new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance is active.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets wether the item is active in the UI or not.")]
    [Category("Other")]
    public bool IsActive
    {
        get => (bool?)GetValue(IsActiveProperty) ?? false;
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Handles changes to the <see cref="IsActive"/> property.</summary>
    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnIsActiveChanged(e);

    /// <summary>
    /// Raises the is active changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnIsActiveChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!isActiveReentrantFlag.CanEnter)
        {
            return;
        }

        using (isActiveReentrantFlag.Enter())
        {
            if (LayoutElement != null)
            {
                LayoutElement.IsActive = (bool)e.NewValue;
            }
        }
    }

    /// <summary>
    /// <see cref="CanClose"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.Register(nameof(CanClose), typeof(bool), typeof(LayoutItem),
            new PropertyMetadata(true, OnCanCloseChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance can close.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets wetherthe item can be closed or not.")]
    [Category("Other")]
    public bool CanClose
    {
        get => (bool?)GetValue(CanCloseProperty) ?? false;
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CanClose"/> property.</summary>
    private static void OnCanCloseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCanCloseChanged(e);

    /// <summary>
    /// Raises the can close changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCanCloseChanged(DependencyPropertyChangedEventArgs e)
    {
        if (LayoutElement != null)
        {
            LayoutElement.CanClose = (bool)e.NewValue;
        }
    }

    /// <summary>
    /// <see cref="CanFloat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanFloatProperty = DependencyProperty.Register(nameof(CanFloat), typeof(bool), typeof(LayoutItem),
            new PropertyMetadata(true, OnCanFloatChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance can float.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets wether the user can move the layout element dragging it to another position.")]
    [Category("Other")]
    public bool CanFloat
    {
        get => (bool?)GetValue(CanFloatProperty) ?? false;
        set => SetValue(CanFloatProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CanFloat"/> property.</summary>
    private static void OnCanFloatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCanFloatChanged(e);

    /// <summary>
    /// Raises the can float changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCanFloatChanged(DependencyPropertyChangedEventArgs e)
    {
        if (LayoutElement != null)
        {
            LayoutElement.CanFloat = (bool)e.NewValue;
        }
    }

    /// <summary>
    /// <see cref="CloseCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseCommandChanged));

    /// <summary>
    /// Gets or sets the close command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the document close button.")]
    [Category("Other")]
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseCommand"/> property.</summary>
    private static void OnCloseCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseCommandChanged(e);

    /// <summary>
    /// Raises the close command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="CloseCommand"/>  value.</summary>
    private static object CoerceCloseCommandValue(DependencyObject d, object value) => value;

    private bool CanExecuteCloseCommand(object parameter) => LayoutElement != null && LayoutElement.CanClose;

    private void ExecuteCloseCommand(object parameter) => Close();

    /// <summary>
    /// Close.
    /// </summary>
    protected abstract void Close();

    /// <summary>
    /// <see cref="FloatCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FloatCommandProperty = DependencyProperty.Register(nameof(FloatCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnFloatCommandChanged));

    /// <summary>
    /// Gets or sets the float command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when the user clicks the float button.")]
    [Category("Other")]
    public ICommand? FloatCommand
    {
        get => (ICommand?)GetValue(FloatCommandProperty);
        set => SetValue(FloatCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="FloatCommand"/> property.</summary>
    private static void OnFloatCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnFloatCommandChanged(e);

    /// <summary>
    /// Raises the float command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnFloatCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="FloatCommand"/> value.</summary>
    private static object CoerceFloatCommandValue(DependencyObject d, object value) => value;

    private bool CanExecuteFloatCommand(object anchorable) =>
        LayoutElement != null
        && LayoutElement.CanFloat
        && LayoutElement.Root?.Manager?.AllowFloatingWindows != false
        && LayoutElement.FindParent<LayoutFloatingWindow>() == null;

    /// <summary>Executes to float the content of this LayoutItem in a separate <see cref="LayoutFloatingWindowControl"/>.</summary>
    /// <param name="parameter">The command parameter.</param>
    private void ExecuteFloatCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteFloatCommand(content);
        }
    }

    /// <summary>
    /// <see cref="DockAsDocumentCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DockAsDocumentCommandProperty = DependencyProperty.Register(nameof(DockAsDocumentCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnDockAsDocumentCommandChanged));

    /// <summary>
    /// Gets or sets the dock as document command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the DockAsDocument button.")]
    [Category("Other")]
    public ICommand? DockAsDocumentCommand
    {
        get => (ICommand?)GetValue(DockAsDocumentCommandProperty);
        set => SetValue(DockAsDocumentCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="DockAsDocumentCommand"/> property.</summary>
    private static void OnDockAsDocumentCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnDockAsDocumentCommandChanged(e);

    /// <summary>
    /// Raises the dock as document command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDockAsDocumentCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="DockAsDocumentCommand"/> value.</summary>
    private static object CoerceDockAsDocumentCommandValue(DependencyObject d, object value) => value;

    /// <summary>
    /// Determines whether the dock as document command can execute.
    /// </summary>
    /// <returns>true if the instance can execute dock as document command; otherwise, false.</returns>
    protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement != null && LayoutElement.FindParent<LayoutDocumentPane>() == null;

    private bool CanExecuteDockAsDocumentCommand(object parameter) => CanExecuteDockAsDocumentCommand();

    private void ExecuteDockAsDocumentCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteDockAsDocumentCommand(content);
        }
    }

    /// <summary>
    /// <see cref="CloseAllButThisCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseAllButThisCommandProperty = DependencyProperty.Register(nameof(CloseAllButThisCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseAllButThisCommandChanged));

    /// <summary>
    /// Gets or sets the close all but this command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the the 'Close All But This' command.")]
    [Category("Other")]
    public ICommand? CloseAllButThisCommand
    {
        get => (ICommand?)GetValue(CloseAllButThisCommandProperty);
        set => SetValue(CloseAllButThisCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseAllButThisCommand"/> property.</summary>
    private static void OnCloseAllButThisCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseAllButThisCommandChanged(e);

    /// <summary>
    /// Raises the close all but this command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseAllButThisCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="CloseAllButThisCommand"/> value.</summary>
    private static object CoerceCloseAllButThisCommandValue(DependencyObject d, object value) => value;

    private bool CanExecuteCloseAllButThisCommand(object parameter)
    {
        ILayoutRoot? root = LayoutElement?.Root;
        if (root == null)
        {
            return false;
        }

        return root.Manager?.Layout.Descendents().OfType<LayoutContent>().Any(d => d != LayoutElement && (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow)) == true;
    }

    private void ExecuteCloseAllButThisCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteCloseAllButThisCommand(content);
        }
    }

    /// <summary>
    /// <see cref="CloseAllCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseAllCommandProperty = DependencyProperty.Register(nameof(CloseAllCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseAllCommandChanged));

    /// <summary>
    /// Gets or sets the close all command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the 'Close All' command.")]
    [Category("Other")]
    public ICommand? CloseAllCommand
    {
        get => (ICommand?)GetValue(CloseAllCommandProperty);
        set => SetValue(CloseAllCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseAllCommand"/> property.</summary>
    private static void OnCloseAllCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseAllCommandChanged(e);

    /// <summary>
    /// Raises the close all command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseAllCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="CloseAllCommand"/> value.</summary>
    private static object CoerceCloseAllCommandValue(DependencyObject d, object value) => value;

    private bool CanExecuteCloseAllCommand(object parameter)
    {
        ILayoutRoot? root = LayoutElement?.Root;
        if (root == null)
        {
            return false;
        }

        return root.Manager?.Layout.Descendents().OfType<LayoutContent>().Any(d => d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow) == true;
    }

    private void ExecuteCloseAllCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteCloseAllCommand(content);
        }
    }

    /// <summary>
    /// <see cref="ActivateCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActivateCommandProperty = DependencyProperty.Register(nameof(ActivateCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnActivateCommandChanged));

    /// <summary>
    /// Gets or sets the activate command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user wants to activate a content (either a Document or an Anchorable).")]
    [Category("Other")]
    public ICommand? ActivateCommand
    {
        get => (ICommand?)GetValue(ActivateCommandProperty);
        set => SetValue(ActivateCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="ActivateCommand"/> property.</summary>
    private static void OnActivateCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnActivateCommandChanged(e);

    /// <summary>
    /// Raises the activate command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnActivateCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Coerces the <see cref="ActivateCommand"/> value.</summary>
    private static object CoerceActivateCommandValue(DependencyObject d, object value) => value;

    private bool CanExecuteActivateCommand(object parameter) => LayoutElement != null;

    private void ExecuteActivateCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteContentActivateCommand(content);
        }
    }

    /// <summary>
    /// <see cref="NewVerticalTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NewVerticalTabGroupCommandProperty = DependencyProperty.Register(nameof(NewVerticalTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnNewVerticalTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the new vertical tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the new vertical tab group command.")]
    [Category("Other")]
    public ICommand? NewVerticalTabGroupCommand
    {
        get => (ICommand?)GetValue(NewVerticalTabGroupCommandProperty);
        set => SetValue(NewVerticalTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="NewVerticalTabGroupCommand"/> property.</summary>
    private static void OnNewVerticalTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnNewVerticalTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the new vertical tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnNewVerticalTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteNewVerticalTabGroupCommand(object parameter)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        if (LayoutElement is LayoutDocument layoutDocument && !layoutDocument.CanMove)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        return (parentDocumentGroup == null ||
                  parentDocumentGroup.ChildrenCount == 1 ||
                  parentDocumentGroup.Root?.Manager?.AllowMixedOrientation == true ||
                  parentDocumentGroup.Orientation == Orientation.Horizontal) &&
                 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
                 parentDocumentPane.ChildrenCount > 1;
    }

    private void ExecuteNewVerticalTabGroupCommand(object parameter)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement)
        {
            return;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = layoutElement.FindParent<LayoutDocumentPaneGroup>();

        if (parentDocumentGroup == null)
        {
            if (parentDocumentPane.Parent is not { } grandParent)
            {
                return;
            }
            parentDocumentGroup = new LayoutDocumentPaneGroup { Orientation = Orientation.Horizontal };
            grandParent.ReplaceChild(parentDocumentPane, parentDocumentGroup);
            parentDocumentGroup.Children.Add(parentDocumentPane);
        }

        parentDocumentGroup.Orientation = Orientation.Horizontal;
        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        parentDocumentGroup.InsertChildAt(indexOfParentPane + 1, new LayoutDocumentPane(layoutElement));
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }

    /// <summary>
    /// <see cref="NewHorizontalTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NewHorizontalTabGroupCommandProperty = DependencyProperty.Register(nameof(NewHorizontalTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnNewHorizontalTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the new horizontal tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the new horizontal tab group command.")]
    [Category("Other")]
    public ICommand? NewHorizontalTabGroupCommand
    {
        get => (ICommand?)GetValue(NewHorizontalTabGroupCommandProperty);
        set => SetValue(NewHorizontalTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="NewHorizontalTabGroupCommand"/> property.</summary>
    private static void OnNewHorizontalTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnNewHorizontalTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the new horizontal tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnNewHorizontalTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteNewHorizontalTabGroupCommand(object parameter)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        if (LayoutElement is LayoutDocument layoutDocument && !layoutDocument.CanMove)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        return (parentDocumentGroup == null ||
                  parentDocumentGroup.ChildrenCount == 1 ||
                  parentDocumentGroup.Root?.Manager?.AllowMixedOrientation == true ||
                  parentDocumentGroup.Orientation == Orientation.Vertical) &&
                 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
                 parentDocumentPane.ChildrenCount > 1;
    }

    private void ExecuteNewHorizontalTabGroupCommand(object parameter)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement)
        {
            return;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = layoutElement.FindParent<LayoutDocumentPaneGroup>();

        if (parentDocumentGroup == null)
        {
            if (parentDocumentPane.Parent is not { } grandParent)
            {
                return;
            }
            parentDocumentGroup = new LayoutDocumentPaneGroup { Orientation = Orientation.Vertical };
            grandParent.ReplaceChild(parentDocumentPane, parentDocumentGroup);
            parentDocumentGroup.Children.Add(parentDocumentPane);
        }

        parentDocumentGroup.Orientation = Orientation.Vertical;
        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        parentDocumentGroup.InsertChildAt(indexOfParentPane + 1, new LayoutDocumentPane(layoutElement));
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }

    /// <summary>
    /// <see cref="MoveToNextTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MoveToNextTabGroupCommandProperty = DependencyProperty.Register(nameof(MoveToNextTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnMoveToNextTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the move to next tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the move to next tab group command.")]
    [Category("Other")]
    public ICommand? MoveToNextTabGroupCommand
    {
        get => (ICommand?)GetValue(MoveToNextTabGroupCommandProperty);
        set => SetValue(MoveToNextTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="MoveToNextTabGroupCommand"/> property.</summary>
    private static void OnMoveToNextTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnMoveToNextTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the move to next tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnMoveToNextTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteMoveToNextTabGroupCommand(object parameter)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        return parentDocumentGroup != null &&
                 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
                 parentDocumentGroup.ChildrenCount > 1 &&
                 parentDocumentGroup.IndexOfChild(parentDocumentPane) < parentDocumentGroup.ChildrenCount - 1 &&
                 parentDocumentGroup.Children[parentDocumentGroup.IndexOfChild(parentDocumentPane) + 1] is LayoutDocumentPane;
    }

    private void ExecuteMoveToNextTabGroupCommand(object parameter)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement
            || layoutElement.FindParent<LayoutDocumentPaneGroup>() is not { } parentDocumentGroup)
        {
            return;
        }

        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        int targetIndex = indexOfParentPane + 1;
        if (targetIndex < 0 || targetIndex >= parentDocumentGroup.ChildrenCount
            || parentDocumentGroup.Children[targetIndex] is not LayoutDocumentPane nextDocumentPane)
        {
            return;
        }

        nextDocumentPane.InsertChildAt(0, layoutElement);
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }

    /// <summary>
    /// <see cref="MoveToPreviousTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MoveToPreviousTabGroupCommandProperty = DependencyProperty.Register(nameof(MoveToPreviousTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnMoveToPreviousTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the move to previous tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the move to previous tab group command.")]
    [Category("Other")]
    public ICommand? MoveToPreviousTabGroupCommand
    {
        get => (ICommand?)GetValue(MoveToPreviousTabGroupCommandProperty);
        set => SetValue(MoveToPreviousTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="MoveToPreviousTabGroupCommand"/> property.</summary>
    private static void OnMoveToPreviousTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnMoveToPreviousTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the move to previous tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnMoveToPreviousTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteMoveToPreviousTabGroupCommand(object parameter)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        return parentDocumentGroup != null &&
                 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
                 parentDocumentGroup.ChildrenCount > 1 &&
                 parentDocumentGroup.IndexOfChild(parentDocumentPane) > 0 &&
                 parentDocumentGroup.Children[parentDocumentGroup.IndexOfChild(parentDocumentPane) - 1] is LayoutDocumentPane;
    }

    private void ExecuteMoveToPreviousTabGroupCommand(object parameter)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement
            || layoutElement.FindParent<LayoutDocumentPaneGroup>() is not { } parentDocumentGroup)
        {
            return;
        }

        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        int targetIndex = indexOfParentPane - 1;
        if (targetIndex < 0 || targetIndex >= parentDocumentGroup.ChildrenCount
            || parentDocumentGroup.Children[targetIndex] is not LayoutDocumentPane nextDocumentPane)
        {
            return;
        }

        nextDocumentPane.InsertChildAt(0, layoutElement);
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }

    /// <summary>
    /// Init default commands.
    /// </summary>
    protected virtual void InitDefaultCommands()
    {
        defaultCloseCommand = new RelayCommand<object>(ExecuteCloseCommand, CanExecuteCloseCommand);
        defaultFloatCommand = new RelayCommand<object>(ExecuteFloatCommand, CanExecuteFloatCommand);
        defaultDockAsDocumentCommand = new RelayCommand<object>(ExecuteDockAsDocumentCommand, CanExecuteDockAsDocumentCommand);
        defaultCloseAllButThisCommand = new RelayCommand<object>(ExecuteCloseAllButThisCommand, CanExecuteCloseAllButThisCommand);
        defaultCloseAllCommand = new RelayCommand<object>(ExecuteCloseAllCommand, CanExecuteCloseAllCommand);
        defaultActivateCommand = new RelayCommand<object>(ExecuteActivateCommand, CanExecuteActivateCommand);
        defaultNewVerticalTabGroupCommand = new RelayCommand<object>(ExecuteNewVerticalTabGroupCommand, CanExecuteNewVerticalTabGroupCommand);
        defaultNewHorizontalTabGroupCommand = new RelayCommand<object>(ExecuteNewHorizontalTabGroupCommand, CanExecuteNewHorizontalTabGroupCommand);
        defaultMoveToNextTabGroupCommand = new RelayCommand<object>(ExecuteMoveToNextTabGroupCommand, CanExecuteMoveToNextTabGroupCommand);
        defaultMoveToPreviousTabGroupCommand = new RelayCommand<object>(ExecuteMoveToPreviousTabGroupCommand, CanExecuteMoveToPreviousTabGroupCommand);
    }

    /// <summary>
    /// Clear default bindings.
    /// </summary>
    protected virtual void ClearDefaultBindings()
    {
        if (CloseCommand == defaultCloseCommand)
        {
            ClearValue(CloseCommandProperty);
        }

        if (FloatCommand == defaultFloatCommand)
        {
            ClearValue(FloatCommandProperty);
        }

        if (DockAsDocumentCommand == defaultDockAsDocumentCommand)
        {
            ClearValue(DockAsDocumentCommandProperty);
        }

        if (CloseAllButThisCommand == defaultCloseAllButThisCommand)
        {
            ClearValue(CloseAllButThisCommandProperty);
        }

        if (CloseAllCommand == defaultCloseAllCommand)
        {
            ClearValue(CloseAllCommandProperty);
        }

        if (ActivateCommand == defaultActivateCommand)
        {
            ClearValue(ActivateCommandProperty);
        }

        if (NewVerticalTabGroupCommand == defaultNewVerticalTabGroupCommand)
        {
            ClearValue(NewVerticalTabGroupCommandProperty);
        }

        if (NewHorizontalTabGroupCommand == defaultNewHorizontalTabGroupCommand)
        {
            ClearValue(NewHorizontalTabGroupCommandProperty);
        }

        if (MoveToNextTabGroupCommand == defaultMoveToNextTabGroupCommand)
        {
            ClearValue(MoveToNextTabGroupCommandProperty);
        }

        if (MoveToPreviousTabGroupCommand == defaultMoveToPreviousTabGroupCommand)
        {
            ClearValue(MoveToPreviousTabGroupCommandProperty);
        }
    }

    /// <summary>
    /// Sets the default bindings.
    /// </summary>
    protected virtual void SetDefaultBindings()
    {
        if (LayoutElement is not { } layoutElement)
        {
            return;
        }

        if (CloseCommand == null)
        {
            CloseCommand = defaultCloseCommand;
        }

        if (FloatCommand == null)
        {
            FloatCommand = defaultFloatCommand;
        }

        if (DockAsDocumentCommand == null)
        {
            DockAsDocumentCommand = defaultDockAsDocumentCommand;
        }

        if (CloseAllButThisCommand == null)
        {
            CloseAllButThisCommand = defaultCloseAllButThisCommand;
        }

        if (CloseAllCommand == null)
        {
            CloseAllCommand = defaultCloseAllCommand;
        }

        if (ActivateCommand == null)
        {
            ActivateCommand = defaultActivateCommand;
        }

        if (NewVerticalTabGroupCommand == null)
        {
            NewVerticalTabGroupCommand = defaultNewVerticalTabGroupCommand;
        }

        if (NewHorizontalTabGroupCommand == null)
        {
            NewHorizontalTabGroupCommand = defaultNewHorizontalTabGroupCommand;
        }

        if (MoveToNextTabGroupCommand == null)
        {
            MoveToNextTabGroupCommand = defaultMoveToNextTabGroupCommand;
        }

        if (MoveToPreviousTabGroupCommand == null)
        {
            MoveToPreviousTabGroupCommand = defaultMoveToPreviousTabGroupCommand;
        }

        IsSelected = LayoutElement?.IsSelected == true;
        IsActive = LayoutElement?.IsActive == true;

        // WinUI has no SetCurrentValue; model synchronization uses the dependency property.
        SetModelValue(CanCloseProperty, layoutElement.CanClose);
    }

    /// <summary>
    /// Raises the visibility changed event.
    /// </summary>
    protected virtual void OnVisibilityChanged()
    {
        if (LayoutElement != null && Visibility == Visibility.Collapsed)
        {
            LayoutElement.Close();
        }
    }

    /// <summary>
    /// Attaches the handler.
    /// </summary>
    /// <param name="model">The layout model.</param>
    internal virtual void Attach(LayoutContent model)
    {
        if (LayoutElement != null)
        {
            Detach();
        }

        LayoutElement = model;
        logicalOwner = model.Root?.Manager;
        logicalContent = model.Content as UIElement;
        Model = model.Content;
        InitDefaultCommands();
        LayoutElement.IsSelectedChanged += LayoutElement_IsSelectedChanged;
        LayoutElement.IsActiveChanged += LayoutElement_IsActiveChanged;
        LayoutElement.PropertyChanged += LayoutElement_PropertyChanged;
        DataContext = this;
    }

    /// <summary>
    /// Detaches the handler.
    /// </summary>
    internal virtual void Detach()
    {
        if (LayoutElement == null)
        {
            return;
        }

        logicalOwner?.InternalRemoveLogicalChild(logicalContent);
        logicalOwner?.InternalRemoveLogicalChild(view);
        logicalOwner = null;
        logicalContent = null;
        LayoutElement.IsSelectedChanged -= LayoutElement_IsSelectedChanged;
        LayoutElement.IsActiveChanged -= LayoutElement_IsActiveChanged;
        LayoutElement.PropertyChanged -= LayoutElement_PropertyChanged;
        ClearDefaultBindings();
        ReleaseStyleBindings();
        if (view != null)
        {
            view.ClearValue(ContentPresenter.ContentProperty);
            view.Content = null;
            view.ClearValue(ContentPresenter.ContentTemplateProperty);
            view.ClearValue(ContentPresenter.ContentTemplateSelectorProperty);
            view = null;
        }
        LayoutElement = null;
        Model = null;
    }

    /// <summary>
    /// Clear default bindings.
    /// </summary>
    internal void ClearDefaultBindingsForManager() => ClearDefaultBindings();

    /// <summary>
    /// Set default bindings.
    /// </summary>
    internal void SetDefaultBindingsForManager() => SetDefaultBindings();

    /// <summary>
    /// Is view exists.
    /// </summary>
    /// <returns>true if the instance is view exists; otherwise, false.</returns>
    internal bool IsViewExists() => view != null;

    private void LayoutElement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LayoutContent.Content):
                logicalOwner?.InternalRemoveLogicalChild(logicalContent);
                logicalContent = LayoutElement?.Content as UIElement;
                logicalOwner?.InternalAddLogicalChild(logicalContent);
                break;
        }

        NotifyDefaultCommands();
    }

    internal void NotifyDefaultCommands()
    {
        foreach (ICommand? command in new[] { defaultCloseCommand, defaultFloatCommand, defaultDockAsDocumentCommand,
            defaultCloseAllButThisCommand, defaultCloseAllCommand, defaultActivateCommand,
            defaultNewVerticalTabGroupCommand, defaultNewHorizontalTabGroupCommand,
            defaultMoveToNextTabGroupCommand, defaultMoveToPreviousTabGroupCommand })
        {
            (command as RelayCommand<object>)?.RaiseCanExecuteChanged();
        }
    }

    private void LayoutElement_IsActiveChanged(object? sender, EventArgs e)
    {
        if (!isActiveReentrantFlag.CanEnter)
        {
            return;
        }

        using (isActiveReentrantFlag.Enter())
        {
            IsActive = LayoutElement?.IsActive == true;
        }
    }

    private void LayoutElement_IsSelectedChanged(object? sender, EventArgs e)
    {
        if (!isSelectedReentrantFlag.CanEnter)
        {
            return;
        }

        using (isSelectedReentrantFlag.Enter())
        {
            IsSelected = LayoutElement?.IsSelected == true;
        }
    }

    private static void OnToolTipChanged(DependencyObject s, DependencyPropertyChangedEventArgs e) => ((LayoutItem)s).OnToolTipChanged();

    private void OnToolTipChanged()
    {
        if (LayoutElement != null)
        {
            LayoutElement.ToolTip = ToolTipService.GetToolTip(this);
        }
    }

    private static void OnVisibilityChanged(DependencyObject s, DependencyPropertyChangedEventArgs e) => ((LayoutItem)s).OnVisibilityChanged();
}
