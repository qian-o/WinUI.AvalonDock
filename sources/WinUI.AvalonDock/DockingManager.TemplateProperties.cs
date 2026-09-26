// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager template metadata.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly Dictionary<DependencyProperty, TemplateValue> templateValues = [];
    private static readonly Dictionary<DependencyProperty, DependencyProperty> TemplateBindingProperties = [];
    private Style? previousTemplateStyle;

    private static bool IsCoercedTemplate(DependencyProperty property) => property == DocumentHeaderTemplateProperty
        || property == AnchorableHeaderTemplateProperty || property == DocumentPaneMenuItemHeaderTemplateProperty
        || property == AnchorableTitleTemplateProperty || property == DocumentTitleTemplateProperty;

    private void InitializeTemplateProperties()
    {
        RegisterPropertyChangedCallback(StyleProperty, (_, _) =>
        {
            Style? previous = previousTemplateStyle;
            previousTemplateStyle = Style;
            foreach (KeyValuePair<DependencyProperty, TemplateValue> pair in templateValues.ToArray())
            {
                if (!HasStyledTemplate(pair.Key, previous) && !HasStyledTemplate(pair.Key))
                {
                    continue;
                }

                bool local = !ReferenceEquals(pair.Value.Local, DependencyProperty.UnsetValue);
                // WPF invalidates old style dependents even with a local base value;
                // a newly styled property leaves an existing local value untouched.
                if (local && !HasStyledTemplate(pair.Key, previous))
                {
                    continue;
                }

                ApplyTemplateValue(pair.Key, pair.Value, local ? pair.Value.Local : StyledTemplate(pair.Key), true);
            }
        });
    }

    // Keep requested values separate from the native effective DP. WinUI does not
    // expose a coercion layer; callers still use the original properties and methods.
    public new object ReadLocalValue(DependencyProperty dp)
    {
        if (!templateValues.TryGetValue(dp, out TemplateValue? state))
        {
            return base.ReadLocalValue(dp);
        }

        return state.BindingProperty is { } source ? base.GetBindingExpression(source) : state.Local!;
    }

    public new BindingExpression GetBindingExpression(DependencyProperty dp) => templateValues.TryGetValue(dp, out TemplateValue? state) && state.BindingProperty is { } source
        ? base.GetBindingExpression(source) : base.GetBindingExpression(dp);

    public new void SetBinding(DependencyProperty dp, BindingBase binding)
    {
        Compatibility.ReadOnlyPropertyGuard.VerifyWritable(dp, AutoHideWindowProperty);
        if (!IsCoercedTemplate(dp))
        {
            base.SetBinding(dp, binding);
            return;
        }
        if (binding is not Binding nativeBinding)
        {
            throw new ArgumentException("A native Binding is required.", nameof(binding));
        }

        TemplateValue state = TemplateState(dp);
        ReleaseTemplateBinding(state);
        DependencyProperty source = TemplateBindingProperty(dp);
        state.BindingProperty = source;
        // The binding remains on the actual manager so Self, namescope and inherited
        // DataContext resolution use the same element as the public property.
        base.SetBinding(source, nativeBinding);
        state.Local = base.GetValue(source);
        ApplyTemplateValue(dp, state, state.Local, true);
    }

    /// <summary>Reevaluates the original manager template coercion rule using its retained base value.</summary>
    public void CoerceValue(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);
        if (!IsCoercedTemplate(dp))
        {
            return;
        }

        if (base.GetBindingExpression(dp)?.ParentBinding is { } binding)
        {
            SetBinding(dp, binding);
            return;
        }
        TemplateValue state = TemplateState(dp);
        ApplyTemplateValue(dp, state, ReferenceEquals(state.Local, DependencyProperty.UnsetValue) ? StyledTemplate(dp) : state.Local, true);
    }

    private TemplateValue TemplateState(DependencyProperty property)
    {
        if (!templateValues.TryGetValue(property, out TemplateValue? state))
        {
            state = new TemplateValue { Local = base.ReadLocalValue(property) };
            templateValues.Add(property, state);
        }
        return state;
    }

    private void SetTemplateValue(DependencyProperty property, object? value)
    {
        if (value != null && value is not DataTemplate)
        {
            throw new ArgumentException("A DataTemplate or null is required.", nameof(value));
        }

        TemplateValue state = TemplateState(property);
        ReleaseTemplateBinding(state);
        state.Local = value;
        ApplyTemplateValue(property, state, value, true);
    }

    private void ClearTemplateValue(DependencyProperty property)
    {
        TemplateValue state = TemplateState(property);
        ReleaseTemplateBinding(state);
        state.Local = DependencyProperty.UnsetValue;
        object? styled = StyledTemplate(property);
        ApplyTemplateValue(property, state, styled, HasStyledTemplate(property));
    }

    private object? StyledTemplate(DependencyProperty property)
    {
        for (Style style = Style; style != null; style = style.BasedOn)
        {
            if (style.Setters.OfType<Setter>().LastOrDefault(setter => setter.Property == property) is { } setter)
            {
                return setter.Value;
            }
        }

        return property.GetMetadata(GetType()).DefaultValue;
    }

    private bool HasStyledTemplate(DependencyProperty property)
        => HasStyledTemplate(property, Style);

    private static bool HasStyledTemplate(DependencyProperty property, Style? style)
    {
        for (; style != null; style = style.BasedOn)
        {
            if (style.Setters.OfType<Setter>().Any(setter => setter.Property == property))
            {
                return true;
            }
        }

        return false;
    }

    private object? CoerceTemplate(DependencyProperty property, object? value)
    {
        DataTemplateSelector? selector = property == DocumentHeaderTemplateProperty ? DocumentHeaderTemplateSelector
            : property == AnchorableHeaderTemplateProperty ? AnchorableHeaderTemplateSelector
            : property == AnchorableTitleTemplateProperty ? AnchorableTitleTemplateSelector
            : property == DocumentTitleTemplateProperty ? DocumentTitleTemplateSelector : DocumentPaneMenuItemHeaderTemplateSelector;
        if (value != null && selector != null)
        {
            return null;
        }

        return property == DocumentPaneMenuItemHeaderTemplateProperty ? value ?? DocumentHeaderTemplate : value;
    }

    private void ApplyTemplateValue(DependencyProperty property, TemplateValue state, object? value, bool coerce)
    {
        object? effective = coerce ? CoerceTemplate(property, value) : value;
        state.Applying++;
        try
        {
            if (ReferenceEquals(state.Local, DependencyProperty.UnsetValue) && ReferenceEquals(effective, StyledTemplate(property)))
            {
                base.ClearValue(property);
            }
            else
            {
                base.SetValue(property, effective);
            }
        }
        finally { state.Applying--; }
    }

    private void OnTemplatePropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        TemplateValue state = TemplateState(args.Property);
        if (state.Restoring)
        {
            return;
        }

        if (state.Applying == 0)
        {
            ReleaseTemplateBinding(state);
            state.Local = base.ReadLocalValue(args.Property);
            if (base.GetBindingExpression(args.Property)?.ParentBinding is { } binding)
            {
                // A native XAML/BindingOperations write bypasses SetBinding. Retain
                // the original Binding before installing the coerced effective value.
                state.Restoring = true;
                try
                {
                    base.SetValue(args.Property, args.OldValue);
                }
                finally { state.Restoring = false; }
                SetBinding(args.Property, binding);
                return;
            }
            bool coerce = !ReferenceEquals(state.Local, DependencyProperty.UnsetValue) || HasStyledTemplate(args.Property) || !ReferenceEquals(previousTemplateStyle, Style);
            object? effective = coerce ? CoerceTemplate(args.Property, args.NewValue) : args.NewValue;
            if (!ReferenceEquals(effective, args.NewValue))
            {
                state.Restoring = true;
                try
                {
                    base.SetValue(args.Property, args.OldValue);
                }
                finally { state.Restoring = false; }
                ApplyTemplateValue(args.Property, state, args.NewValue, coerce);
                return;
            }
        }
        if (args.Property == DocumentHeaderTemplateProperty)
        {
            OnDocumentHeaderTemplateChanged(args);
        }
        else if (args.Property == AnchorableHeaderTemplateProperty)
        {
            OnAnchorableHeaderTemplateChanged(args);
        }
        else if (args.Property == AnchorableTitleTemplateProperty)
        {
            OnAnchorableTitleTemplateChanged(args);
        }
        else if (args.Property == DocumentTitleTemplateProperty)
        {
            OnDocumentTitleTemplateChanged(args);
        }
        else
        {
            OnDocumentPaneMenuItemHeaderTemplateChanged(args);
        }
    }

    protected virtual void OnDocumentHeaderTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }
    protected virtual void OnDocumentHeaderTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue != null && DocumentHeaderTemplate != null)
        {
            DocumentHeaderTemplate = null;
        }

        if (DocumentPaneMenuItemHeaderTemplateSelector == null)
        {
            DocumentPaneMenuItemHeaderTemplateSelector = DocumentHeaderTemplateSelector;
        }
    }
    protected virtual void OnAnchorableHeaderTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }
    protected virtual void OnAnchorableHeaderTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue != null)
        {
            AnchorableHeaderTemplate = null;
        }
    }

    private static DependencyProperty TemplateBindingProperty(DependencyProperty property)
    {
        if (TemplateBindingProperties.TryGetValue(property, out DependencyProperty? input))
        {
            return input;
        }

        string name = property == DocumentHeaderTemplateProperty ? "DocumentHeader" : property == AnchorableHeaderTemplateProperty ? "AnchorableHeader"
            : property == AnchorableTitleTemplateProperty ? "AnchorableTitle" : property == DocumentTitleTemplateProperty ? "DocumentTitle" : "DocumentPaneMenuItemHeader";
        input = DependencyProperty.Register(name + "TemplateBaseValue", typeof(DataTemplate), typeof(DockingManager), new PropertyMetadata(null, (owner, args) =>
        {
            DockingManager manager = (DockingManager)owner;
            TemplateValue state = manager.TemplateState(property);
            if (state.BindingProperty != args.Property)
            {
                return;
            }

            state.Local = args.NewValue;
            manager.ApplyTemplateValue(property, state, args.NewValue, true);
        }));
        TemplateBindingProperties.Add(property, input);
        return input;
    }

    private void ReleaseTemplateBinding(TemplateValue state)
    {
        DependencyProperty? source = state.BindingProperty;
        state.BindingProperty = null;
        if (source != null)
        {
            base.ClearValue(source);
        }
    }

    private sealed class TemplateValue
    {
        internal object? Local;
        internal int Applying;
        internal bool Restoring;
        internal DependencyProperty? BindingProperty;
    }
}
