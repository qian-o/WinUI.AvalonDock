// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorableFloatingWindow.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout anchorable floating window.
/// </summary>
[Serializable]
[ContentProperty(Name = nameof(RootPanel))]
public class LayoutAnchorableFloatingWindow : LayoutFloatingWindow, ILayoutElementWithVisibility
{
    private LayoutAnchorablePaneGroup? rootPanel;

    [NonSerialized]
    private bool isVisible = true;

    /// <summary>
    /// Occurs when the is visible changed event is raised.
    /// </summary>
    public event EventHandler? IsVisibleChanged;

    /// <summary>
    /// Gets a value indicating whether this instance is single pane.
    /// </summary>
    public bool IsSinglePane => RootPanel != null && RootPanel.Descendents().OfType<ILayoutAnchorablePane>().Count(p => p.IsVisible) == 1;

    /// <summary>
    /// Gets a value indicating whether this instance is visible.
    /// </summary>
    [XmlIgnore]
    public bool IsVisible
    {
        get => isVisible;
        private set
        {
            if (value == isVisible)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsVisible));
            isVisible = value;
            RaisePropertyChanged(nameof(IsVisible));
            IsVisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the root panel.
    /// </summary>
    public LayoutAnchorablePaneGroup? RootPanel
    {
        get => rootPanel;
        set
        {
            if (value == rootPanel)
            {
                return;
            }

            RaisePropertyChanging(nameof(RootPanel));
            if (rootPanel != null)
            {
                rootPanel.ChildrenTreeChanged -= OnRootPanelChildrenTreeChanged;
            }

            rootPanel = value;
            if (rootPanel != null)
            {
                rootPanel.Parent = this;
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
    /// Gets the single pane.
    /// </summary>
    public ILayoutAnchorablePane? SinglePane
    {
        get
        {
            if (!IsSinglePane)
            {
                return null;
            }

            LayoutAnchorablePane singlePane = RootPanel.Descendents().OfType<LayoutAnchorablePane>().Single(p => p.IsVisible);
            singlePane.UpdateIsDirectlyHostedInFloatingWindow();
            return singlePane;
        }
    }

    /// <inheritdoc/>
    void ILayoutElementWithVisibility.ComputeVisibility() => ComputeVisibility();

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
        Debug.Assert(ReferenceEquals(element, RootPanel) && element != null);
        RootPanel = null;
    }

    /// <inheritdoc/>
    public override void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        Debug.Assert(ReferenceEquals(oldElement, RootPanel) && oldElement != null);
        RootPanel = newElement as LayoutAnchorablePaneGroup;
    }

    /// <inheritdoc/>
    public override int ChildrenCount => RootPanel == null ? 0 : 1;

    /// <inheritdoc/>
    public override bool IsValid => RootPanel != null;

#if TRACE
    /// <inheritdoc />
    public override void ConsoleDump(int tab)
    {
        System.Diagnostics.Trace.Write(new string(' ', tab * 4));
        System.Diagnostics.Trace.WriteLine("FloatingAnchorableWindow()");

        RootPanel?.ConsoleDump(tab + 1);
    }
#endif

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
    /// Executes the compute visibility operation.
    /// </summary>
    private void ComputeVisibility() => IsVisible = RootPanel != null && RootPanel.IsVisible;
}
