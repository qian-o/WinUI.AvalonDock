// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutAnchorableItem.cs

using System;
using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Commands;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using BindableAttribute = System.ComponentModel.BindableAttribute;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout anchorable item.
/// </summary>
public class LayoutAnchorableItem : LayoutItem
{
    private LayoutAnchorable? anchorable;   // The content of this item
    private ICommand? defaultHideCommand;
    private ICommand? defaultAutoHideCommand;
    private ICommand? defaultDockCommand;
    private ICommand? defaultDetachToWindowCommand;
    private readonly ReentrantFlag visibilityReentrantFlag = new();
    private readonly ReentrantFlag anchorableVisibilityReentrantFlag = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorableItem"/> class.
    /// </summary>
    internal LayoutAnchorableItem()
    {
        SetModelValue(CanCloseProperty, false);
    }

    /// <summary>
    /// <see cref="HideCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HideCommandProperty = DependencyProperty.Register(nameof(HideCommand), typeof(ICommand), typeof(LayoutAnchorableItem),
            new PropertyMetadata(null, OnHideCommandChanged));

    /// <summary>
    /// Gets or sets the hide command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the the command to execute when an anchorable is hidden.")]
    [Category("Other")]
    public ICommand? HideCommand
    {
        get => (ICommand?)GetValue(HideCommandProperty);
        set => SetValue(HideCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="HideCommand"/> property.</summary>
    private static void OnHideCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnHideCommandChanged(e);

    /// <summary>
    /// Raises the hide command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnHideCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteHideCommand(object parameter) => LayoutElement != null && anchorable?.CanHide == true;

    private void ExecuteHideCommand(object parameter) => anchorable?.Root?.Manager?.ExecuteHideCommand(anchorable);

    /// <summary>
    /// <see cref="DetachToWindowCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DetachToWindowCommandProperty = DependencyProperty.Register(nameof(DetachToWindowCommand), typeof(ICommand), typeof(LayoutAnchorableItem),
            new PropertyMetadata(null, OnDetachToWindowCommandChanged));

    /// <summary>
    /// Gets or sets the command that moves this anchorable into a standalone window.
    /// </summary>
    /// <remarks>
    /// Executing it while the anchorable is already detached returns it to the layout, so a single
    /// menu entry toggles the mode.
    /// </remarks>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when the anchorable is moved into a standalone window.")]
    [Category("Other")]
    public ICommand? DetachToWindowCommand
    {
        get => (ICommand?)GetValue(DetachToWindowCommandProperty);
        set => SetValue(DetachToWindowCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="DetachToWindowCommand"/> property.</summary>
    private static void OnDetachToWindowCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnDetachToWindowCommandChanged(e);

    /// <summary>
    /// Raises the detach to window command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDetachToWindowCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteDetachToWindowCommand(object parameter) =>
        LayoutElement != null
        && anchorable?.Root?.Manager != null
        && anchorable.Root.Manager.AllowDetachedWindows
        && anchorable.CanFloat;

    private void ExecuteDetachToWindowCommand(object parameter)
    {
        LayoutAnchorable? model = anchorable;
        DockingManager? manager = model?.Root?.Manager;
        if (manager == null || model == null)
        {
            return;
        }

        if (manager.IsDetached(model))
        {
            manager.ReattachAnchorable(model);
        }
        else
        {
            manager.DetachAnchorableToWindow(model);
        }
    }

    /// <summary>
    /// <see cref="AutoHideCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AutoHideCommandProperty = DependencyProperty.Register(nameof(AutoHideCommand), typeof(ICommand), typeof(LayoutAnchorableItem),
            new PropertyMetadata(null, OnAutoHideCommandChanged));

    /// <summary>
    /// Gets or sets the auto hide command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the auto hide button.")]
    [Category("Other")]
    public ICommand? AutoHideCommand
    {
        get => (ICommand?)GetValue(AutoHideCommandProperty);
        set => SetValue(AutoHideCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="AutoHideCommand"/> property.</summary>
    private static void OnAutoHideCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnAutoHideCommandChanged(e);

    /// <summary>
    /// Raises the auto hide command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnAutoHideCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteAutoHideCommand(object parameter)
    {
        if (LayoutElement == null)
        {
            return false;
        }

        if (LayoutElement.FindParent<LayoutAnchorableFloatingWindow>() != null)
        {
            return false; // is floating
        }

        return anchorable?.CanAutoHide == true;
    }

    private void ExecuteAutoHideCommand(object parameter) => anchorable?.Root?.Manager?.ExecuteAutoHideCommand(anchorable);

    /// <summary>
    /// <see cref="DockCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DockCommandProperty = DependencyProperty.Register(nameof(DockCommand), typeof(ICommand), typeof(LayoutAnchorableItem),
            new PropertyMetadata(null, OnDockCommandChanged));

    /// <summary>
    /// Gets or sets the dock command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the Dock button.")]
    [Category("Other")]
    public ICommand? DockCommand
    {
        get => (ICommand?)GetValue(DockCommandProperty);
        set => SetValue(DockCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="DockCommand"/> property.</summary>
    private static void OnDockCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnDockCommandChanged(e);

    /// <summary>
    /// Raises the dock command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDockCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteDockCommand(object parameter) => LayoutElement?.FindParent<LayoutAnchorableFloatingWindow>() != null;

    private void ExecuteDockCommand(object parameter)
    {
        if (anchorable is { Root.Manager: { } manager } model)
        {
            manager.ExecuteDockCommand(model);
        }
    }

    /// <summary>
    /// <see cref="CanHide"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanHideProperty = DependencyProperty.Register(nameof(CanHide), typeof(bool), typeof(LayoutAnchorableItem), new PropertyMetadata(
        (bool)true,
        OnCanHideChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance can hide.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets or sets whether the user can hide the anchorable item.")]
    [Category("Anchorable")]
    public bool CanHide
    {
        get => (bool?)GetValue(CanHideProperty) ?? false;
        set => SetValue(CanHideProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CanHide"/> property.</summary>
    private static void OnCanHideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnCanHideChanged(e);

    /// <summary>
    /// Raises the can hide changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCanHideChanged(DependencyPropertyChangedEventArgs e)
    {
        if (anchorable != null)
        {
            anchorable.CanHide = (bool)e.NewValue;
        }
    }

    /// <summary>
    /// <see cref="CanMove"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanMoveProperty = DependencyProperty.Register(nameof(CanMove), typeof(bool), typeof(LayoutAnchorableItem), new PropertyMetadata(
        (bool)true,
        OnCanMoveChanged));

    /// <summary>
    /// Gets or sets a value indicating whether this instance can move.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets or sets whether the user can move the anchorable item.")]
    [Category("Anchorable")]
    public bool CanMove
    {
        get => (bool?)GetValue(CanMoveProperty) ?? false;
        set => SetValue(CanMoveProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CanMove"/> property.</summary>
    private static void OnCanMoveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutAnchorableItem)d).OnCanMoveChanged(e);

    /// <summary>
    /// Raises the can move changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCanMoveChanged(DependencyPropertyChangedEventArgs e)
    {
        if (anchorable != null)
        {
            anchorable.CanMove = (bool)e.NewValue;
        }
    }

    /// <inheritdoc/>
    internal override void Attach(LayoutContent model)
    {
        if (LayoutElement != null)
        {
            Detach();
        }

        anchorable = model as LayoutAnchorable ?? throw new ArgumentException("The model must be an anchorable.", nameof(model));
        anchorable.IsVisibleChanged += OnAnchorableIsVisibleChanged;
        anchorable.PropertyChanged += Anchorable_PropertyChanged;
        base.Attach(model);
    }

    /// <inheritdoc/>
    internal override void Detach()
    {
        if (anchorable != null)
        {
            anchorable.IsVisibleChanged -= OnAnchorableIsVisibleChanged;
        }

        if (anchorable != null)
        {
            anchorable.PropertyChanged -= Anchorable_PropertyChanged;
        }

        anchorable = null;
        base.Detach();
    }

    /// <inheritdoc/>
    protected override bool CanExecuteDockAsDocumentCommand()
    {
        bool canExecute = base.CanExecuteDockAsDocumentCommand();
        if (canExecute && anchorable != null)
        {
            return anchorable.CanDockAsTabbedDocument;
        }

        return canExecute;
    }

    /// <inheritdoc/>
    protected override void Close()
    {
        if (anchorable?.Root?.Manager == null)
        {
            return;
        }

        DockingManager dockingManager = anchorable.Root.Manager;
        dockingManager.ExecuteCloseCommand(anchorable);
    }

    /// <inheritdoc/>
    protected override void InitDefaultCommands()
    {
        defaultHideCommand = new RelayCommand<object>(ExecuteHideCommand, CanExecuteHideCommand);
        defaultAutoHideCommand = new RelayCommand<object>(ExecuteAutoHideCommand, CanExecuteAutoHideCommand);
        defaultDockCommand = new RelayCommand<object>(ExecuteDockCommand, CanExecuteDockCommand);
        defaultDetachToWindowCommand = new RelayCommand<object>(ExecuteDetachToWindowCommand, CanExecuteDetachToWindowCommand);
        base.InitDefaultCommands();
    }

    /// <inheritdoc/>
    protected override void ClearDefaultBindings()
    {
        if (HideCommand == defaultHideCommand)
        {
            ClearValue(HideCommandProperty);
        }

        if (AutoHideCommand == defaultAutoHideCommand)
        {
            ClearValue(AutoHideCommandProperty);
        }

        if (DockCommand == defaultDockCommand)
        {
            ClearValue(DockCommandProperty);
        }

        if (DetachToWindowCommand == defaultDetachToWindowCommand)
        {
            ClearValue(DetachToWindowCommandProperty);
        }

        base.ClearDefaultBindings();
    }

    /// <inheritdoc/>
    protected override void SetDefaultBindings()
    {
        if (HideCommand == null)
        {
            HideCommand = defaultHideCommand;
        }

        if (AutoHideCommand == null)
        {
            AutoHideCommand = defaultAutoHideCommand;
        }

        if (DockCommand == null)
        {
            DockCommand = defaultDockCommand;
        }

        if (DetachToWindowCommand == null)
        {
            DetachToWindowCommand = defaultDetachToWindowCommand;
        }

        Visibility = anchorable?.IsVisible == true ? Visibility.Visible : Visibility.Collapsed;
        base.SetDefaultBindings();
    }

    /// <inheritdoc/>
    protected override void OnVisibilityChanged()
    {
        if (anchorable?.Root != null && visibilityReentrantFlag.CanEnter)
        {
            using (visibilityReentrantFlag.Enter())
            {
                switch (Visibility)
                {
                    case Visibility.Collapsed:
                        anchorable.HideAnchorable(false);
                        break;
                    case Visibility.Visible:
                        anchorable.Show();
                        break;
                }
            }
        }

        // WinUI has no Hidden value. Collapsed represents a hidden tool and must not close it.
    }

    private void Anchorable_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyToolCommands();
    }

    internal void NotifyToolCommands()
    {
        foreach (ICommand? command in new[] { defaultHideCommand, defaultAutoHideCommand, defaultDockCommand, defaultDetachToWindowCommand })
        {
            (command as RelayCommand<object>)?.RaiseCanExecuteChanged();
        }
    }

    private void OnAnchorableIsVisibleChanged(object? sender, EventArgs e)
    {
        if (anchorable?.Root == null || !anchorableVisibilityReentrantFlag.CanEnter)
        {
            return;
        }

        using (anchorableVisibilityReentrantFlag.Enter())
        {
            Visibility = anchorable?.IsVisible == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
