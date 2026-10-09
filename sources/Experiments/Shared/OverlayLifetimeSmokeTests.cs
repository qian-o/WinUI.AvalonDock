using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AvalonDock;
using AvalonDock.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace WinUI.AvalonDock.Experiments.Shared;

/// <summary>Opt-in real-window overlay show/hide/render/close lifetime regression.</summary>
internal static class OverlayLifetimeSmokeTests
{
    private const int TotalCycles = 20;
    private static readonly ConstructorInfo Constructor = typeof(OverlayWindow).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(FrameworkElement), typeof(bool)], null)!;
    private static readonly Type SurfaceType = typeof(DockingManager).Assembly.GetType(
        "AvalonDock.Platforms.Windows.WindowsOverlayWindowSurface", true)!;
    private static readonly FieldInfo RegistryField = SurfaceType.GetField("Windows", BindingFlags.Static | BindingFlags.NonPublic)!;

    internal static bool StartIfRequested(Window window, DockingManager sample, string[] arguments)
    {
        int option = Array.IndexOf(arguments, "--overlay-lifetime");
        if (option < 0) { return false; }
        string output = option + 1 < arguments.Length && !arguments[option + 1].StartsWith("--", StringComparison.Ordinal)
            ? Path.GetFullPath(arguments[option + 1]) : Path.Combine(AppContext.BaseDirectory, "overlay-lifetime.log");
        sample.Loaded += Run;
        return true;

        async void Run(object sender, RoutedEventArgs args)
        {
            sample.Loaded -= Run;
            using StreamWriter log = new(output, false, new UTF8Encoding(false)) { AutoFlush = true };
            try
            {
                // The dedicated destination windows below own every test editor and overlay.
                sample.Dispose();
                window.Content = new Grid();
                await Task.Delay(150);
                await RunAsync(window, log.WriteLine);
                log.WriteLine("RESULT: PASS");
            }
            catch (Exception exception)
            {
                log.WriteLine($"FAIL: {exception}");
                log.WriteLine("RESULT: FAIL");
                Environment.ExitCode = 1;
            }
            finally
            {
                window.Close();
                Application.Current.Exit();
            }
        }
    }

    internal static async Task<IReadOnlyList<string>> RunAsync(Window mainWindow, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        if (!mainWindow.DispatcherQueue.HasThreadAccess)
        {
            throw new ArgumentException("Run overlay checks on the window's UI dispatcher.", nameof(mainWindow));
        }
        List<string> checks = [];
        List<ObservedReference> references = [];
        List<nint> retiredNativeHandles = [];
        List<ResourceSample> samples = [];
        Stopwatch elapsed = Stopwatch.StartNew();
        Write($"INFO: overlay lifetime pid={Environment.ProcessId}, utc={DateTime.UtcNow:O}, cycles={TotalCycles}; test-owned destination windows and editors, no keyboard or mouse injection.");
        Require(RegisteredWindows.Count == 0, "overlay native registry starts empty");
        await Collect();
        Sample("initial", 0);
        try
        {
            for (int cycle = 0; cycle < TotalCycles; cycle++)
            {
                CycleResult result = await RunCycle(cycle, Write);
                references.AddRange(result.References);
                retiredNativeHandles.AddRange(result.NativeHandles);
                if (cycle + 1 is 4 or 12 or TotalCycles)
                {
                    mainWindow.Activate();
                    await Collect();
                    Sample($"after {cycle + 1} closed overlay/destination pairs and GC", cycle + 1);
                }
            }
            foreach (ResourceSample sample in samples.Skip(1))
            {
                Require(sample.RegisteredWindows == 0 && sample.NativeWindows == 0,
                    $"closed overlays leave no registered or native HWND ({sample.Stage})");
                Require(sample.References.All(reference => reference.Alive <= 1),
                    $"closed overlay, template, surface, destination and editor references do not accumulate ({sample.Stage}; at most one debugger-local survivor per kind)");
            }
            for (int index = 2; index < samples.Count; index++)
            {
                ResourceSample previous = samples[index - 1];
                ResourceSample current = samples[index];
                Require((long)current.Gdi - previous.Gdi <= 32,
                    $"overlay GDI growth stays bounded ({previous.Stage} to {current.Stage}, limit 32)");
                Require((long)current.User - previous.User <= 16,
                    $"overlay USER growth stays bounded ({previous.Stage} to {current.Stage}, limit 16)");
            }
            Write($"PASS: {TotalCycles} overlay Show/Hide/Show/render/Close cycles completed in {elapsed.Elapsed.TotalSeconds:F2} seconds. Resource limits are controlled-environment regression checks, not public API or platform-independent stability guarantees.");
            return checks;
        }
        finally { mainWindow.Activate(); }

        void Write(string line) { checks.Add(line); report?.Invoke(line); }
        void Sample(string stage, int cycles)
        {
            ResourceSample sample = ReadResources(stage, cycles, elapsed.Elapsed, references, retiredNativeHandles);
            samples.Add(sample);
            Write(sample.Description);
        }
        void Require(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Overlay lifetime: {description}{Environment.NewLine}"
                    + string.Join(Environment.NewLine, samples.Select(sample => sample.Description)));
            }
            Write($"PASS: {description}");
        }
    }

    private static async Task<CycleResult> RunCycle(int cycle, Action<string> report)
    {
        TextBox editor = new() { Text = $"Overlay destination editor {cycle}", AcceptsReturn = true };
        Grid destination = new();
        destination.Children.Add(editor);
        Window owner = new() { Title = $"AvalonDock overlay verification {cycle}", Content = destination };
        OverlayWindow? overlay = null;
        nint ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        nint overlayHandle = 0;
        try
        {
            owner.Activate();
            await WaitUntil(() => destination.IsLoaded && editor.IsLoaded && destination.ActualWidth > 0 && destination.ActualHeight > 0,
                $"cycle {cycle}: independent destination loads");
            if (!editor.Focus(FocusState.Programmatic)) { throw new InvalidOperationException($"Overlay cycle {cycle}: editor rejected focus."); }
            await WaitUntil(() => ReferenceEquals(FocusManager.GetFocusedElement(editor.XamlRoot), editor), $"cycle {cycle}: editor focus");
            overlay = (OverlayWindow)Constructor.Invoke([destination, false]);
            overlayHandle = WinRT.Interop.WindowNative.GetWindowHandle(overlay);
            object view = ReadField(overlay, "view");
            object surface = ReadField(overlay, "surface");
            List<ObservedReference> references =
            [
                new("overlay", new WeakReference(overlay)), new("template-view", new WeakReference(view)),
                new("surface", new WeakReference(surface)), new("destination", new WeakReference(destination)),
                new("destination-window", new WeakReference(owner)), new("editor", new WeakReference(editor))
            ];
            overlay.Show();
            await WaitForRender(overlay, cycle);
            CheckFocus("first Show");
            overlay.Hide();
            await WaitUntil(() => !ReadProperty<bool>(overlay, "IsVisible"), $"cycle {cycle}: overlay hides");
            CheckFocus("Hide");
            overlay.Show();
            await WaitForRender(overlay, cycle);
            CheckFocus("second Show");
            if (cycle == 0)
            {
                int nativeClosing = 0;
                void CancelNativeClose(AppWindow sender, AppWindowClosingEventArgs args) { nativeClosing++; args.Cancel = true; }
                overlay.AppWindow.Closing += CancelNativeClose;
                try
                {
                    // Deliver the normal native close request only to this test-owned HWND.
                    // The externally observed cancellation tests WinUI's base/native path, not OnClosing override semantics.
                    if (!PostMessageW(overlayHandle, 0x0010, 0, 0)) { throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
                    await WaitUntil(() => nativeClosing > 0, "test-owned overlay receives native WM_CLOSE request");
                    await Task.Delay(80);
                    if (ReadProperty<bool>(overlay, "IsClosed") || !IsWindow(overlayHandle) || !ReadProperty<bool>(overlay, "IsVisible"))
                    {
                        throw new InvalidOperationException("Cancelling the test-owned overlay's native close did not preserve its live visible window.");
                    }
                    CheckFocus("cancelled native Close");
                    report("PASS: external AppWindow.Closing cancellation preserves the test-owned overlay native window and editor focus; public OverlayWindow.Close semantics were not replaced.");
                }
                finally { overlay.AppWindow.Closing -= CancelNativeClose; }
            }
            overlay.Close();
            await WaitUntil(() => ReadProperty<bool>(overlay, "IsClosed") && !IsWindow(overlayHandle)
                && !RegisteredWindows.Contains(overlayHandle), $"cycle {cycle}: overlay closes and unregisters");
            if (ReadProperty<object?>(overlay, "RenderFailure") != null)
            {
                throw new InvalidOperationException($"Overlay cycle {cycle}: close retained a render failure.");
            }
            owner.Close();
            await WaitUntil(() => !IsWindow(ownerHandle) && !destination.IsLoaded && !editor.IsLoaded,
                $"cycle {cycle}: independent destination window closes and unloads");
            report($"PASS: overlay cycle {cycle} Show/Hide/Show renders without failure, preserves editor focus, and closes both owned HWNDs");
            return new CycleResult(references, [overlayHandle, ownerHandle]);

            void CheckFocus(string operation)
            {
                if (!ReferenceEquals(FocusManager.GetFocusedElement(editor.XamlRoot), editor)
                    || GetForegroundWindow() != ownerHandle)
                {
                    throw new InvalidOperationException($"Overlay cycle {cycle}: {operation} changed editor focus or activated another native window.");
                }
            }
        }
        finally
        {
            if (overlay != null && !ReadProperty<bool>(overlay, "IsClosed")) { overlay.Close(); }
            if (IsWindow(ownerHandle)) { owner.Close(); }
        }
    }

    private static async Task WaitForRender(OverlayWindow overlay, int cycle)
    {
        await WaitUntil(() => ReadProperty<object?>(overlay, "RenderFailure") != null
            || ReadProperty<bool>(overlay, "IsVisible") && ((FrameworkElement)ReadField(overlay, "view")).IsLoaded
                && (int)ReadField(overlay, "renderedRevision") == (int)ReadField(overlay, "revision")
                && !(bool)ReadField(overlay, "rendering"), $"cycle {cycle}: visible overlay finishes rendering");
        if (ReadProperty<object?>(overlay, "RenderFailure") is Exception failure)
        {
            throw new InvalidOperationException($"Overlay cycle {cycle}: RenderFailure", failure);
        }
    }

    private static object ReadField(OverlayWindow overlay, string name)
        => typeof(OverlayWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
    private static T ReadProperty<T>(OverlayWindow overlay, string name)
        => (T)typeof(OverlayWindow).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
    private static HashSet<nint> RegisteredWindows => (HashSet<nint>)RegistryField.GetValue(null)!;

    private static async Task Collect()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            await Task.Delay(100);
            GC.Collect();
            await Task.Run(GC.WaitForPendingFinalizers);
            GC.Collect();
        }
    }

    private static ResourceSample ReadResources(string stage, int cycles, TimeSpan elapsed,
        IReadOnlyList<ObservedReference> references, IReadOnlyList<nint> nativeHandles)
    {
        using Process process = Process.GetCurrentProcess();
        if (!GetProcessHandleCount(process.Handle, out uint handles)) { throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        process.Refresh();
        return new ResourceSample(stage, cycles, DateTime.UtcNow, elapsed.TotalSeconds, handles,
            GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1), process.PrivateMemorySize64,
            GC.GetTotalMemory(false), RegisteredWindows.Count, nativeHandles.Count(IsWindow),
            references.GroupBy(reference => reference.Kind).Select(group =>
                new ReferenceSample(group.Key, group.Count(reference => reference.Reference.IsAlive), group.Count())).ToArray());
    }

    private static async Task WaitUntil(Func<bool> condition, string description)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        do { await Task.Delay(20); if (condition()) { return; } }
        while (timeout.Elapsed < TimeSpan.FromSeconds(5));
        throw new InvalidOperationException($"Overlay lifetime timed out waiting for {description}.");
    }

    private sealed record ObservedReference(string Kind, WeakReference Reference);
    private sealed record ReferenceSample(string Kind, int Alive, int Total);
    private sealed record CycleResult(IReadOnlyList<ObservedReference> References, IReadOnlyList<nint> NativeHandles);
    private sealed record ResourceSample(string Stage, int Cycles, DateTime Utc, double ElapsedSeconds,
        uint Handles, uint Gdi, uint User, long PrivateBytes, long ManagedBytes, int RegisteredWindows, int NativeWindows,
        IReadOnlyList<ReferenceSample> References)
    {
        internal string Description => FormattableString.Invariant($"INFO: {Stage}: pid={Environment.ProcessId}, utc={Utc:O}, elapsedSeconds={ElapsedSeconds:F2}, cycles={Cycles}, handles={Handles}, GDI={Gdi}, USER={User}, privateMiB={PrivateBytes / 1048576d:F2}, managedMiB={ManagedBytes / 1048576d:F2}, overlayRegistry={RegisteredWindows}, retiredNativeHWNDs={NativeWindows}, alive references [{string.Join(", ", References.Select(reference => $"{reference.Kind}={reference.Alive}/{reference.Total}"))}]");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessHandleCount(nint process, out uint handleCount);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetGuiResources(nint process, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);
}
