// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutItem.cs

using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BindableAttribute = System.ComponentModel.BindableAttribute;

namespace AvalonDock.Controls;

public abstract partial class LayoutItem
{
    /// <summary>
    /// <see cref="NewVerticalTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NewVerticalTabGroupCommandProperty = DependencyProperty.Register(nameof(NewVerticalTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnNewVerticalTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the new vertical tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the new vertical tab group command.")]
    [Category("Other")]
    public ICommand? NewVerticalTabGroupCommand
    {
        get => (ICommand?)GetValue(NewVerticalTabGroupCommandProperty);
        set => SetValue(NewVerticalTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="NewVerticalTabGroupCommand"/> property.</summary>
    private static void OnNewVerticalTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnNewVerticalTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the new vertical tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnNewVerticalTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteNewVerticalTabGroupCommand(object parameter) => CanExecuteNewTabGroup(Orientation.Horizontal);

    private void ExecuteNewVerticalTabGroupCommand(object parameter) => ExecuteNewTabGroup(Orientation.Horizontal);

    /// <summary>
    /// <see cref="NewHorizontalTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NewHorizontalTabGroupCommandProperty = DependencyProperty.Register(nameof(NewHorizontalTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnNewHorizontalTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the new horizontal tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the new horizontal tab group command.")]
    [Category("Other")]
    public ICommand? NewHorizontalTabGroupCommand
    {
        get => (ICommand?)GetValue(NewHorizontalTabGroupCommandProperty);
        set => SetValue(NewHorizontalTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="NewHorizontalTabGroupCommand"/> property.</summary>
    private static void OnNewHorizontalTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnNewHorizontalTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the new horizontal tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnNewHorizontalTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteNewHorizontalTabGroupCommand(object parameter) => CanExecuteNewTabGroup(Orientation.Vertical);

    private void ExecuteNewHorizontalTabGroupCommand(object parameter) => ExecuteNewTabGroup(Orientation.Vertical);

    private bool CanExecuteNewTabGroup(Orientation orientation)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        if (LayoutElement is LayoutDocument layoutDocument && !layoutDocument.CanMove)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        return (parentDocumentGroup == null ||
                  parentDocumentGroup.ChildrenCount == 1 ||
                  parentDocumentGroup.Root?.Manager?.AllowMixedOrientation == true ||
                  parentDocumentGroup.Orientation == orientation) &&
                 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
                 parentDocumentPane.ChildrenCount > 1;
    }

    private void ExecuteNewTabGroup(Orientation orientation)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement)
        {
            return;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = layoutElement.FindParent<LayoutDocumentPaneGroup>();

        if (parentDocumentGroup == null)
        {
            if (parentDocumentPane.Parent is not { } grandParent)
            {
                return;
            }
            parentDocumentGroup = new LayoutDocumentPaneGroup { Orientation = orientation };
            grandParent.ReplaceChild(parentDocumentPane, parentDocumentGroup);
            parentDocumentGroup.Children.Add(parentDocumentPane);
        }

        parentDocumentGroup.Orientation = orientation;
        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        parentDocumentGroup.InsertChildAt(indexOfParentPane + 1, new LayoutDocumentPane(layoutElement));
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }

    /// <summary>
    /// <see cref="MoveToNextTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MoveToNextTabGroupCommandProperty = DependencyProperty.Register(nameof(MoveToNextTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnMoveToNextTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the move to next tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the move to next tab group command.")]
    [Category("Other")]
    public ICommand? MoveToNextTabGroupCommand
    {
        get => (ICommand?)GetValue(MoveToNextTabGroupCommandProperty);
        set => SetValue(MoveToNextTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="MoveToNextTabGroupCommand"/> property.</summary>
    private static void OnMoveToNextTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnMoveToNextTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the move to next tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnMoveToNextTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteMoveToNextTabGroupCommand(object parameter) => CanExecuteMoveToTabGroup(1);

    private void ExecuteMoveToNextTabGroupCommand(object parameter) => ExecuteMoveToTabGroup(1);

    /// <summary>
    /// <see cref="MoveToPreviousTabGroupCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MoveToPreviousTabGroupCommandProperty = DependencyProperty.Register(nameof(MoveToPreviousTabGroupCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnMoveToPreviousTabGroupCommandChanged));

    /// <summary>
    /// Gets or sets the move to previous tab group command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the move to previous tab group command.")]
    [Category("Other")]
    public ICommand? MoveToPreviousTabGroupCommand
    {
        get => (ICommand?)GetValue(MoveToPreviousTabGroupCommandProperty);
        set => SetValue(MoveToPreviousTabGroupCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="MoveToPreviousTabGroupCommand"/> property.</summary>
    private static void OnMoveToPreviousTabGroupCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnMoveToPreviousTabGroupCommandChanged(e);

    /// <summary>
    /// Raises the move to previous tab group command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnMoveToPreviousTabGroupCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteMoveToPreviousTabGroupCommand(object parameter) => CanExecuteMoveToTabGroup(-1);

    private void ExecuteMoveToPreviousTabGroupCommand(object parameter) => ExecuteMoveToTabGroup(-1);

    private bool CanExecuteMoveToTabGroup(int direction)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        LayoutDocumentPaneGroup? parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
        if (parentDocumentGroup == null || LayoutElement.Parent is not LayoutDocumentPane parentDocumentPane
            || parentDocumentGroup.ChildrenCount <= 1)
        {
            return false;
        }

        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        return (direction > 0 ? indexOfParentPane < parentDocumentGroup.ChildrenCount - 1 : indexOfParentPane > 0)
            && parentDocumentGroup.Children[indexOfParentPane + direction] is LayoutDocumentPane;
    }

    private void ExecuteMoveToTabGroup(int direction)
    {
        if (LayoutElement is not { Parent: LayoutDocumentPane parentDocumentPane } layoutElement
            || layoutElement.FindParent<LayoutDocumentPaneGroup>() is not { } parentDocumentGroup)
        {
            return;
        }

        int indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
        int targetIndex = indexOfParentPane + direction;
        if (targetIndex < 0 || targetIndex >= parentDocumentGroup.ChildrenCount
            || parentDocumentGroup.Children[targetIndex] is not LayoutDocumentPane nextDocumentPane)
        {
            return;
        }

        nextDocumentPane.InsertChildAt(0, layoutElement);
        layoutElement.IsActive = true;
        layoutElement.Root?.CollectGarbage();
    }
}
