// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutDocumentFloatingWindow.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout document floating window.
/// </summary>
[ContentProperty(Name = nameof(RootPanel))]
[Serializable]
public class LayoutDocumentFloatingWindow : LayoutFloatingWindow, ILayoutElementWithVisibility
{
    private LayoutDocumentPaneGroup? rootPanel;

    [NonSerialized]
    private bool isVisible = true;

    /// <summary>
    /// Occurs when the is visible changed event is raised.
    /// </summary>
    public event EventHandler? IsVisibleChanged;

    /// <summary>
    /// Gets or sets the root panel.
    /// </summary>
    public LayoutDocumentPaneGroup? RootPanel
    {
        get => rootPanel;
        set
        {
            if (rootPanel == value)
            {
                return;
            }

            if (rootPanel != null)
            {
                rootPanel.ChildrenTreeChanged -= OnRootPanelChildrenTreeChanged;
            }

            rootPanel = value;
            if (rootPanel != null)
            {
                rootPanel.Parent = this;
            }

            if (rootPanel != null)
            {
                rootPanel.ChildrenTreeChanged += OnRootPanelChildrenTreeChanged;
            }

            RaisePropertyChanged(nameof(RootPanel));
            RaisePropertyChanged(nameof(IsSinglePane));
            RaisePropertyChanged(nameof(SinglePane));
            RaisePropertyChanged(nameof(Children));
            RaisePropertyChanged(nameof(ChildrenCount));
            ((ILayoutElementWithVisibility)this).ComputeVisibility();
        }
    }

    /// <summary>
    /// Executes the root panel children tree changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnRootPanelChildrenTreeChanged(object? sender, ChildrenTreeChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsSinglePane));
        RaisePropertyChanged(nameof(SinglePane));
    }

    /// <summary>
    /// Gets a value indicating whether this instance is single pane.
    /// </summary>
    public bool IsSinglePane => RootPanel?.Descendents().OfType<LayoutDocumentPane>().Count(p => p.IsVisible) == 1;

    /// <summary>
    /// Gets the single pane.
    /// </summary>
    public LayoutDocumentPane? SinglePane
    {
        get
        {
            if (!IsSinglePane)
            {
                return null;
            }

            LayoutDocumentPane singlePane = RootPanel.Descendents().OfType<LayoutDocumentPane>().Single(p => p.IsVisible);
            return singlePane;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is visible.
    /// </summary>
    [XmlIgnore]
    public bool IsVisible
    {
        get => isVisible;
        private set
        {
            if (isVisible == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsVisible));
            isVisible = value;
            RaisePropertyChanged(nameof(IsVisible));
            IsVisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<ILayoutElement> Children
    {
        get
        {
            if (RootPanel is { } panel)
            {
                yield return panel;
            }
        }
    }

    /// <inheritdoc/>
    public override void RemoveChild(ILayoutElement element)
    {
        RootPanel = null;
    }

    /// <inheritdoc/>
    public override void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        RootPanel = newElement as LayoutDocumentPaneGroup;
    }

    /// <inheritdoc/>
    public override int ChildrenCount => RootPanel == null ? 0 : 1;

    /// <inheritdoc/>
    void ILayoutElementWithVisibility.ComputeVisibility() => IsVisible = RootPanel != null && RootPanel.IsVisible;

    /// <inheritdoc/>
    public override bool IsValid => RootPanel != null;

}
