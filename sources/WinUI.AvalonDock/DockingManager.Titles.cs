// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class DockingManager
{
    public static readonly DependencyProperty DocumentTitleTemplateProperty = DependencyProperty.Register(
        nameof(DocumentTitleTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnTemplatePropertyChanged(args)));

    [Bindable(true), Description("Gets or sets the DataTemplate to use for displaying the title of a document."), Category("Document")]
    public DataTemplate? DocumentTitleTemplate
    {
        get => (DataTemplate?)GetValue(DocumentTitleTemplateProperty);
        set => SetValue(DocumentTitleTemplateProperty, value);
    }
    protected virtual void OnDocumentTitleTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    public static readonly DependencyProperty DocumentTitleTemplateSelectorProperty = DependencyProperty.Register(
        nameof(DocumentTitleTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnDocumentTitleTemplateSelectorChanged(args)));

    [Bindable(true), Description("Gets or sets the DataTemplateSelector to use for displaying the DataTemplate of a document's title."), Category("Document")]
    public DataTemplateSelector? DocumentTitleTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(DocumentTitleTemplateSelectorProperty);
        set => SetValue(DocumentTitleTemplateSelectorProperty, value);
    }
    protected virtual void OnDocumentTitleTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue != null)
        {
            DocumentTitleTemplate = null;
        }
    }

    /// <summary>Identifies the anchorable title template dependency property.</summary>
    public static readonly DependencyProperty AnchorableTitleTemplateProperty = DependencyProperty.Register(
        nameof(AnchorableTitleTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnTemplatePropertyChanged(args)));

    /// <summary>Gets or sets the template used for an anchorable title.</summary>
    [Bindable(true)]
    [Description("Gets or sets the DataTemplate to use for the title of an anchorable.")]
    [Category("Anchorable")]
    public DataTemplate? AnchorableTitleTemplate
    {
        get => (DataTemplate?)GetValue(AnchorableTitleTemplateProperty);
        set => SetValue(AnchorableTitleTemplateProperty, value);
    }

    /// <summary>Provides derived managers an opportunity to handle title template changes.</summary>
    protected virtual void OnAnchorableTitleTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>Identifies the anchorable title template selector dependency property.</summary>
    public static readonly DependencyProperty AnchorableTitleTemplateSelectorProperty = DependencyProperty.Register(
        nameof(AnchorableTitleTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (sender, args) => ((DockingManager)sender).OnAnchorableTitleTemplateSelectorChanged(args)));

    /// <summary>Gets or sets the selector used for an anchorable title template.</summary>
    [Bindable(true)]
    [Description("Gets or sets the DataTemplateSelector to use when selecting a DataTemplate for the title of an anchorable.")]
    [Category("Anchorable")]
    public DataTemplateSelector? AnchorableTitleTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(AnchorableTitleTemplateSelectorProperty);
        set => SetValue(AnchorableTitleTemplateSelectorProperty, value);
    }

    /// <summary>Clears the direct template when a title selector becomes active.</summary>
    protected virtual void OnAnchorableTitleTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue != null && AnchorableTitleTemplate != null)
        {
            AnchorableTitleTemplate = null;
        }
    }
}
