using System.Runtime.InteropServices;
using Microsoft.UI.Content;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsFocusService : IFocusService
{
    public IDisposable ObserveNativeFocus(Action<UIElement, INativeFocusTarget> focusChanged) => new NativeFocusSubscription(focusChanged);

    private sealed class NativeFocusSubscription : IDisposable
    {
        private readonly WindowHookHandler hook = new();
        private readonly Action<UIElement, INativeFocusTarget> focusChanged;
        internal NativeFocusSubscription(Action<UIElement, INativeFocusTarget> focusChanged)
        {
            this.focusChanged = focusChanged;
            hook.FocusChanged += OnFocusChanged;
            hook.Attach();
        }
        private void OnFocusChanged(object? sender, FocusChangeEventArgs args)
        {
            if (WindowsChildWindowHost.FindNativeHost(args.GotFocusWinHandle) is { } host)
            {
                focusChanged(host.Owner, new NativeFocusTarget(host, args.GotFocusWinHandle));
            }
        }
        public void Dispose()
        {
            hook.Detach();
            hook.FocusChanged -= OnFocusChanged;
        }
    }

    private sealed class NativeFocusTarget(WindowsChildWindowHost host, nint handle) : INativeFocusTarget
    {
        private readonly WeakReference<WindowsChildWindowHost> owner = new(host);
        private readonly int version = host.ConnectionVersion;
        public bool Focus()
        {
            if (!owner.TryGetTarget(out WindowsChildWindowHost? current) || current.ConnectionVersion != version || !current.ContainsNativeChild(handle))
            {
                return false;
            }

            PlatformServices.Coordinates.ActivateWindow(current.Owner);
            SetFocus(handle);
            return GetFocus() == handle;
        }
        [DllImport("user32.dll")] private static extern nint SetFocus(nint handle);
        [DllImport("user32.dll")] private static extern nint GetFocus();
    }
    public bool HasKeyboardFocus(UIElement element) => element.XamlRoot is { } root
        && ReferenceEquals(FocusManager.GetFocusedElement(root), element)
        && GetController(root)?.HasFocus == true;

    public bool Focus(UIElement element, FocusState state)
    {
        if (element is not FrameworkElement { IsLoaded: true, XamlRoot: not null } loaded)
        {
            return false;
        }

        PlatformServices.Coordinates.ActivateWindow(loaded);
        InputFocusController? controller = GetController(loaded.XamlRoot);
        if (controller is null || !controller.HasFocus && !controller.TrySetFocus())
        {
            return false;
        }

        return ReferenceEquals(FocusManager.GetFocusedElement(loaded.XamlRoot), element) || element.Focus(state);
    }

    private static InputFocusController? GetController(XamlRoot root)
    {
        foreach (ContentIsland? island in ContentIsland.FindAllForCurrentThread())
        {
            if (!island.IsClosed && ReferenceEquals(island.Environment, root.ContentIslandEnvironment))
            {
                return InputFocusController.GetForIsland(island);
            }
        }

        return null;
    }
}
