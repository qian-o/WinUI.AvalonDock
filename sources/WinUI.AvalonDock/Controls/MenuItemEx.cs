// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/MenuItemEx.cs.
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public class MenuItemEx : MenuFlyoutItem
{
    private bool reentrantFlag = false;
    private Style? sourceStyle;
    private Image? observedIconImage;
    private long iconSourceToken;
    private readonly Dictionary<DependencyProperty, Binding> styleBindings = [];
    public new Style? Style
    {
        get => sourceStyle ?? base.Style; set => ApplyItemStyle(value);
    }
    public MenuItemEx()
    {
        DefaultStyleKey = typeof(MenuItemEx);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/MenuItemEx.xaml");
        Click += (_, _) => OnClick();
        Loaded += (_, _) => UpdateCheckedVisual();
    }
    public static readonly DependencyProperty IsCheckableProperty = DependencyProperty.Register(nameof(IsCheckable), typeof(bool), typeof(MenuItemEx),
        new PropertyMetadata(false));
    public bool IsCheckable
    {
        get => (bool)GetValue(IsCheckableProperty); set => SetValue(IsCheckableProperty, value);
    }
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(MenuItemEx),
        new PropertyMetadata(false, (owner, args) => ((MenuItemEx)owner).OnIsCheckedChanged((bool)args.NewValue)));
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value);
    }
    public event RoutedEventHandler? Checked;
    public event RoutedEventHandler? Unchecked;
    protected virtual void OnClick()
    {
        if (IsCheckable)
        {
            IsChecked = !IsChecked;
        }
    }
    protected virtual void OnChecked(RoutedEventArgs e) => Checked?.Invoke(this, e);
    protected virtual void OnUnchecked(RoutedEventArgs e) => Unchecked?.Invoke(this, e);
    private void OnIsCheckedChanged(bool checkedValue)
    {
        if (checkedValue)
        {
            OnChecked(new RoutedEventArgs());
        }
        else
        {
            OnUnchecked(new RoutedEventArgs());
        }

        UpdateCheckedVisual();
    }
    private void UpdateCheckedVisual() => VisualStateManager.GoToState(this, IsChecked ? "AvalonDockChecked" : "AvalonDockUnchecked", false);
    protected override AutomationPeer OnCreateAutomationPeer() => new MenuItemExAutomationPeer(this);
    private sealed class MenuItemExAutomationPeer(MenuItemEx owner) : MenuFlyoutItemAutomationPeer(owner), IToggleProvider
    {
        protected override object? GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Toggle && owner.IsCheckable ? this : base.GetPatternCore(patternInterface);
        public ToggleState ToggleState => owner.IsChecked ? ToggleState.On : ToggleState.Off;
        public void Toggle() => Invoke();
    }
    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(MenuItemEx),
        new PropertyMetadata(null, (owner, args) => ((MenuItemEx)owner).OnIconTemplateChanged(args)));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the data template for the icon in the menu item.."), Category("Menu")]
    public DataTemplate? IconTemplate
    {
        get => (DataTemplate?)GetValue(IconTemplateProperty); set => SetValue(IconTemplateProperty, value);
    }
    protected virtual void OnIconTemplateChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();
    public static readonly DependencyProperty IconTemplateSelectorProperty = DependencyProperty.Register(nameof(IconTemplateSelector), typeof(DataTemplateSelector), typeof(MenuItemEx),
        new PropertyMetadata(null, (owner, args) => ((MenuItemEx)owner).OnIconTemplateSelectorChanged(args)));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the DataTemplateSelector for the icon in the menu item."), Category("Menu")]
    public DataTemplateSelector? IconTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(IconTemplateSelectorProperty); set => SetValue(IconTemplateSelectorProperty, value);
    }
    protected virtual void OnIconTemplateSelectorChanged(DependencyPropertyChangedEventArgs e) => UpdateIcon();

    // WPF MenuItem accepts arbitrary icon/header objects; native MenuFlyoutItem only
    // supplies IconElement and Text, so retain those inherited entries on this facade.
    public new static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(MenuItemEx),
        new PropertyMetadata(null, (owner, args) => { MenuItemEx item = (MenuItemEx)owner; item.SyncNativeIcon(); if (args.NewValue != null) { item.UpdateIcon(); } }));
    public new object? Icon
    {
        get => GetValue(IconProperty); set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(MenuItemEx),
        new PropertyMetadata(null, (owner, args) => ((MenuItemEx)owner).Text = args.NewValue?.ToString() ?? string.Empty));
    public object? Header
    {
        get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value);
    }
    public static readonly DependencyProperty HeaderTemplateProperty = DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(MenuItemEx), new PropertyMetadata(null));
    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value);
    }
    public static readonly DependencyProperty HeaderTemplateSelectorProperty = DependencyProperty.Register(nameof(HeaderTemplateSelector), typeof(DataTemplateSelector), typeof(MenuItemEx), new PropertyMetadata(null));
    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(HeaderTemplateSelectorProperty); set => SetValue(HeaderTemplateSelectorProperty, value);
    }
    private void UpdateIcon()
    {
        if (reentrantFlag)
        {
            return;
        }

        reentrantFlag = true;
        if (IconTemplateSelector != null)
        {
            DataTemplate dataTemplateToUse = IconTemplateSelector.SelectTemplate(Icon, this);
            if (dataTemplateToUse != null)
            {
                SetTemplateIcon(dataTemplateToUse.LoadContent());
            }
        }
        else if (IconTemplate != null)
        {
            SetTemplateIcon(IconTemplate.LoadContent());
        }

        reentrantFlag = false;
    }
    private void SetTemplateIcon(object icon)
    {
        // LoadContent creates a detached element. Its bindings need the menu
        // item's model even while an empty icon keeps the viewbox collapsed.
        if (icon is FrameworkElement element
            && element.ReadLocalValue(FrameworkElement.DataContextProperty) == DependencyProperty.UnsetValue)
        {
            BindingOperations.SetBinding(element, FrameworkElement.DataContextProperty,
                new Binding { Source = this, Path = new PropertyPath(nameof(DataContext)) });
        }

        Icon = icon;
    }
    private void SyncNativeIcon()
    {
        // The default icon template can produce an Image with no source. Keep that
        // item out of the native icon column until the binding supplies a source.
        if (!ReferenceEquals(observedIconImage, Icon))
        {
            if (observedIconImage is not null)
            {
                observedIconImage.UnregisterPropertyChangedCallback(Image.SourceProperty, iconSourceToken);
            }

            observedIconImage = Icon as Image;
            if (observedIconImage is not null)
            {
                iconSourceToken = observedIconImage.RegisterPropertyChangedCallback(Image.SourceProperty,
                    (_, _) => UpdateNativeIconPlaceholder());
            }
        }

        UpdateNativeIconPlaceholder();
    }
    private void UpdateNativeIconPlaceholder()
    {
        // The retained template renders Icon; the native property reserves its column.
        if (Icon is null or Image { Source: null })
        {
            base.Icon = null;
        }
        else if (base.Icon == null)
        {
            base.Icon = new SymbolIcon { Symbol = Symbol.Document, Opacity = 0 };
        }
    }
    internal void ApplyItemStyle(Style? style)
    {
        if (ReferenceEquals(style, sourceStyle))
        {
            return;
        }

        foreach (KeyValuePair<DependencyProperty, Binding> pair in styleBindings)
        {
            if (ReferenceEquals(GetBindingExpression(pair.Key)?.ParentBinding, pair.Value))
            {
                ClearValue(pair.Key);
            }
        }

        styleBindings.Clear();
        sourceStyle = style;
        Setter[] setters = EnumerateSetters(style).GroupBy(setter => setter.Property).Select(group => group.Last()).ToArray();
        static Binding? BindingFor(Setter setter) => setter.Value as Binding ?? (setter.ReadLocalValue(Setter.ValueProperty) as BindingExpression)?.ParentBinding;
        if (style is not null && setters.Any(setter => BindingFor(setter) != null))
        {
            Style native = new(style.TargetType);
            foreach (Setter? setter in setters.Where(setter => BindingFor(setter) == null))
            {
                native.Setters.Add(new Setter(setter.Property, setter.Value));
            }

            base.Style = native;
        }
        else
        {
            base.Style = style;
        }

        foreach (Setter? setter in setters)
        {
            if (BindingFor(setter) is { } binding && ReadLocalValue(setter.Property) == DependencyProperty.UnsetValue)
            {
                SetBinding(setter.Property, binding);
                styleBindings[setter.Property] = binding;
            }
        }
    }
    internal bool HasStyledHeader => EnumerateSetters(sourceStyle ?? base.Style).Any(setter => setter.Property == HeaderProperty);
    private static IEnumerable<Setter> EnumerateSetters(Style? style)
    {
        if (style == null)
        {
            yield break;
        }

        foreach (Setter setter in EnumerateSetters(style.BasedOn))
        {
            yield return setter;
        }

        foreach (Setter setter in style.Setters.OfType<Setter>())
        {
            yield return setter;
        }
    }
}
