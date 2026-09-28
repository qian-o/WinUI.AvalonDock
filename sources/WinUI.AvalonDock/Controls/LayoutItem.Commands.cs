// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutItem.cs

using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Commands;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using BindableAttribute = System.ComponentModel.BindableAttribute;

namespace AvalonDock.Controls;

public abstract partial class LayoutItem
{
    private ICommand? defaultCloseCommand;
    private ICommand? defaultFloatCommand;
    private ICommand? defaultDockAsDocumentCommand;
    private ICommand? defaultCloseAllButThisCommand;
    private ICommand? defaultCloseAllCommand;
    private ICommand? defaultActivateCommand;
    private ICommand? defaultNewVerticalTabGroupCommand;
    private ICommand? defaultNewHorizontalTabGroupCommand;
    private ICommand? defaultMoveToNextTabGroupCommand;
    private ICommand? defaultMoveToPreviousTabGroupCommand;

    /// <summary>
    /// <see cref="CloseCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseCommandChanged));

    /// <summary>
    /// Gets or sets the close command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the document close button.")]
    [Category("Other")]
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseCommand"/> property.</summary>
    private static void OnCloseCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseCommandChanged(e);

    /// <summary>
    /// Raises the close command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteCloseCommand(object parameter) => LayoutElement != null && LayoutElement.CanClose;

    private void ExecuteCloseCommand(object parameter) => Close();

    /// <summary>
    /// Close.
    /// </summary>
    protected abstract void Close();

    /// <summary>
    /// <see cref="FloatCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FloatCommandProperty = DependencyProperty.Register(nameof(FloatCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnFloatCommandChanged));

    /// <summary>
    /// Gets or sets the float command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when the user clicks the float button.")]
    [Category("Other")]
    public ICommand? FloatCommand
    {
        get => (ICommand?)GetValue(FloatCommandProperty);
        set => SetValue(FloatCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="FloatCommand"/> property.</summary>
    private static void OnFloatCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnFloatCommandChanged(e);

    /// <summary>
    /// Raises the float command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnFloatCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteFloatCommand(object anchorable) =>
        LayoutElement != null
        && LayoutElement.CanFloat
        && LayoutElement.Root?.Manager?.AllowFloatingWindows != false
        && LayoutElement.FindParent<LayoutFloatingWindow>() == null;

    /// <summary>Executes to float the content of this LayoutItem in a separate <see cref="LayoutFloatingWindowControl"/>.</summary>
    /// <param name="parameter">The command parameter.</param>
    private void ExecuteFloatCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteFloatCommand(content);
        }
    }

    /// <summary>
    /// <see cref="DockAsDocumentCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DockAsDocumentCommandProperty = DependencyProperty.Register(nameof(DockAsDocumentCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnDockAsDocumentCommandChanged));

    /// <summary>
    /// Gets or sets the dock as document command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user click the DockAsDocument button.")]
    [Category("Other")]
    public ICommand? DockAsDocumentCommand
    {
        get => (ICommand?)GetValue(DockAsDocumentCommandProperty);
        set => SetValue(DockAsDocumentCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="DockAsDocumentCommand"/> property.</summary>
    private static void OnDockAsDocumentCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnDockAsDocumentCommandChanged(e);

    /// <summary>
    /// Raises the dock as document command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDockAsDocumentCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    /// <summary>
    /// Determines whether the dock as document command can execute.
    /// </summary>
    /// <returns>true if the instance can execute dock as document command; otherwise, false.</returns>
    protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement != null && LayoutElement.FindParent<LayoutDocumentPane>() == null;

    private bool CanExecuteDockAsDocumentCommand(object parameter) => CanExecuteDockAsDocumentCommand();

    private void ExecuteDockAsDocumentCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteDockAsDocumentCommand(content);
        }
    }

    /// <summary>
    /// <see cref="CloseAllButThisCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseAllButThisCommandProperty = DependencyProperty.Register(nameof(CloseAllButThisCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseAllButThisCommandChanged));

    /// <summary>
    /// Gets or sets the close all but this command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the the 'Close All But This' command.")]
    [Category("Other")]
    public ICommand? CloseAllButThisCommand
    {
        get => (ICommand?)GetValue(CloseAllButThisCommandProperty);
        set => SetValue(CloseAllButThisCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseAllButThisCommand"/> property.</summary>
    private static void OnCloseAllButThisCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseAllButThisCommandChanged(e);

    /// <summary>
    /// Raises the close all but this command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseAllButThisCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteCloseAllButThisCommand(object parameter)
    {
        ILayoutRoot? root = LayoutElement?.Root;
        if (root == null)
        {
            return false;
        }

        return root.Manager?.Layout.Descendents().OfType<LayoutContent>().Any(d => d != LayoutElement && (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow)) == true;
    }

    private void ExecuteCloseAllButThisCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteCloseAllButThisCommand(content);
        }
    }

    /// <summary>
    /// <see cref="CloseAllCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CloseAllCommandProperty = DependencyProperty.Register(nameof(CloseAllCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnCloseAllCommandChanged));

    /// <summary>
    /// Gets or sets the close all command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the 'Close All' command.")]
    [Category("Other")]
    public ICommand? CloseAllCommand
    {
        get => (ICommand?)GetValue(CloseAllCommandProperty);
        set => SetValue(CloseAllCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="CloseAllCommand"/> property.</summary>
    private static void OnCloseAllCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnCloseAllCommandChanged(e);

    /// <summary>
    /// Raises the close all command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnCloseAllCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteCloseAllCommand(object parameter)
    {
        ILayoutRoot? root = LayoutElement?.Root;
        if (root == null)
        {
            return false;
        }

        return root.Manager?.Layout.Descendents().OfType<LayoutContent>().Any(d => d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow) == true;
    }

    private void ExecuteCloseAllCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteCloseAllCommand(content);
        }
    }

    /// <summary>
    /// <see cref="ActivateCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActivateCommandProperty = DependencyProperty.Register(nameof(ActivateCommand), typeof(ICommand), typeof(LayoutItem),
            new PropertyMetadata(null, OnActivateCommandChanged));

    /// <summary>
    /// Gets or sets the activate command.
    /// </summary>
    [BindableAttribute(true)]
    [Description("Gets/sets the command to execute when user wants to activate a content (either a Document or an Anchorable).")]
    [Category("Other")]
    public ICommand? ActivateCommand
    {
        get => (ICommand?)GetValue(ActivateCommandProperty);
        set => SetValue(ActivateCommandProperty, value);
    }

    /// <summary>Handles changes to the <see cref="ActivateCommand"/> property.</summary>
    private static void OnActivateCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LayoutItem)d).OnActivateCommandChanged(e);

    /// <summary>
    /// Raises the activate command changed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnActivateCommandChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private bool CanExecuteActivateCommand(object parameter) => LayoutElement != null;

    private void ExecuteActivateCommand(object parameter)
    {
        if (LayoutElement is { Root.Manager: { } manager } content)
        {
            manager.ExecuteContentActivateCommand(content);
        }
    }

    /// <summary>
    /// Init default commands.
    /// </summary>
    protected virtual void InitDefaultCommands()
    {
        defaultCloseCommand = new RelayCommand<object>(ExecuteCloseCommand, CanExecuteCloseCommand);
        defaultFloatCommand = new RelayCommand<object>(ExecuteFloatCommand, CanExecuteFloatCommand);
        defaultDockAsDocumentCommand = new RelayCommand<object>(ExecuteDockAsDocumentCommand, CanExecuteDockAsDocumentCommand);
        defaultCloseAllButThisCommand = new RelayCommand<object>(ExecuteCloseAllButThisCommand, CanExecuteCloseAllButThisCommand);
        defaultCloseAllCommand = new RelayCommand<object>(ExecuteCloseAllCommand, CanExecuteCloseAllCommand);
        defaultActivateCommand = new RelayCommand<object>(ExecuteActivateCommand, CanExecuteActivateCommand);
        defaultNewVerticalTabGroupCommand = new RelayCommand<object>(ExecuteNewVerticalTabGroupCommand, CanExecuteNewVerticalTabGroupCommand);
        defaultNewHorizontalTabGroupCommand = new RelayCommand<object>(ExecuteNewHorizontalTabGroupCommand, CanExecuteNewHorizontalTabGroupCommand);
        defaultMoveToNextTabGroupCommand = new RelayCommand<object>(ExecuteMoveToNextTabGroupCommand, CanExecuteMoveToNextTabGroupCommand);
        defaultMoveToPreviousTabGroupCommand = new RelayCommand<object>(ExecuteMoveToPreviousTabGroupCommand, CanExecuteMoveToPreviousTabGroupCommand);
    }

    /// <summary>
    /// Clear default bindings.
    /// </summary>
    protected virtual void ClearDefaultBindings()
    {
        if (CloseCommand == defaultCloseCommand)
        {
            ClearValue(CloseCommandProperty);
        }

        if (FloatCommand == defaultFloatCommand)
        {
            ClearValue(FloatCommandProperty);
        }

        if (DockAsDocumentCommand == defaultDockAsDocumentCommand)
        {
            ClearValue(DockAsDocumentCommandProperty);
        }

        if (CloseAllButThisCommand == defaultCloseAllButThisCommand)
        {
            ClearValue(CloseAllButThisCommandProperty);
        }

        if (CloseAllCommand == defaultCloseAllCommand)
        {
            ClearValue(CloseAllCommandProperty);
        }

        if (ActivateCommand == defaultActivateCommand)
        {
            ClearValue(ActivateCommandProperty);
        }

        if (NewVerticalTabGroupCommand == defaultNewVerticalTabGroupCommand)
        {
            ClearValue(NewVerticalTabGroupCommandProperty);
        }

        if (NewHorizontalTabGroupCommand == defaultNewHorizontalTabGroupCommand)
        {
            ClearValue(NewHorizontalTabGroupCommandProperty);
        }

        if (MoveToNextTabGroupCommand == defaultMoveToNextTabGroupCommand)
        {
            ClearValue(MoveToNextTabGroupCommandProperty);
        }

        if (MoveToPreviousTabGroupCommand == defaultMoveToPreviousTabGroupCommand)
        {
            ClearValue(MoveToPreviousTabGroupCommandProperty);
        }
    }

    /// <summary>
    /// Sets the default bindings.
    /// </summary>
    protected virtual void SetDefaultBindings()
    {
        if (LayoutElement is not { } layoutElement)
        {
            return;
        }

        if (CloseCommand == null)
        {
            CloseCommand = defaultCloseCommand;
        }

        if (FloatCommand == null)
        {
            FloatCommand = defaultFloatCommand;
        }

        if (DockAsDocumentCommand == null)
        {
            DockAsDocumentCommand = defaultDockAsDocumentCommand;
        }

        if (CloseAllButThisCommand == null)
        {
            CloseAllButThisCommand = defaultCloseAllButThisCommand;
        }

        if (CloseAllCommand == null)
        {
            CloseAllCommand = defaultCloseAllCommand;
        }

        if (ActivateCommand == null)
        {
            ActivateCommand = defaultActivateCommand;
        }

        if (NewVerticalTabGroupCommand == null)
        {
            NewVerticalTabGroupCommand = defaultNewVerticalTabGroupCommand;
        }

        if (NewHorizontalTabGroupCommand == null)
        {
            NewHorizontalTabGroupCommand = defaultNewHorizontalTabGroupCommand;
        }

        if (MoveToNextTabGroupCommand == null)
        {
            MoveToNextTabGroupCommand = defaultMoveToNextTabGroupCommand;
        }

        if (MoveToPreviousTabGroupCommand == null)
        {
            MoveToPreviousTabGroupCommand = defaultMoveToPreviousTabGroupCommand;
        }

        IsSelected = LayoutElement?.IsSelected == true;
        IsActive = LayoutElement?.IsActive == true;

        // WinUI has no SetCurrentValue; model synchronization uses the dependency property.
        SetModelValue(CanCloseProperty, layoutElement.CanClose);
    }

    /// <summary>
    /// Clear default bindings.
    /// </summary>
    internal void ClearDefaultBindingsForManager() => ClearDefaultBindings();

    /// <summary>
    /// Set default bindings.
    /// </summary>
    internal void SetDefaultBindingsForManager() => SetDefaultBindings();

    internal void NotifyDefaultCommands()
    {
        // 先保存快照：事件处理器可能在通知期间重新附加此项并替换默认命令。
        var commands = (Close: defaultCloseCommand, Float: defaultFloatCommand,
            DockAsDocument: defaultDockAsDocumentCommand, CloseOthers: defaultCloseAllButThisCommand,
            CloseAll: defaultCloseAllCommand, Activate: defaultActivateCommand,
            NewVertical: defaultNewVerticalTabGroupCommand, NewHorizontal: defaultNewHorizontalTabGroupCommand,
            MoveNext: defaultMoveToNextTabGroupCommand, MovePrevious: defaultMoveToPreviousTabGroupCommand);
        NotifyDefaultCommand(commands.Close);
        NotifyDefaultCommand(commands.Float);
        NotifyDefaultCommand(commands.DockAsDocument);
        NotifyDefaultCommand(commands.CloseOthers);
        NotifyDefaultCommand(commands.CloseAll);
        NotifyDefaultCommand(commands.Activate);
        NotifyDefaultCommand(commands.NewVertical);
        NotifyDefaultCommand(commands.NewHorizontal);
        NotifyDefaultCommand(commands.MoveNext);
        NotifyDefaultCommand(commands.MovePrevious);
    }

    private static void NotifyDefaultCommand(ICommand? command) => (command as RelayCommand<object>)?.RaiseCanExecuteChanged();
}
