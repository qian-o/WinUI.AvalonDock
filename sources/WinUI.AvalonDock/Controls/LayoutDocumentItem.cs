// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentItem.cs

using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout Document Item.
/// </summary>
public class LayoutDocumentItem : LayoutItem
{
    private LayoutDocument? document;   // The content of this item

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentItem"/> class.
    /// </summary>
    internal LayoutDocumentItem()
    {
    }

    /// <summary>
    /// <see cref="Description"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(LayoutDocumentItem),
                new PropertyMetadata(null, OnDescriptionChanged));

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the description to display (in the NavigatorWindow) for the document item.")]
    [Category("Other")]
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Handles the on Description Changed.
    /// </summary>
    /// <param name="d">The d.</param>
    /// <param name="e">The event arguments.</param>
    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutDocumentItem)d).OnDescriptionChanged(e);

    /// <summary>
    /// Handles the on Description Changed.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDescriptionChanged(DependencyPropertyChangedEventArgs e)
    {
        // Native style bindings may clear after Detach has released the document.
        if (document != null)
        {
            document.Description = (string?)e.NewValue;
        }
    }

    /// <inheritdoc/>
    protected override void Close()
    {
        if (document?.Root?.Manager == null)
        {
            return;
        }

        DockingManager dockingManager = document.Root.Manager;
        dockingManager.ExecuteCloseCommand(document);
    }

    /// <inheritdoc/>
    protected override void OnVisibilityChanged()
    {
        if (document?.Root != null)
        {
            document.IsVisible = Visibility == Visibility.Visible;
            if (document.Parent is LayoutDocumentPane layoutDocumentPane)
            {
                layoutDocumentPane.ComputeVisibility();
            }
        }

        base.OnVisibilityChanged();
    }

    /// <inheritdoc/>
    internal override void Attach(LayoutContent model)
    {
        if (LayoutElement != null)
        {
            Detach();
        }

        document = model as LayoutDocument ?? throw new ArgumentException("The model must be a document.", nameof(model));
        base.Attach(model);
    }

    /// <inheritdoc/>
    internal override void Detach()
    {
        document = null;
        base.Detach();
    }

    /// <inheritdoc/>
    protected override bool CanExecuteDockAsDocumentCommand()
    {
        return LayoutElement != null && LayoutElement.FindParent<LayoutDocumentPane>() != null && LayoutElement.IsFloating;
    }
}
