// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class DockingManager
{
    public static readonly DependencyProperty DocumentPaneTemplateProperty = DependencyProperty.Register(
        nameof(DocumentPaneTemplate), typeof(ControlTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnDocumentPaneTemplateChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ControlTemplate´that can be used to render the LayoutDocumentPaneControl.")]
    [System.ComponentModel.Category("Document")]
    public ControlTemplate? DocumentPaneTemplate
    {
        get => (ControlTemplate?)GetValue(DocumentPaneTemplateProperty);
        set => SetValue(DocumentPaneTemplateProperty, value);
    }

    protected virtual void OnDocumentPaneTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
        // The original hook is passive; control templates/styles determine presentation.
    }

    public static readonly DependencyProperty AnchorablePaneTemplateProperty = DependencyProperty.Register(
        nameof(AnchorablePaneTemplate), typeof(ControlTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnAnchorablePaneTemplateChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ControlTemplate used to render LayoutAnchorablePaneControl")]
    [System.ComponentModel.Category("Anchorable")]
    public ControlTemplate? AnchorablePaneTemplate
    {
        get => (ControlTemplate?)GetValue(AnchorablePaneTemplateProperty);
        set => SetValue(AnchorablePaneTemplateProperty, value);
    }

    protected virtual void OnAnchorablePaneTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
        // The original hook is passive; control templates/styles determine presentation.
    }

    public static readonly DependencyProperty DocumentPaneControlStyleProperty = DependencyProperty.Register(
        nameof(DocumentPaneControlStyle), typeof(Style), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnDocumentPaneControlStyleChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the style of a LayoutDocumentPaneControl.")]
    [System.ComponentModel.Category("Document")]
    public Style? DocumentPaneControlStyle
    {
        get => (Style?)GetValue(DocumentPaneControlStyleProperty);
        set => SetValue(DocumentPaneControlStyleProperty, value);
    }

    protected virtual void OnDocumentPaneControlStyleChanged(DependencyPropertyChangedEventArgs e)
    {
        // The original hook is passive; control templates/styles determine presentation.
    }

    public static readonly DependencyProperty AnchorablePaneControlStyleProperty = DependencyProperty.Register(
        nameof(AnchorablePaneControlStyle), typeof(Style), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnAnchorablePaneControlStyleChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the Style to apply to LayoutAnchorablePaneControl.")]
    [System.ComponentModel.Category("Anchorable")]
    public Style? AnchorablePaneControlStyle
    {
        get => (Style?)GetValue(AnchorablePaneControlStyleProperty);
        set => SetValue(AnchorablePaneControlStyleProperty, value);
    }

    protected virtual void OnAnchorablePaneControlStyleChanged(DependencyPropertyChangedEventArgs e)
    {
        // The original hook is passive; control templates/styles determine presentation.
    }

    [System.ComponentModel.Bindable(false)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether the LayoutDocumentPaneControl is virtualizing its tabbed item child controls or not.")]
    [System.ComponentModel.Category("Document")]
    public bool IsVirtualizingDocument { get; set; } = true;
    [System.ComponentModel.Bindable(false)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether the LayoutAnchorablePaneControl is virtualizing its tabbed item child controls or not.")]
    [System.ComponentModel.Category("Anchorable")]
    public bool IsVirtualizingAnchorable { get; set; } = true;

    public static readonly DependencyProperty IgnoreTabControlKeyBindingsProperty = DependencyProperty.Register(
        nameof(IgnoreTabControlKeyBindings), typeof(bool), typeof(DockingManager), new PropertyMetadata(false));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether the standard tab control key bindings are ignored or not.")]
    [System.ComponentModel.Category("Document")]
    public bool IgnoreTabControlKeyBindings
    {
        get => (bool)GetValue(IgnoreTabControlKeyBindingsProperty);
        set => SetValue(IgnoreTabControlKeyBindingsProperty, value);
    }
}
