using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Content;
using Windows.Graphics;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

/// <summary>Plain Windows App SDK resource baselines, without docking managers or layout models.</summary>
internal static class RuntimeLifetimeSmokeTests
{
    private static string? captionGetterObservation;
    private static string? captionMode;
    internal static void Start(Window owner)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int option = Array.IndexOf(arguments, "--runtime-lifetime");
        bool islands = arguments.Contains("--xaml-source-lifetime", StringComparer.Ordinal);
        bool menus = arguments.Contains("--menu-lifetime", StringComparer.Ordinal);
        bool captions = arguments.Contains("--caption-lifetime", StringComparer.Ordinal);
        bool composite = arguments.Contains("--composite-lifetime", StringComparer.Ordinal);
        bool compositeMenu = !arguments.Contains("--composite-no-menu", StringComparer.Ordinal);
        bool compositeRegions = !arguments.Contains("--composite-no-regions", StringComparer.Ordinal);
        int modeOption = Array.IndexOf(arguments, "--caption-mode");
        if (modeOption >= 0 && modeOption + 1 < arguments.Length)
        {
            captionMode = arguments[modeOption + 1];
            if (captionMode is not ("A" or "B" or "C" or "D" or "E" or "F" or "G")) { throw new ArgumentException("Use caption mode A to G."); }
        }
        int warmupCycles = captionMode == null ? 12 : 5;
        int totalCycles = ReadIntegerOption(arguments, "--lifetime-cycles", captionMode == null ? 102 : 20, warmupCycles, 100000);
        int batchCycles = ReadIntegerOption(arguments, "--lifetime-batch", captionMode == null ? 30 : 5, 1, 100000);
        int idleSeconds = ReadIntegerOption(arguments, "--lifetime-idle-seconds", 0, 0, 3600);
        string? checkpoint = ReadPathOption(arguments, "--lifetime-checkpoint");
        if (islands) { option = Array.IndexOf(arguments, "--xaml-source-lifetime"); }
        if (menus) { option = Array.IndexOf(arguments, "--menu-lifetime"); }
        if (captions) { option = Array.IndexOf(arguments, "--caption-lifetime"); }
        if (composite) { option = Array.IndexOf(arguments, "--composite-lifetime"); }
        string output = option + 1 < arguments.Length && !arguments[option + 1].StartsWith("--", StringComparison.Ordinal) ? arguments[option + 1]
            : Path.Combine(AppContext.BaseDirectory, composite ? "Composite-lifetime.log" : islands ? "XamlSource-lifetime.log" : menus ? "Menu-lifetime.log" : captions ? "Caption-lifetime.log" : "Runtime-lifetime.log");
        FrameworkElement root = (FrameworkElement)owner.Content;
        root.Loaded += Run;

        async void Run(object sender, RoutedEventArgs args)
        {
            root.Loaded -= Run;
            ReferenceTracker windows = new();
            ReferenceTracker editors = new();
            ReferenceTracker sources = new();
            ReferenceTracker flyouts = new();
            SourceDisposeTracker disposal = new();
            Stopwatch duration = Stopwatch.StartNew();
            StreamWriter? log = null;
            StreamWriter? csv = null;
            int completedCycles = 0;
            try
            {
                log = new StreamWriter(output, false, new UTF8Encoding(false)) { AutoFlush = true };
                if (checkpoint != null)
                {
                    csv = new StreamWriter(checkpoint, false, new UTF8Encoding(false)) { AutoFlush = true };
                    csv.WriteLine("pid,utc,elapsedSeconds,stage,cycles,handles,gdi,user,privateBytes,managedBytes,trackedHosts,aliveHosts,trackedContent,aliveContent,releasedHosts,releasedContent,trackedSources,aliveSources,releasedSources,trackedMenus,aliveMenus,releasedMenus,observerTrackedWeakReferences,threadIslands,threadOpenIslands,threadConnectedIslands,threadIslandReadErrors,sourcesDisposed,disposedIslandsClosed,disposedIslandsConnected,disposedIslandReadErrors");
                }
                Write($"INFO: processId={Environment.ProcessId}; startedUtc={DateTime.UtcNow:O}; cycles={totalCycles}; warmup={warmupCycles}; batch={batchCycles}; idleSeconds={idleSeconds}; checkpoint={checkpoint ?? "none"}.");
                Write("INFO: kernel handle totals use documented GetProcessHandleCount; GDI and USER totals use documented GetGuiResources. Per-kernel-object type snapshots require an external diagnostic tool; no undocumented enumeration API is used here.");
                if (composite) { Write($"INFO: composite baseline: one new top-level Window, one DesktopWindowXamlSource, one TextBox moved main tree to island and back, menu={compositeMenu}, inputRegions={compositeRegions}; owned HWND native caption style only. Explicit editor focus at initial, floating and returned stages is stronger than the docking loop's explicit focus sequence."); }
                await Task.Delay(150);
                await Collect();
                Sample("initial");
                if (captionMode != null) { Write($"INFO: caption mode {captionMode}: {totalCycles} cycles; no color setters. A=extends/presenter/regions; B=presenter/regions, extends=false; C=extends/presenter; D=presenter only, no TitleBar access; E=TitleBar.Height only; F=extends only, no TitleBar access; G=remove owned HWND WS_CAPTION and refresh frame, regions, no WinUI title-bar APIs."); }
                for (int cycle = 0; cycle < warmupCycles; cycle++)
                {
                    await RunNextCycle();
                }
                owner.Activate();
                await Collect();
                Sample($"after {warmupCycles} warm-up cycles");
                while (completedCycles < totalCycles)
                {
                    int cyclesInBatch = Math.Min(batchCycles, totalCycles - completedCycles);
                    for (int cycle = 0; cycle < cyclesInBatch; cycle++)
                    {
                        await RunNextCycle();
                    }
                    owner.Activate();
                    await Collect();
                    Sample($"after {completedCycles} cycles");
                }
                for (int idleElapsed = 0; idleElapsed < idleSeconds;)
                {
                    int interval = Math.Min(5, idleSeconds - idleElapsed);
                    await Task.Delay(TimeSpan.FromSeconds(interval));
                    idleElapsed += interval;
                    // Observe queued cleanup without forcing a collection during the idle interval.
                    Sample($"after {idleElapsed} idle seconds without forced GC");
                }
                if (idleSeconds > 0)
                {
                    await Collect();
                    Sample($"after {idleSeconds} idle seconds and final GC");
                }
                duration.Stop();
                if (captionGetterObservation != null) { Write(captionGetterObservation); }
                Write($"PASS: {completedCycles} plain WinUI {(composite ? "Window/island/editor-reparent/menu composite" : islands ? "DesktopWindowXamlSource/TextBox initialize and dispose" : menus ? "MenuFlyout show and hide" : captions ? "Window/custom native caption configure and close" : "Window/TextBox activate and close")} cycles and {idleSeconds} tail idle seconds completed in {duration.Elapsed.TotalSeconds:F2} seconds; no DockingManager or docking layout was constructed.");
                Write("INFO: resource observations are a Windows App SDK runtime baseline, not a cross-environment stability guarantee.");
            }
            catch (Exception exception)
            {
                if (log != null) { Write($"FAIL: completedCycles={completedCycles}: {exception}"); }
                else { File.AppendAllText(output, $"FAIL: {exception}{Environment.NewLine}"); }
                Environment.ExitCode = 1;
            }
            finally
            {
                csv?.Dispose();
                log?.Dispose();
                owner.Close();
                Application.Current.Exit();
            }

            void Write(string line) => log!.WriteLine(line);
            void Sample(string stage)
            {
                ResourceSample sample = ReadResources(stage, completedCycles, duration.Elapsed, windows, editors, sources, flyouts, disposal);
                Write(sample.Description);
                csv?.WriteLine(sample.ToCsv());
            }
            async Task RunNextCycle()
            {
                if (composite)
                {
                    CompositeReferences result = await RunCompositeCycle(owner, completedCycles, compositeMenu, compositeRegions, ObserveDispose);
                    windows.Add(result.Window);
                    editors.Add(result.Editor);
                    sources.Add(result.Source);
                    if (result.Menu != null) { flyouts.Add(result.Menu); }
                }
                else
                {
                    (WeakReference window, WeakReference editor) = islands ? await RunIslandCycle(owner, completedCycles, ObserveDispose)
                        : menus ? await RunMenuCycle(root, completedCycles) : await RunCycle(completedCycles, captions);
                    windows.Add(window);
                    editors.Add(editor);
                }
                completedCycles++;
            }
            void ObserveDispose(int cycle, IslandDisposeObservation observation)
            {
                disposal.Add(observation);
                Write($"INFO: source dispose cycle={cycle}: islandFound={observation.Found}; beforeClosed={Show(observation.BeforeClosed)}; beforeConnected={Show(observation.BeforeConnected)}; afterClosed={Show(observation.AfterClosed)}; afterConnected={Show(observation.AfterConnected)}; readErrors={observation.ReadErrors}. These are observed flags, not a lifecycle contract assertion.");
            }
            static string Show(bool? value) => value?.ToString() ?? "unknown";
        }
    }

    private static async Task<(WeakReference Window, WeakReference Editor)> RunCycle(int cycle, bool captions)
    {
        TextBox editor = new() { Text = $"Plain WinUI editor {cycle}", AcceptsReturn = true };
        Grid content = new();
        content.Children.Add(editor);
        Window window = new() { Title = $"Plain runtime baseline {cycle}", Content = content };
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        InputNonClientPointerSource? input = null;
        RectInt32[] drag = [new(0, 0, 500, 36)];
        RectInt32[] pass = [new(500, 0, 100, 36)];
        void OnRegionsChanged(InputNonClientPointerSource sender, NonClientRegionsChangedEventArgs args)
        {
            if (closed) { return; }
            if ((sender.GetRegionRects(NonClientRegionKind.Caption)?.Length ?? 0) == 0)
            {
                sender.SetRegionRects(NonClientRegionKind.Caption, drag);
            }
            if ((sender.GetRegionRects(NonClientRegionKind.Passthrough)?.Length ?? 0) == 0)
            {
                sender.SetRegionRects(NonClientRegionKind.Passthrough, pass);
            }
        }
        try
        {
            window.Activate();
            await WaitUntil(() => editor.IsLoaded, "plain window content loads");
            if (!editor.Focus(FocusState.Programmatic))
            {
                throw new InvalidOperationException($"Plain window cycle {cycle}: editor refused programmatic focus.");
            }
            if (captions)
            {
                AppWindowTitleBar? titleBar = null;
                if (captionMode is not ("D" or "F" or "G"))
                {
                    titleBar = window.AppWindow.TitleBar;
                    _ = titleBar.Height;
                }
                if (captionMode is not ("D" or "E" or "G")) { window.ExtendsContentIntoTitleBar = captionMode != "B"; }
                if (captionMode is not ("E" or "F" or "G")) { ((OverlappedPresenter)window.AppWindow.Presenter).SetBorderAndTitleBar(true, false); }
                if (captionMode == "G")
                {
                    nint handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    nint style = GetWindowLongPtrW(handle, -16);
                    Marshal.SetLastPInvokeError(0);
                    if (SetWindowLongPtrW(handle, -16, style & ~((nint)0x00C00000)) == 0 && Marshal.GetLastPInvokeError() != 0)
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                    }
                    if (!SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0037))
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                if (captionMode is null or "A" or "B" or "G")
                {
                    input = InputNonClientPointerSource.GetForWindowId(window.AppWindow.Id);
                    input.RegionsChanged += OnRegionsChanged;
                    input.SetRegionRects(NonClientRegionKind.Caption, drag);
                    input.SetRegionRects(NonClientRegionKind.Passthrough, pass);
                }
                for (int repeat = 0; repeat < (captionMode == null ? 8 : 0); repeat++)
                {
                    titleBar!.ButtonForegroundColor = null;
                    titleBar.ButtonInactiveForegroundColor = null;
                    titleBar.ButtonHoverForegroundColor = null;
                    titleBar.ButtonPressedForegroundColor = null;
                    titleBar.ButtonBackgroundColor = null;
                    titleBar.ButtonInactiveBackgroundColor = null;
                    titleBar.ButtonHoverBackgroundColor = null;
                    titleBar.ButtonPressedBackgroundColor = null;
                }
                if (cycle == 0 && titleBar != null)
                {
                    captionGetterObservation = $"INFO: after {(captionMode == null ? 8 : 0)} rounds of null color setters: ButtonForegroundColor={titleBar.ButtonForegroundColor?.ToString() ?? "null"}; ButtonBackgroundColor={titleBar.ButtonBackgroundColor?.ToString() ?? "null"}; ButtonInactiveForegroundColor={titleBar.ButtonInactiveForegroundColor?.ToString() ?? "null"}; ButtonHoverBackgroundColor={titleBar.ButtonHoverBackgroundColor?.ToString() ?? "null"}.";
                }
            }
            await Task.Delay(20);
        }
        finally
        {
            if (input != null) { input.RegionsChanged -= OnRegionsChanged; }
            window.Close();
        }
        await WaitUntil(() => closed && !editor.IsLoaded, "plain window closes and unloads content");
        return (new WeakReference(window), new WeakReference(editor));
    }

    private static async Task<(WeakReference Window, WeakReference Editor)> RunIslandCycle(Window owner, int cycle,
        Action<int, IslandDisposeObservation> observeDispose)
    {
        DesktopWindowXamlSource source = new();
        TextBox editor = new() { Text = $"Plain XAML island editor {cycle}", AcceptsReturn = true };
        Grid content = new();
        content.Children.Add(editor);
        try
        {
            source.Initialize(owner.AppWindow.Id);
            source.Content = content;
            source.SiteBridge.MoveAndResize(new Windows.Graphics.RectInt32(0, 0, 500, 300));
            source.SiteBridge.Show();
            await WaitUntil(() => editor.IsLoaded, "plain XAML island content loads");
            await Task.Delay(20);
        }
        finally
        {
            observeDispose(cycle, DisposeSourceObserved(source, content));
        }
        await WaitUntil(() => !editor.IsLoaded, "plain XAML island content unloads");
        return (new WeakReference(source), new WeakReference(editor));
    }

    private static async Task<(WeakReference Window, WeakReference Editor)> RunMenuCycle(FrameworkElement owner, int cycle)
    {
        MenuFlyout menu = new();
        MenuFlyoutItem item = new() { Text = $"Plain WinUI menu {cycle}" };
        menu.Items.Add(item);
        try
        {
            menu.ShowAt(owner);
            await WaitUntil(() => menu.IsOpen, "plain menu opens");
            await Task.Delay(20);
        }
        finally
        {
            menu.Hide();
        }
        await WaitUntil(() => !menu.IsOpen, "plain menu closes");
        return (new WeakReference(menu), new WeakReference(item));
    }

    private static async Task<CompositeReferences> RunCompositeCycle(Window owner, int cycle, bool withMenu, bool withRegions,
        Action<int, IslandDisposeObservation> observeDispose)
    {
        Grid main = (Grid)owner.Content;
        TextBox editor = new() { Text = $"Composite plain WinUI editor {cycle}", AcceptsReturn = true };
        Grid floatingRoot = new();
        Window floating = new() { Title = $"Composite SDK baseline {cycle}", Content = floatingRoot };
        DesktopWindowXamlSource source = new();
        Border islandRoot = new();
        MenuFlyout? menu = null;
        InputNonClientPointerSource? input = null;
        bool closed = false;
        bool sourceDisposed = false;
        RectInt32[] drag = [new(0, 0, 500, 36)];
        RectInt32[] pass = [new(500, 0, 100, 36)];
        void OnClosed(object sender, WindowEventArgs args)
        {
            closed = true;
            floating.Closed -= OnClosed;
            if (input != null) { input.RegionsChanged -= OnRegionsChanged; }
        }
        void OnRegionsChanged(InputNonClientPointerSource sender, NonClientRegionsChangedEventArgs args)
        {
            if (closed) { return; }
            if ((sender.GetRegionRects(NonClientRegionKind.Caption)?.Length ?? 0) == 0)
            {
                sender.SetRegionRects(NonClientRegionKind.Caption, drag);
            }
            if ((sender.GetRegionRects(NonClientRegionKind.Passthrough)?.Length ?? 0) == 0)
            {
                sender.SetRegionRects(NonClientRegionKind.Passthrough, pass);
            }
        }
        static void OnTakeFocusRequested(DesktopWindowXamlSource sender, DesktopWindowXamlSourceTakeFocusRequestedEventArgs args)
        {
            XamlSourceFocusNavigationReason reason = args.Request.Reason == XamlSourceFocusNavigationReason.Last
                ? XamlSourceFocusNavigationReason.Last : XamlSourceFocusNavigationReason.First;
            sender.NavigateFocus(new XamlSourceFocusNavigationRequest(reason));
        }
        floating.Closed += OnClosed;
        try
        {
            main.Children.Add(editor);
            await WaitUntil(() => editor.IsLoaded, "composite editor loads in the main tree");
            RequireFocus(editor, "initial main tree");

            floating.Activate();
            await WaitUntil(() => floatingRoot.IsLoaded, "composite floating window loads");
            nint handle = WinRT.Interop.WindowNative.GetWindowHandle(floating);
            nint style = GetWindowLongPtrW(handle, -16);
            Marshal.SetLastPInvokeError(0);
            if (SetWindowLongPtrW(handle, -16, style & ~((nint)0x00C00000)) == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
            }
            if (!SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0237))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            if (withRegions)
            {
                input = InputNonClientPointerSource.GetForWindowId(floating.AppWindow.Id);
                input.RegionsChanged += OnRegionsChanged;
                input.SetRegionRects(NonClientRegionKind.Caption, drag);
                input.SetRegionRects(NonClientRegionKind.Passthrough, pass);
            }

            main.Children.Remove(editor);
            await WaitUntil(() => !editor.IsLoaded, "composite editor unloads before moving to the island");
            islandRoot.Child = editor;
            source.Initialize(floating.AppWindow.Id);
            source.Content = islandRoot;
            source.TakeFocusRequested += OnTakeFocusRequested;
            source.SiteBridge.MoveAndResize(new RectInt32(0, 36, 600, 360));
            source.SiteBridge.Show();
            await WaitUntil(() => editor.IsLoaded, "composite editor loads in the floating island");
            RequireFocus(editor, "floating island");
            await Task.Delay(20);

            // Match the product's source disconnect order: event removal, owned root release,
            // source content clear, source dispose. No borrowed focus controller is disposed.
            source.TakeFocusRequested -= OnTakeFocusRequested;
            observeDispose(cycle, DisposeSourceObserved(source, islandRoot, () => islandRoot.Child = null));
            sourceDisposed = true;
            await WaitUntil(() => !editor.IsLoaded, "composite editor unloads from the disposed island");
            floating.Content = null;
            floating.Close();
            await WaitUntil(() => closed, "composite floating window closes");

            main.Children.Add(editor);
            owner.Activate();
            await WaitUntil(() => editor.IsLoaded, "composite editor reloads in the main tree");
            RequireFocus(editor, "returned main tree");
            if (withMenu)
            {
                menu = new MenuFlyout();
                menu.Items.Add(new MenuFlyoutItem { Text = $"Composite SDK menu {cycle}" });
                menu.ShowAt(editor);
                await WaitUntil(() => menu.IsOpen, "composite menu opens on the returned editor");
                await Task.Delay(20);
                menu.Hide();
                await WaitUntil(() => !menu.IsOpen, "composite menu closes");
            }
            main.Children.Remove(editor);
            await WaitUntil(() => !editor.IsLoaded, "composite editor unloads after removal");
            return new CompositeReferences(new WeakReference(floating), new WeakReference(editor),
                new WeakReference(source), menu == null ? null : new WeakReference(menu));
        }
        finally
        {
            menu?.Hide();
            main.Children.Remove(editor);
            if (!sourceDisposed)
            {
                source.TakeFocusRequested -= OnTakeFocusRequested;
                observeDispose(cycle, DisposeSourceObserved(source, islandRoot, () => islandRoot.Child = null));
            }
            if (input != null) { input.RegionsChanged -= OnRegionsChanged; }
            if (!closed)
            {
                floating.Content = null;
                floating.Close();
            }
        }

        static void RequireFocus(TextBox element, string stage)
        {
            if (!element.Focus(FocusState.Programmatic))
            {
                throw new InvalidOperationException($"Composite runtime editor refused focus in {stage}.");
            }
        }
    }

    private sealed record CompositeReferences(WeakReference Window, WeakReference Editor, WeakReference Source, WeakReference? Menu);

    private static IslandDisposeObservation DisposeSourceObserved(DesktopWindowXamlSource source,
        FrameworkElement root, Action? releaseRoot = null)
    {
        // Borrow the island only during this synchronous diagnostic operation.
        // Never retain it in a tracker, callback, returned object, or across an await.
        ContentIsland? island = root.XamlRoot?.ContentIsland;
        int readErrors = 0;
        bool? beforeClosed = island == null ? null : ReadIslandFlag(island, true, ref readErrors);
        bool? beforeConnected = island == null ? null : ReadIslandFlag(island, false, ref readErrors);
        releaseRoot?.Invoke();
        try { source.Content = null; }
        finally { source.Dispose(); }
        bool? afterClosed = island == null ? null : ReadIslandFlag(island, true, ref readErrors);
        bool? afterConnected = island == null ? null : ReadIslandFlag(island, false, ref readErrors);
        return new IslandDisposeObservation(island != null, beforeClosed, beforeConnected, afterClosed,
            afterConnected, readErrors);
    }

    private static bool? ReadIslandFlag(ContentIsland island, bool closed, ref int readErrors)
    {
        try { return closed ? island.IsClosed : island.IsConnected; }
        catch (COMException) { readErrors++; return null; }
        catch (ObjectDisposedException) { readErrors++; return null; }
    }

    private static ThreadIslandCounts ReadThreadIslands()
    {
        // The SDK vector and borrowed islands remain method-local; return only counts.
        try
        {
            var islands = ContentIsland.FindAllForCurrentThread();
            int total = 0;
            int open = 0;
            int connected = 0;
            int readErrors = 0;
            foreach (ContentIsland island in islands)
            {
                total++;
                if (ReadIslandFlag(island, true, ref readErrors) == false) { open++; }
                if (ReadIslandFlag(island, false, ref readErrors) == true) { connected++; }
            }
            return new ThreadIslandCounts(total, open, connected, readErrors);
        }
        catch (COMException) { return new ThreadIslandCounts(-1, -1, -1, 1); }
        catch (ObjectDisposedException) { return new ThreadIslandCounts(-1, -1, -1, 1); }
    }

    private sealed record IslandDisposeObservation(bool Found, bool? BeforeClosed, bool? BeforeConnected,
        bool? AfterClosed, bool? AfterConnected, int ReadErrors);
    private sealed record ThreadIslandCounts(int Total, int Open, int Connected, int ReadErrors);

    private sealed class SourceDisposeTracker
    {
        internal int Disposed { get; private set; }
        internal int Closed { get; private set; }
        internal int Connected { get; private set; }
        internal int ReadErrors { get; private set; }
        internal void Add(IslandDisposeObservation observation)
        {
            Disposed++;
            if (observation.AfterClosed == true) { Closed++; }
            if (observation.AfterConnected == true) { Connected++; }
            ReadErrors += observation.ReadErrors;
        }
    }

    private static async Task Collect()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            await Task.Run(GC.WaitForPendingFinalizers);
            GC.Collect();
            await Task.Delay(120);
        }
    }

    private static ResourceSample ReadResources(string stage, int cycles, TimeSpan elapsed, ReferenceTracker windows,
        ReferenceTracker editors, ReferenceTracker sources, ReferenceTracker flyouts, SourceDisposeTracker disposal)
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        if (!GetProcessHandleCount(process.Handle, out uint handles))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        windows.PruneReleased();
        editors.PruneReleased();
        sources.PruneReleased();
        flyouts.PruneReleased();
        ThreadIslandCounts islands = ReadThreadIslands();
        return new ResourceSample(DateTime.UtcNow, elapsed.TotalSeconds, stage, cycles, handles,
            GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1), process.PrivateMemorySize64,
            GC.GetTotalMemory(false), windows.Created, windows.Live, editors.Created, editors.Live,
            windows.Released, editors.Released, sources.Created, sources.Live, sources.Released,
            flyouts.Created, flyouts.Live, flyouts.Released, windows.Live + editors.Live + sources.Live + flyouts.Live,
            islands.Total, islands.Open, islands.Connected, islands.ReadErrors, disposal.Disposed, disposal.Closed,
            disposal.Connected, disposal.ReadErrors);
    }

    private static int ReadIntegerOption(string[] arguments, string option, int fallback, int minimum, int maximum)
    {
        int index = Array.IndexOf(arguments, option);
        if (index < 0) { return fallback; }
        if (index + 1 >= arguments.Length || !int.TryParse(arguments[index + 1], NumberStyles.None,
            CultureInfo.InvariantCulture, out int value) || value < minimum || value > maximum)
        {
            throw new ArgumentException($"{option} requires an integer from {minimum} to {maximum}.");
        }
        return value;
    }

    private static string? ReadPathOption(string[] arguments, string option)
    {
        int index = Array.IndexOf(arguments, option);
        if (index < 0) { return null; }
        if (index + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[index + 1]))
        {
            throw new ArgumentException($"{option} requires an absolute path in an existing output directory.");
        }
        return Path.GetFullPath(arguments[index + 1]);
    }

    private sealed record ResourceSample(DateTime Utc, double ElapsedSeconds, string Stage, int Cycles,
        uint Handles, uint Gdi, uint User, long PrivateBytes, long ManagedBytes,
        int TrackedHosts, int AliveHosts, int TrackedContent, int AliveContent, int ReleasedHosts, int ReleasedContent,
        int TrackedSources, int AliveSources, int ReleasedSources, int TrackedMenus, int AliveMenus, int ReleasedMenus,
        int ObserverTrackedWeakReferences, int ThreadIslands, int ThreadOpenIslands, int ThreadConnectedIslands,
        int ThreadIslandReadErrors, int SourcesDisposed, int DisposedIslandsClosed, int DisposedIslandsConnected,
        int DisposedIslandReadErrors)
    {
        internal string Description => $"INFO: {Stage}: utc={Utc:O}; elapsedSeconds={ElapsedSeconds:F2}; cycles={Cycles}; handles={Handles}; GDI={Gdi}; USER={User}; privateBytes={PrivateBytes}; managedBytes={ManagedBytes}; aliveHosts={AliveHosts}/{TrackedHosts}; aliveContent={AliveContent}/{TrackedContent}; releasedHosts={ReleasedHosts}; releasedContent={ReleasedContent}; aliveSources={AliveSources}/{TrackedSources}; releasedSources={ReleasedSources}; aliveMenus={AliveMenus}/{TrackedMenus}; releasedMenus={ReleasedMenus}; observerTrackedWeakReferences={ObserverTrackedWeakReferences}; threadIslands={ThreadIslands}; threadOpenIslands={ThreadOpenIslands}; threadConnectedIslands={ThreadConnectedIslands}; threadIslandReadErrors={ThreadIslandReadErrors}; sourcesDisposed={SourcesDisposed}; disposedIslandsClosed={DisposedIslandsClosed}; disposedIslandsConnected={DisposedIslandsConnected}; disposedIslandReadErrors={DisposedIslandReadErrors}";

        internal string ToCsv() => FormattableString.Invariant(
            $"{Environment.ProcessId},{Utc:O},{ElapsedSeconds:F3},\"{Stage.Replace("\"", "\"\"", StringComparison.Ordinal)}\",{Cycles},{Handles},{Gdi},{User},{PrivateBytes},{ManagedBytes},{TrackedHosts},{AliveHosts},{TrackedContent},{AliveContent},{ReleasedHosts},{ReleasedContent},{TrackedSources},{AliveSources},{ReleasedSources},{TrackedMenus},{AliveMenus},{ReleasedMenus},{ObserverTrackedWeakReferences},{ThreadIslands},{ThreadOpenIslands},{ThreadConnectedIslands},{ThreadIslandReadErrors},{SourcesDisposed},{DisposedIslandsClosed},{DisposedIslandsConnected},{DisposedIslandReadErrors}");
    }

    private sealed class ReferenceTracker
    {
        private readonly List<WeakReference> live = [];
        internal int Created { get; private set; }
        internal int Released { get; private set; }
        internal int Live => live.Count;
        internal void Add(WeakReference reference)
        {
            live.Add(reference);
            Created++;
        }
        internal void PruneReleased() => Released += live.RemoveAll(reference => !reference.IsAlive);
    }

    private static async Task WaitUntil(Func<bool> condition, string description)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        do
        {
            await Task.Delay(20);
            if (condition()) { return; }
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(3));
        throw new TimeoutException(description);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessHandleCount(nint process, out uint handleCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetGuiResources(nint process, uint flags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint window, int index);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint window, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
