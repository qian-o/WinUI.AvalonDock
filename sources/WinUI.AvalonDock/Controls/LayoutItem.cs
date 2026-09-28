// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutItem.cs

using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using BindableAttribute = System.ComponentModel.BindableAttribute;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout item.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public abstract partial class LayoutItem : FrameworkElement
{
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


}
