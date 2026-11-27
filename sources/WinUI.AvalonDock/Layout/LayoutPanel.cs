// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutPanel.cs

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutPanel : LayoutPositionableGroup<ILayoutPanelElement>, ILayoutPanelElement, ILayoutOrientableGroup
{
    // WinUI XBF cannot resolve an implicit content member declared on a closed generic
    // base. Preserve the same inherited collection and type through a concrete metadata owner.
    public override System.Collections.ObjectModel.ObservableCollection<ILayoutPanelElement> Children => base.Children;

    // WPF Horizontal is zero; WinUI Vertical is zero. Preserve the upstream default explicitly.
    private Orientation panelOrientation = Orientation.Horizontal;

    public LayoutPanel()
    {
    }

    public LayoutPanel(ILayoutPanelElement firstChild)
    {
        Children.Add(firstChild);
    }

    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    public Orientation Orientation
    {
        get => panelOrientation;
        set
        {
            if (value == panelOrientation)
            {
                return;
            }

            RaisePropertyChanging(nameof(Orientation));
            panelOrientation = value;
            RaisePropertyChanged(nameof(Orientation));
        }
    }

    /// <summary>
    /// Using a DependencyProperty as the backing store for thhe <see cref="CanDock"/> property.
    /// </summary>
    public static readonly DependencyProperty CanDockProperty =
        DependencyProperty.Register("CanDock", typeof(bool),
            typeof(LayoutPanel), new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets a value indicating whether this instance can dock.
    /// </summary>
    public bool CanDock
    {
        get
        {
            return (bool)GetValue(CanDockProperty);
        }
        set
        {
            SetValue(CanDockProperty, value);
        }
    }

    /// <inheritdoc/>
    protected override bool GetVisibility() => Children.Any(c => c.IsVisible);

}
