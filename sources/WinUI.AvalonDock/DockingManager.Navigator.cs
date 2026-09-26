using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace AvalonDock;

public partial class DockingManager
{
    private NavigatorWindow? navigatorWindow;
    public static readonly DependencyProperty ShowNavigatorProperty = DependencyProperty.Register(nameof(ShowNavigator), typeof(bool), typeof(DockingManager), new PropertyMetadata(true));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether the navigator window should be shown when the user presses Control + Tab.")]
    [System.ComponentModel.Category("FloatingWindow")]
    public bool ShowNavigator
    {
        get => (bool)GetValue(ShowNavigatorProperty); set => SetValue(ShowNavigatorProperty, value);
    }
    public static readonly DependencyProperty AllowMovingFloatingWindowWithKeyboardProperty = DependencyProperty.Register(
        nameof(AllowMovingFloatingWindowWithKeyboard), typeof(bool), typeof(DockingManager), new PropertyMetadata(false));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether floating windows can be moved using arrow keys when focused.")]
    [System.ComponentModel.Category("FloatingWindow")]
    public bool AllowMovingFloatingWindowWithKeyboard
    {
        get => (bool)GetValue(AllowMovingFloatingWindowWithKeyboardProperty);
        set => SetValue(AllowMovingFloatingWindowWithKeyboardProperty, value);
    }
    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Tab && (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0
            && ShowNavigator && navigatorWindow == null && layoutItems.Count != 0)
        {
            e.Handled = true;
            navigatorWindow = new NavigatorWindow(this);
            try
            {
                navigatorWindow.ShowDialog();
            }
            finally { navigatorWindow = null; }
        }
        base.OnPreviewKeyDown(e);
    }
    internal void HandleNavigatorKey(KeyRoutedEventArgs args) => OnPreviewKeyDown(args);
    internal void FocusNavigatorContent(LayoutContent content)
    {
        if (!ReferenceEquals(content.Root?.Manager, this))
        {
            return;
        }

        if (GetLayoutItemFromModel(content)?.View is not { } view)
        {
            return;
        }
        void Apply()
        {
            // Re-enabling the modal owner may restore its old native focus after the
            // command ran. Complete the accepted command's focus transfer after that.
            if (!ReferenceEquals(content.Root?.Manager, this) || !view.IsLoaded)
            {
                return;
            }

            PlatformServices.Coordinates.ActivateWindow(view);
            if (FocusElementManager.GetLastWindowHandle(content) != null
                || FocusElementManager.GetLastFocusedElement(content) is FrameworkElement { IsLoaded: true, XamlRoot: not null })
            {
                FocusElementManager.SetFocusOnLastElement(content);
            }
            else if (FocusManager.FindFirstFocusableElement(view) is Control focusable)
            {
                PlatformServices.Focus.Focus(focusable, FocusState.Keyboard);
            }
            else
            {
                FocusDroppedContent(content);
            }
        }
        if (view.IsLoaded)
        {
            DispatcherQueue.TryEnqueue(Apply);
        }
        else
        {
            RoutedEventHandler? loaded = null;
            loaded = (_, _) => { view.Loaded -= loaded; Apply(); };
            view.Loaded += loaded;
        }
    }
}
