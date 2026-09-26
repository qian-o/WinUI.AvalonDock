// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutDocumentPaneGroup.cs

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout document pane group.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutDocumentPaneGroup : LayoutPositionableGroup<ILayoutDocumentPane>, ILayoutDocumentPane, ILayoutOrientableGroup
{
    // WinUI XBF requires a concrete owner for implicit content declared on a generic base.
    // This exposes the original collection instance without changing its API type or state.
    public override System.Collections.ObjectModel.ObservableCollection<ILayoutDocumentPane> Children => base.Children;

    // WPF Horizontal is zero; WinUI Vertical is zero. Preserve the upstream default explicitly.
    private Orientation paneOrientation = Orientation.Horizontal;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentPaneGroup"/> class.
    /// </summary>
    public LayoutDocumentPaneGroup()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentPaneGroup"/> class.
    /// </summary>
    /// <param name="documentPane">The document pane.</param>
    public LayoutDocumentPaneGroup(LayoutDocumentPane documentPane)
    {
        Children.Add(documentPane);
    }

    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    public Orientation Orientation
    {
        get => paneOrientation;
        set
        {
            if (value == paneOrientation)
            {
                return;
            }

            RaisePropertyChanging(nameof(Orientation));
            paneOrientation = value;
            RaisePropertyChanged(nameof(Orientation));
        }
    }

    /// <inheritdoc/>
    protected override bool GetVisibility() => true;

#if TRACE
    /// <inheritdoc />
    public override void ConsoleDump(int tab)
    {
        System.Diagnostics.Trace.Write(new string(' ', tab * 4));
        System.Diagnostics.Trace.WriteLine(string.Format("DocumentPaneGroup({0})", Orientation));

        foreach (LayoutElement child in Children)
        {
            child.ConsoleDump(tab + 1);
        }
    }
#endif

}
