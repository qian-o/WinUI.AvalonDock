// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager and Themes/generic.xaml menus.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class DockingManager
{
    public static readonly DependencyProperty DocumentContextMenuProperty = DependencyProperty.Register(nameof(DocumentContextMenu), typeof(MenuFlyout), typeof(DockingManager),
        new PropertyMetadata(null, OnContextMenuPropertyChanged));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ContextMenu to show for a document.")]
    [System.ComponentModel.Category("Document")]
    public MenuFlyout? DocumentContextMenu
    {
        get => (MenuFlyout?)GetValue(DocumentContextMenuProperty); set => SetValue(DocumentContextMenuProperty, value);
    }

    private static void OnContextMenuPropertyChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is MenuFlyout menu)
        {
            AvalonDock.Controls.MenuFlyoutContext.AttachResources(menu, ((DockingManager)owner).Resources);
        }
    }
    public static readonly DependencyProperty DocumentPaneMenuItemHeaderTemplateProperty = DependencyProperty.Register(nameof(DocumentPaneMenuItemHeaderTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnTemplatePropertyChanged(args)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplate for the header to display menu dropdown items on a document pane.")]
    [System.ComponentModel.Category("Other")]
    public DataTemplate? DocumentPaneMenuItemHeaderTemplate
    {
        get => (DataTemplate?)GetValue(DocumentPaneMenuItemHeaderTemplateProperty); set => SetValue(DocumentPaneMenuItemHeaderTemplateProperty, value);
    }
    protected virtual void OnDocumentPaneMenuItemHeaderTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }
    public static readonly DependencyProperty DocumentPaneMenuItemHeaderTemplateSelectorProperty = DependencyProperty.Register(nameof(DocumentPaneMenuItemHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnDocumentPaneMenuItemHeaderTemplateSelectorChanged(args)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplateSelector to select a DataTemplate for the menu items shown when user selects the LayoutDocumentPaneControl context menu switch.")]
    [System.ComponentModel.Category("Document")]
    public DataTemplateSelector? DocumentPaneMenuItemHeaderTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(DocumentPaneMenuItemHeaderTemplateSelectorProperty); set => SetValue(DocumentPaneMenuItemHeaderTemplateSelectorProperty, value);
    }
    protected virtual void OnDocumentPaneMenuItemHeaderTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue != null && DocumentPaneMenuItemHeaderTemplate != null)
        {
            DocumentPaneMenuItemHeaderTemplate = null;
        }
    }
    public static readonly DependencyProperty IconContentTemplateProperty = DependencyProperty.Register(nameof(IconContentTemplate), typeof(DataTemplate), typeof(DockingManager), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplate to use on the icon extracted from the layout model.")]
    [System.ComponentModel.Category("Other")]
    public DataTemplate? IconContentTemplate
    {
        get => (DataTemplate?)GetValue(IconContentTemplateProperty); set => SetValue(IconContentTemplateProperty, value);
    }
    public static readonly DependencyProperty IconContentTemplateSelectorProperty = DependencyProperty.Register(nameof(IconContentTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplateSelector to select a DataTemplate for a content icon.")]
    [System.ComponentModel.Category("Other")]
    public DataTemplateSelector? IconContentTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(IconContentTemplateSelectorProperty); set => SetValue(IconContentTemplateSelectorProperty, value);
    }
}
