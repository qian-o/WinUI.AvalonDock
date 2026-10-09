using System.Reflection;
using System.Runtime.InteropServices;
using AvalonDock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WinUI.AvalonDock.Experiments.Shared;

/// <summary>Checks the pinned upstream ShowDialog thread-window semantics on native WinUI windows.</summary>
internal static class ModalCompatibilitySmokeTests
{
    internal static async Task<IReadOnlyList<string>> RunAsync(Window ownerWindow)
    {
        List<string> checks = [];
        Grid owner = new();
        ownerWindow.Content = owner;
        Window peer = new() { Title = "Modal compatibility peer", Content = new Grid() };
        Window alreadyDisabled = new() { Title = "Modal compatibility disabled peer", Content = new Grid() };
        Window modal = new() { Title = "Modal compatibility dialog", Content = new TextBlock { Text = "Thread-modal verification" } };
        nint ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(ownerWindow);
        nint peerHandle = WinRT.Interop.WindowNative.GetWindowHandle(peer);
        nint disabledHandle = WinRT.Interop.WindowNative.GetWindowHandle(alreadyDisabled);
        nint modalHandle = WinRT.Interop.WindowNative.GetWindowHandle(modal);
        IDisposable? host = null;
        bool modalClosed = false;
        modal.Closed += (_, _) => modalClosed = true;
        try
        {
            peer.Activate();
            alreadyDisabled.Activate();
            ownerWindow.Activate();
            await Task.Delay(150);
            Check(IsWindowEnabled(ownerHandle) && IsWindowEnabled(peerHandle), "both thread-owned windows start enabled");
            EnableWindow(disabledHandle, false);
            Check(!IsWindowEnabled(disabledHandle), "test peer is disabled before opening modal");
            SetActiveWindow(ownerHandle);
            Check(GetActiveWindow() == ownerHandle, "owner is the calling thread's active window before modal");
            Type services = typeof(DockingManager).Assembly.GetType("AvalonDock.Platforms.PlatformServices", true)!;
            host = (IDisposable)services.GetMethod("CreateModalWindowHost", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [modal, owner])!;
            Exception? callbackFailure = null;
            bool inspected = false;
            owner.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    inspected = true;
                    Check(!IsWindowEnabled(ownerHandle) && !IsWindowEnabled(peerHandle),
                        "modal disables visible enabled windows on its UI thread as upstream does");
                    Check(!IsWindowEnabled(disabledHandle), "modal keeps already-disabled peer disabled");
                    Check(IsWindowEnabled(modalHandle), "modal itself remains enabled");
                    Check(GetWindow(modalHandle, 4) == ownerHandle, "modal has the native owner used by upstream navigation");
                    SetActiveWindow(modalHandle);
                    Check(GetActiveWindow() == modalHandle, "modal is active on the calling thread before close");
                }
                catch (Exception error)
                {
                    callbackFailure = error;
                }
                finally
                {
                    modal.Close();
                }
            });
            host.GetType().GetMethod("Run")!.Invoke(host, [new Size(320, 100)]);
            if (callbackFailure != null)
            {
                throw callbackFailure;
            }
            Check(inspected && modalClosed, "modal returns after native close");
            Check(IsWindowEnabled(ownerHandle) && IsWindowEnabled(peerHandle), "modal restores only windows it disabled");
            Check(!IsWindowEnabled(disabledHandle), "modal leaves original disabled state unchanged after close");
            Check(GetActiveWindow() == ownerHandle, "modal restores the previous thread-active owner window");
            bool outerDispatcherAlive = false;
            owner.DispatcherQueue.TryEnqueue(() => outerDispatcherAlive = true);
            await Task.Delay(100);
            Check(outerDispatcherAlive, "closing nested modal loop leaves application dispatcher running");
            host.Dispose();
            host = null;
            Window inactiveModal = new() { Title = "Inactive modal close verification", Content = new Grid() };
            bool inactiveModalClosed = false;
            inactiveModal.Closed += (_, _) => inactiveModalClosed = true;
            try
            {
                SetActiveWindow(ownerHandle);
                host = (IDisposable)services.GetMethod("CreateModalWindowHost", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [inactiveModal, owner])!;
                callbackFailure = null;
                owner.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // A peer can become enabled and active through application code.
                        // Closing the inactive dialog must then preserve the current activity.
                        EnableWindow(peerHandle, true);
                        SetActiveWindow(peerHandle);
                        Check(GetActiveWindow() == peerHandle, "peer can become active before an inactive modal closes");
                    }
                    catch (Exception error) { callbackFailure = error; }
                    finally { inactiveModal.Close(); }
                });
                host.GetType().GetMethod("Run")!.Invoke(host, [new Size(320, 100)]);
                if (callbackFailure != null) { throw callbackFailure; }
                Check(inactiveModalClosed && GetActiveWindow() == peerHandle,
                    "closing an inactive modal does not force activation back to the previous owner");
                Check(IsWindowEnabled(ownerHandle) && IsWindowEnabled(peerHandle) && !IsWindowEnabled(disabledHandle),
                    "inactive modal close preserves original window enable states");
            }
            finally
            {
                host?.Dispose();
                host = null;
                if (!inactiveModalClosed) { inactiveModal.Close(); }
            }
            return checks;
        }
        finally
        {
            host?.Dispose();
            EnableWindow(disabledHandle, true);
            if (!modalClosed)
            {
                modal.Close();
            }
            peer.Close();
            alreadyDisabled.Close();
            ownerWindow.Activate();
        }

        void Check(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Modal compatibility: {description}");
            }
            checks.Add($"PASS: {description}");
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint window, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern nint SetActiveWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
}
