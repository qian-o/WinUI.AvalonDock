using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace WinUI.AvalonDock.Experiments.Shared;

/// <summary>Opt-in same-dispatcher workspace and native-window lifetime checks.</summary>
internal static class WorkspaceLifecycleSmokeTests
{
    private static readonly Type FocusType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.FocusElementManager", true)!;
    private static readonly FieldInfo ManagersField = FocusType.GetField("Managers", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo HandlerField = FocusType.GetField("windowHandler", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo LastFocusMethod = FocusType.GetMethod("GetLastFocusedElement", BindingFlags.Static | BindingFlags.NonPublic)!;

    // Keep the caller's native window alive, but dispose and detach its manager before calling.
    // This tests the upstream single-UI-thread focus subscription; it does not assert multi-thread support.
    internal static async Task<IReadOnlyList<string>> RunAsync(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        LifetimeOptions lifetimeOptions = LifetimeOptions.Read(Environment.GetCommandLineArgs());
        if (!mainWindow.DispatcherQueue.HasThreadAccess || RegisteredManagers.Count != 0)
        {
            throw new ArgumentException("Run on the UI dispatcher after disposing all earlier managers.", nameof(mainWindow));
        }

        List<string> checks = [];
        Check(HandlerField.GetValue(null) == null, "no focus subscription remains before workspace checks");
        Workspace first = CreateWorkspace("First workspace");
        Workspace second = CreateWorkspace("Second workspace");
        bool firstClosed = false;
        bool secondClosed = false;
        try
        {
            first.Window.Activate();
            second.Window.Activate();
            await WaitUntil(() => first.Manager.IsLoaded && second.Manager.IsLoaded && RegisteredManagers.Count == 2,
                "two workspaces load and register focus observation");
            Check(first.Manager.IsLoaded && second.Manager.IsLoaded, "two managers load in independent native window roots");
            Check(!ReferenceEquals(first.Manager.XamlRoot, second.Manager.XamlRoot), "workspaces have independent XamlRoots");
            Check(RegisteredManagers.Count == 2 && RegisteredManagers.Contains(first.Manager) && RegisteredManagers.Contains(second.Manager),
                "same-thread managers share exactly one focus subscription");
            object? sharedHandler = HandlerField.GetValue(null);
            Check(sharedHandler != null, "native focus observation is installed while managers are loaded");

            await ActivateDocument(first, 0);
            await ActivateDocument(second, 0);
            await ActivateDocument(first, 1);
            await ActivateDocument(second, 1);
            Check(ReferenceEquals(first.Manager.Layout.ActiveContent, first.Documents[1])
                && ReferenceEquals(second.Manager.Layout.ActiveContent, second.Documents[1]),
                "alternating model activation preserves each workspace's active document");
            Check(HasRememberedFocus(first, 0) && HasRememberedFocus(first, 1)
                && HasRememberedFocus(second, 0) && HasRememberedFocus(second, 1),
                "focus records remain attached to their own document and user TextBox");

            first.Window.Activate();
            bool loadedBeforeDirectActivation = first.Editors[0].IsLoaded;
            first.Documents[0].IsActive = true;
            await WaitUntil(() => first.Editors[0].IsLoaded, "directly activated document content loads");
            await Settle();
            string directActivationObservation = $"INFO: direct IsActive activation with editorLoadedBeforeActivation={loadedBeforeDirectActivation}: {DescribeFocus(first, 0)}. The fixed upstream restoration path runs synchronously; selecting an unloaded virtualized document does not promise deferred editor focus.";
            checks.Add(directActivationObservation);

            // Upstream restores the remembered element synchronously. Verify that contract
            // while the selected editor is already loaded, without manually focusing it.
            first.Documents[0].IsSelected = true;
            await WaitUntil(() => first.Editors[0].IsLoaded, "selected editor is loaded before focus restoration");
            LayoutDocumentTabItem header = first.Documents[0].TabItem
                ?? throw new InvalidOperationException("The selected document has no loaded tab header.");
            object originalTabStop = header.ReadLocalValue(Control.IsTabStopProperty);
            try
            {
                // Temporarily make this test-owned header focusable. Upstream excludes
                // LayoutDocumentTabItem from content-focus recording, so the editor record survives.
                header.IsTabStop = true;
                Check(header.Focus(FocusState.Programmatic), "document header accepts focus for the restoration precondition");
                await WaitUntil(() => ReferenceEquals(FocusManager.GetFocusedElement(header.XamlRoot), header),
                    "document header receives focus before restoring editor focus");
                Check(!ReferenceEquals(FocusManager.GetFocusedElement(first.Editors[0].XamlRoot), first.Editors[0])
                    && HasRememberedFocus(first, 0), "loaded editor starts unfocused with its remembered focus record intact");
                first.Manager.Layout.ActiveContent = null;
                Check(first.Documents[0].IsSelected && !first.Documents[0].IsActive,
                    "loaded selected document can be inactive before restoring its recorded focus");
                first.Documents[0].IsActive = true;
                try
                {
                    await WaitUntil(() => first.Editors[0].IsLoaded
                        && ReferenceEquals(FocusManager.GetFocusedElement(first.Editors[0].XamlRoot), first.Editors[0]),
                        "reactivating a loaded selected document restores its recorded editor focus");
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException($"{exception.Message}; {DescribeFocus(first, 0)}; earlier observation: {directActivationObservation}", exception);
                }
                Check(ReferenceEquals(FocusManager.GetFocusedElement(first.Editors[0].XamlRoot), first.Editors[0]),
                    "reactivating a loaded selected document restores its recorded editor focus");
            }
            finally
            {
                if (originalTabStop == DependencyProperty.UnsetValue)
                {
                    header.ClearValue(Control.IsTabStopProperty);
                }
                else
                {
                    header.SetValue(Control.IsTabStopProperty, originalTabStop);
                }
            }
            Check(ReferenceEquals(second.Manager.Layout.ActiveContent, second.Documents[1]) && HasRememberedFocus(second, 1),
                "restoring first workspace focus does not change second workspace model or focus record");

            second.Window.Content = new Grid();
            await WaitUntil(() => !second.Manager.IsLoaded && RegisteredManagers.Count == 1,
                "temporary unload completes");
            Check(!second.Manager.IsLoaded && RegisteredManagers.Count == 1 && RegisteredManagers.Contains(first.Manager),
                "temporary unload removes only the unloaded manager from focus observation");
            Check(ReferenceEquals(HandlerField.GetValue(null), sharedHandler), "remaining workspace keeps the existing native focus subscription");
            second.Window.Content = second.Manager;
            await WaitUntil(() => second.Manager.IsLoaded && RegisteredManagers.Count == 2,
                "temporary reload completes");
            Check(second.Manager.IsLoaded && RegisteredManagers.Count == 2, "reloading a workspace restores exactly one manager registration");
            await ActivateDocument(second, 0);
            Check(HasRememberedFocus(second, 0), "reloaded workspace records its own editor focus");

            first.Manager.Dispose();
            first.Window.Content = new Grid();
            first.Window.Close();
            firstClosed = true;
            await WaitUntil(() => RegisteredManagers.Count == 1, "first workspace focus registration is removed");
            Check(RegisteredManagers.Count == 1 && RegisteredManagers.Contains(second.Manager),
                "disposing and closing one workspace retains only the surviving manager");
            Check(ReferenceEquals(HandlerField.GetValue(null), sharedHandler), "closing one workspace preserves native focus observation for the survivor");
            await ActivateDocument(second, 1);
            second.Documents[1].Float();
            await WaitUntil(() => second.Documents[1].IsFloating && second.Manager.FloatingWindows.Count() == 1
                && second.Editors[1].IsLoaded, "surviving document floats and loads");
            Check(second.Documents[1].IsFloating && second.Manager.FloatingWindows.Count() == 1,
                "surviving workspace still creates a document floating window");
            Check(second.Editors[1].Focus(FocusState.Programmatic), "floating document accepts focus through the user control API");
            await Settle();
            Check(HasRememberedFocus(second, 1), "floating editor focus is recorded for the surviving document");
            second.Documents[1].Dock();
            await WaitUntil(() => !second.Documents[1].IsFloating && !second.Manager.FloatingWindows.Any()
                && !second.Manager.Layout.FloatingWindows.Any() && second.Editors[1].IsLoaded,
                "surviving document redocks and floating hosts close");
            Check(!second.Documents[1].IsFloating && !second.Manager.FloatingWindows.Any() && !second.Manager.Layout.FloatingWindows.Any(),
                "redocking closes and removes the surviving workspace's floating window");
            await ActivateDocument(second, 1);
            Check(HasRememberedFocus(second, 1), "surviving editor still receives focus after redocking");

            await RunCycles(second, checks, lifetimeOptions);

            second.Manager.Dispose();
            second.Window.Content = new Grid();
            second.Window.Close();
            secondClosed = true;
            await WaitUntil(() => RegisteredManagers.Count == 0 && HandlerField.GetValue(null) == null,
                "last workspace releases focus observation");
            Check(RegisteredManagers.Count == 0 && HandlerField.GetValue(null) == null,
                "disposing the last workspace releases the static manager registry and native focus subscription");
            return checks;
        }
        finally
        {
            first.Manager.Dispose();
            second.Manager.Dispose();
            if (!firstClosed)
            {
                first.Window.Content = new Grid();
                first.Window.Close();
            }
            if (!secondClosed)
            {
                second.Window.Content = new Grid();
                second.Window.Close();
            }
            mainWindow.Activate();
        }

        void Check(bool condition, string description) => Assert(condition, description, checks);
    }

    private static async Task RunCycles(Workspace workspace, List<string> checks, LifetimeOptions options)
    {
        // These are controlled same-environment regression limits, not public API or
        // machine-independent performance guarantees. The first full batch also warms caches.
        ReferenceTracker references = new();
        List<ResourceSample> samples = [];
        Stopwatch elapsed = Stopwatch.StartNew();
        using StreamWriter checkpoint = new(options.CheckpointPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        checkpoint.WriteLine(ResourceSample.CsvHeader);
        checks.Add(FormattableString.Invariant($"INFO: workspace lifetime processId={Environment.ProcessId}, startedUtc={DateTime.UtcNow:O}, cycles={options.TotalCycles}, warmup={LifetimeOptions.WarmupCycles}, batch={options.BatchCycles}, idleSeconds={options.IdleSeconds}, checkpoint={options.CheckpointPath}."));
        int completedCycles = 0;
        // Write the PID and starting counters before the first cycle; subsequent rows survive a failed run too.
        checkpoint.WriteLine(ReadResources("initial", completedCycles, elapsed.Elapsed, references).ToCsv());
        for (; completedCycles < LifetimeOptions.WarmupCycles; completedCycles++)
        {
            references.AddRange(await RunCycle(workspace, completedCycles));
        }
        await ActivateDocument(workspace, 0);
        await CollectReleasedObjects();
        Sample($"after {LifetimeOptions.WarmupCycles} warm-up cycles");
        while (completedCycles < options.TotalCycles)
        {
            int batchEnd = completedCycles + Math.Min(options.BatchCycles, options.TotalCycles - completedCycles);
            for (; completedCycles < batchEnd; completedCycles++)
            {
                references.AddRange(await RunCycle(workspace, completedCycles));
            }
            await ActivateDocument(workspace, 0);
            await CollectReleasedObjects();
            Sample($"after {completedCycles} cycles");
        }
        int pressureSampleCount = samples.Count;
        for (int idleElapsed = 0; idleElapsed < options.IdleSeconds;)
        {
            int interval = Math.Min(5, options.IdleSeconds - idleElapsed);
            // No forced collection here: observe delayed native/XAML teardown while the dispatcher remains free.
            await Task.Delay(TimeSpan.FromSeconds(interval));
            idleElapsed += interval;
            Sample($"after {idleElapsed} idle seconds without forced GC");
        }
        if (options.IdleSeconds > 0)
        {
            await CollectReleasedObjects();
            Sample($"after {options.IdleSeconds} idle seconds and final GC");
        }
        elapsed.Stop();
        Assert(workspace.Pane.Children.Count == 2 && workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2,
            $"{completedCycles} create, float, dock, menu and close cycles leave only the original documents", checks);
        Assert(!workspace.Manager.FloatingWindows.Any() && !workspace.Manager.Layout.FloatingWindows.Any(),
            $"{completedCycles} lifetime cycles leave no floating controls or floating layout models", checks);
        Assert(RegisteredManagers.Count == 1 && RegisteredManagers.Contains(workspace.Manager),
            $"{completedCycles} lifetime cycles do not accumulate focus registrations", checks);
        if (pressureSampleCount < 3)
        {
            checks.Add("INFO: fewer than two post-warm-up pressure batches were requested; no later pressure-batch resource trend can be checked.");
        }
        for (int index = 2; index < samples.Count; index++)
        {
            ResourceSample previous = samples[index - 1];
            ResourceSample current = samples[index];
            long gdiGrowth = (long)current.Gdi - previous.Gdi;
            long userGrowth = (long)current.User - previous.User;
            // Preserve the default 30-cycle limits; longer opt-in batches scale by their measured cycle count.
            // Short batches and idle samples retain the same tolerance for platform counter fluctuations.
            int measuredCycles = current.Cycles - previous.Cycles;
            int gdiLimit = (int)Math.Ceiling(32d * Math.Max(30, measuredCycles) / 30);
            int userLimit = (int)Math.Ceiling(16d * Math.Max(30, measuredCycles) / 30);
            Require(gdiGrowth <= gdiLimit, $"GDI growth is bounded after warm-up ({previous.Stage} to {current.Stage}: {gdiGrowth}, limit {gdiLimit})");
            Require(userGrowth <= userLimit, $"USER growth is bounded after warm-up ({previous.Stage} to {current.Stage}: {userGrowth}, limit {userLimit})");
        }
        foreach (ResourceSample sample in samples)
        {
            Require(sample.References.All(reference => reference.Alive <= 1),
                $"released object references do not accumulate ({sample.Stage}, at most one live object per kind)");
            FloatingHostSample host = sample.FloatingHosts;
            Require(host.NativeWindows == 0 && host.OwnerHooks == 0 && host.Subclasses == 0
                && host.SelfHandles == 0 && host.OwnerHandles == 0 && host.Closed == host.Windows,
                $"retired floating windows retain no live native HWND, callback hook or GCHandle ({sample.Stage})");
        }
        checks.Add(FormattableString.Invariant($"INFO: {completedCycles} same-thread lifecycle cycles and {options.IdleSeconds} tail idle seconds completed in {elapsed.Elapsed.TotalSeconds:F2} seconds. GDI/USER and released-object limits detect regressions in this controlled environment; process handles are observational because the plain WinUI baselines also grow modestly. This is not a long-duration stability guarantee."));

        void Sample(string stage)
        {
            references.PruneReleased();
            ResourceSample sample = ReadResources(stage, completedCycles, elapsed.Elapsed, references);
            samples.Add(sample);
            checks.Add(sample.Description);
            checkpoint.WriteLine(sample.ToCsv());
        }
        void Require(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Workspace lifecycle resource regression: {description}{Environment.NewLine}"
                    + string.Join(Environment.NewLine, samples.Select(sample => sample.Description)));
            }
            checks.Add($"PASS: controlled environment {description}");
        }
    }

    private static async Task<IReadOnlyList<ObservedReference>> RunCycle(Workspace workspace, int cycle)
    {
        TextBox editor = new() { Text = $"Consumer editor {cycle}", AcceptsReturn = true };
        LayoutDocument document = new() { Title = $"Lifetime {cycle}", ContentId = $"lifetime-{cycle}", Content = editor };
        workspace.Pane.Children.Add(document);
        document.IsSelected = true;
        await WaitUntil(() => editor.IsLoaded, $"cycle {cycle}: new document content loads");
        document.IsActive = true;
        if (!editor.IsLoaded || !editor.Focus(FocusState.Programmatic))
        {
            throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: new editor failed to load or focus.");
        }
        LayoutItem item = workspace.Manager.GetLayoutItemFromModel(document)
            ?? throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: document has no layout item.");
        List<ObservedReference> references =
        [
            new("document", new WeakReference(document)),
            new("content", new WeakReference(editor)),
            new("item", new WeakReference(item)),
            new("presenter", new WeakReference(item.View))
        ];
        document.Float();
        await WaitUntil(() => document.IsFloating && workspace.Manager.FloatingWindows.Count() == 1
            && workspace.Manager.FloatingWindows.Single().Content is FrameworkElement { IsLoaded: true }
            && editor.IsLoaded, $"cycle {cycle}: native floating content loads");
        LayoutFloatingWindowControl? floating = workspace.Manager.FloatingWindows.SingleOrDefault();
        if (!document.IsFloating || floating == null)
        {
            throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: document did not create one native floating window.");
        }
        references.Add(new("floating-control", new WeakReference(floating)));
        document.Dock();
        await WaitUntil(() => !document.IsFloating && !workspace.Manager.FloatingWindows.Any()
            && editor.IsLoaded, $"cycle {cycle}: content redocks and floating host closes");
        if (document.IsFloating || workspace.Manager.FloatingWindows.Any())
        {
            throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: floating window survived redocking.");
        }

        MenuFlyout? originalMenu = workspace.Manager.DocumentContextMenu;
        MenuFlyout menu = new();
        menu.Items.Add(new MenuFlyoutItem { Text = $"Consumer menu {cycle}" });
        references.Add(new("menu", new WeakReference(menu)));
        try
        {
            workspace.Manager.DocumentContextMenu = menu;
            menu.ShowAt(editor);
            await WaitUntil(() => menu.IsOpen, $"cycle {cycle}: menu opens");
            if (!menu.IsOpen)
            {
                throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: document menu did not open.");
            }
            menu.Hide();
            await WaitUntil(() => !menu.IsOpen, $"cycle {cycle}: menu closes");
        }
        finally
        {
            menu.Hide();
            workspace.Manager.DocumentContextMenu = originalMenu;
        }
        document.Close();
        await WaitUntil(() => document.Root == null && workspace.Manager.GetLayoutItemFromModel(document) == null,
            $"cycle {cycle}: document closes and item registration is removed");
        if (document.Root != null || workspace.Manager.GetLayoutItemFromModel(document) != null)
        {
            throw new InvalidOperationException($"Workspace lifetime cycle {cycle}: closed document retained a layout parent or item registration.");
        }
        return references;
    }

    private static Workspace CreateWorkspace(string title)
    {
        TextBox[] editors = [new() { Text = title + " editor one" }, new() { Text = title + " editor two" }];
        LayoutDocument[] documents =
        [
            new() { Title = title + " one", ContentId = title + "-one", Content = editors[0] },
            new() { Title = title + " two", ContentId = title + "-two", Content = editors[1] }
        ];
        LayoutDocumentPane pane = new();
        pane.Children.Add(documents[0]);
        pane.Children.Add(documents[1]);
        DockingManager manager = new()
        {
            Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(pane)) }
        };
        Window window = new() { Title = "AvalonDock verification: " + title, Content = manager };
        return new Workspace(window, manager, pane, documents, editors);
    }

    private static async Task ActivateDocument(Workspace workspace, int index)
    {
        workspace.Window.Activate();
        workspace.Documents[index].IsSelected = true;
        await WaitUntil(() => workspace.Editors[index].IsLoaded, "selected document content loads before activation");
        workspace.Documents[index].IsActive = true;
        await Settle();
        TextBox editor = workspace.Editors[index];
        if (!editor.Focus(FocusState.Programmatic))
        {
            throw new InvalidOperationException($"Workspace focus: {workspace.Documents[index].Title} rejected programmatic focus.");
        }
        await Settle();
        if (!ReferenceEquals(FocusManager.GetFocusedElement(editor.XamlRoot), editor))
        {
            throw new InvalidOperationException($"Workspace focus: {workspace.Documents[index].Title} did not retain its editor focus.");
        }
    }

    private static bool HasRememberedFocus(Workspace workspace, int index)
        => ReferenceEquals(LastFocusMethod.Invoke(null, [workspace.Documents[index]]), workspace.Editors[index]);

    private static string DescribeFocus(Workspace workspace, int index)
    {
        TextBox editor = workspace.Editors[index];
        object? focused = editor.XamlRoot == null ? null : FocusManager.GetFocusedElement(editor.XamlRoot);
        return $"documentActive={workspace.Documents[index].IsActive}, documentSelected={workspace.Documents[index].IsSelected}, "
            + $"editorLoaded={editor.IsLoaded}, editorRootPresent={editor.XamlRoot != null}, editorFocusState={editor.FocusState}, "
            + $"editorSize={editor.ActualWidth:F1}x{editor.ActualHeight:F1}, focusedType={focused?.GetType().FullName ?? "null"}, "
            + $"editorHasFocus={ReferenceEquals(focused, editor)}, recordedEditorMatches={HasRememberedFocus(workspace, index)}";
    }

    private static IList RegisteredManagers => (IList)ManagersField.GetValue(null)!;

    private static async Task CollectReleasedObjects()
    {
        // Let queued native/XAML teardown run between collections; never block the UI dispatcher on a sleep.
        for (int pass = 0; pass < 3; pass++)
        {
            await Task.Delay(80);
            GC.Collect();
            await Task.Run(GC.WaitForPendingFinalizers);
            GC.Collect();
        }
    }

    private static ResourceSample ReadResources(string stage, int cycles, TimeSpan elapsed, ReferenceTracker references)
    {
        using Process process = Process.GetCurrentProcess();
        if (!GetProcessHandleCount(process.Handle, out uint handles))
        {
            throw new InvalidOperationException("GetProcessHandleCount failed while reading the verification process.");
        }
        uint gdi = GetGuiResources(process.Handle, 0);
        uint user = GetGuiResources(process.Handle, 1);
        process.Refresh();
        return new ResourceSample(Environment.ProcessId, DateTime.UtcNow, elapsed.TotalSeconds, stage, cycles,
            handles, gdi, user, process.PrivateMemorySize64, GC.GetTotalMemory(false), references.LiveReferences.Count,
            references.ReadCounts(), ReadFloatingHosts(references.LiveReferences));
    }

    private static FloatingHostSample ReadFloatingHosts(IReadOnlyList<ObservedReference> references)
    {
        int windows = 0;
        int closed = 0;
        int nativeWindows = 0;
        int hosts = 0;
        int ownerHooks = 0;
        int subclasses = 0;
        int selfHandles = 0;
        int ownerHandles = 0;
        foreach (ObservedReference reference in references.Where(reference => reference.Kind == "floating-control"))
        {
            if (reference.Reference.Target is not LayoutFloatingWindowControl window)
            {
                continue;
            }
            windows++;
            if ((bool)typeof(LayoutFloatingWindowControl).GetProperty("IsClosed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!)
            {
                closed++;
            }
            object? host = typeof(LayoutFloatingWindowControl).GetField("host", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
            if (host == null)
            {
                continue;
            }
            hosts++;
            Type type = host.GetType();
            if (Read<bool>("ownerHookInstalled"))
            {
                ownerHooks++;
            }
            if (Read<bool>("subclassInstalled"))
            {
                subclasses++;
            }
            if (Read<GCHandle>("selfHandle").IsAllocated)
            {
                selfHandles++;
            }
            if (Read<GCHandle>("ownerReference").IsAllocated)
            {
                ownerHandles++;
            }
            if (IsWindow(Read<nint>("handle")))
            {
                nativeWindows++;
            }
            T Read<T>(string field) => (T)type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        }
        return new FloatingHostSample(windows, closed, hosts, nativeWindows, ownerHooks, subclasses, selfHandles, ownerHandles);
    }

    private static void Assert(bool condition, string description, List<string> checks)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Workspace lifecycle: {description}");
        }
        checks.Add($"PASS: {description}");
    }

    private static async Task Settle() => await Task.Delay(120);

    private static async Task WaitUntil(Func<bool> condition, string description)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        // A pending XAML/native event is not a synchronous public API guarantee.
        // Bound the wait, while leaving the dispatcher free to finish queued load and close work.
        do
        {
            await Task.Delay(20);
            if (condition())
            {
                return;
            }
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(3));
        throw new InvalidOperationException($"Workspace lifecycle timed out waiting for: {description}");
    }

    private sealed record Workspace(Window Window, DockingManager Manager, LayoutDocumentPane Pane, LayoutDocument[] Documents, TextBox[] Editors);
    private sealed record ObservedReference(string Kind, WeakReference Reference);
    private sealed record ReferenceSample(string Kind, int Alive, int Released, int Total);
    private sealed class ReferenceTracker
    {
        private static readonly string[] Kinds = ["document", "content", "item", "presenter", "floating-control", "menu"];
        private readonly List<ObservedReference> liveReferences = [];
        private readonly Dictionary<string, int> created = Kinds.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
        private readonly Dictionary<string, int> released = Kinds.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);

        internal IReadOnlyList<ObservedReference> LiveReferences => liveReferences;

        internal void AddRange(IReadOnlyList<ObservedReference> references)
        {
            foreach (ObservedReference reference in references)
            {
                created[reference.Kind]++;
                liveReferences.Add(reference);
            }
        }

        internal void PruneReleased()
        {
            // Keep every live target visible to subsequent samples and native-host inspection.
            // Retain only counters for dead targets so the observer itself does not grow once per cycle.
            for (int index = liveReferences.Count - 1; index >= 0; index--)
            {
                ObservedReference reference = liveReferences[index];
                if (!reference.Reference.IsAlive)
                {
                    released[reference.Kind]++;
                    liveReferences.RemoveAt(index);
                }
            }
            liveReferences.TrimExcess();
        }

        internal ReferenceSample[] ReadCounts() => Kinds.Select(kind =>
            // Alive is the last liveness observation at this checkpoint; these counts always sum to Total.
            new ReferenceSample(kind, created[kind] - released[kind], released[kind], created[kind])).ToArray();
    }

    private sealed record LifetimeOptions(int TotalCycles, int BatchCycles, int IdleSeconds, string CheckpointPath)
    {
        internal const int WarmupCycles = 12;

        internal static LifetimeOptions Read(string[] arguments)
        {
            int cycles = ReadIntegerOption("--lifetime-cycles", 102, WarmupCycles, 100000);
            int batch = ReadIntegerOption("--lifetime-batch", 30, 1, 100000);
            int idle = ReadIntegerOption("--lifetime-idle-seconds", 0, 0, 3600);
            int checkpointOption = Array.IndexOf(arguments, "--lifetime-checkpoint");
            string checkpoint;
            if (checkpointOption >= 0)
            {
                if (checkpointOption + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[checkpointOption + 1]))
                {
                    throw new ArgumentException("--lifetime-checkpoint requires an absolute CSV path in an existing output directory.");
                }
                checkpoint = Path.GetFullPath(arguments[checkpointOption + 1]);
            }
            else
            {
                int boundaryOption = Array.IndexOf(arguments, "--boundary-smoke");
                string output = boundaryOption >= 0 && boundaryOption + 1 < arguments.Length
                    && !arguments[boundaryOption + 1].StartsWith("--", StringComparison.Ordinal)
                    ? Path.GetFullPath(arguments[boundaryOption + 1])
                    : Path.Combine(AppContext.BaseDirectory, "workspace-lifetime.log");
                checkpoint = output + ".lifetime.csv";
            }
            if (!Directory.Exists(Path.GetDirectoryName(checkpoint)))
            {
                throw new ArgumentException($"The lifetime checkpoint output directory does not exist: {Path.GetDirectoryName(checkpoint)}");
            }
            return new LifetimeOptions(cycles, batch, idle, checkpoint);

            int ReadIntegerOption(string option, int fallback, int minimum, int maximum)
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
        }
    }
    private sealed record FloatingHostSample(int Windows, int Closed, int Hosts, int NativeWindows,
        int OwnerHooks, int Subclasses, int SelfHandles, int OwnerHandles)
    {
        public override string ToString() => $"windows={Windows}, closed={Closed}, hosts={Hosts}, nativeIsWindow={NativeWindows}, "
            + $"ownerHookInstalled={OwnerHooks}, subclassInstalled={Subclasses}, selfGCHandleAllocated={SelfHandles}, ownerGCHandleAllocated={OwnerHandles}";
    }
    private sealed record ResourceSample(int ProcessId, DateTime Utc, double ElapsedSeconds, string Stage, int Cycles,
        uint Handles, uint Gdi, uint User, long PrivateBytes, long ManagedBytes, int TrackedWeakReferences,
        IReadOnlyList<ReferenceSample> References, FloatingHostSample FloatingHosts)
    {
        // The first fourteen columns match the plain-runtime baseline's checkpoint schema.
        internal const string CsvHeader = "pid,utc,elapsedSeconds,stage,cycles,handles,gdi,user,privateBytes,managedBytes,trackedHosts,aliveHosts,trackedContent,aliveContent,"
            + "observerTrackedWeakReferences,documentTotal,documentAlive,documentReleased,contentReleased,itemTotal,itemAlive,itemReleased,presenterTotal,presenterAlive,presenterReleased,"
            + "floatingControlsReleased,menuTotal,menuAlive,menuReleased,retiredWindows,retiredClosed,retiredHosts,nativeWindows,ownerHooks,subclasses,selfHandles,ownerHandles";

        internal string Description => FormattableString.Invariant($"INFO: {Stage}: pid={ProcessId}, utc={Utc:O}, elapsedSeconds={ElapsedSeconds:F2}, cycles={Cycles}, handles={Handles}, GDI={Gdi}, USER={User}, privateMiB={PrivateBytes / 1048576d:F2}, managedMiB={ManagedBytes / 1048576d:F2}, observerTrackedWeakReferences={TrackedWeakReferences}, alive weak references [{string.Join(", ", References.Select(reference => $"{reference.Kind}={reference.Alive}/{reference.Total}, released={reference.Released}"))}], surviving floating host state [{FloatingHosts}]");

        internal string ToCsv()
        {
            ReferenceSample document = References.Single(reference => reference.Kind == "document");
            ReferenceSample content = References.Single(reference => reference.Kind == "content");
            ReferenceSample item = References.Single(reference => reference.Kind == "item");
            ReferenceSample presenter = References.Single(reference => reference.Kind == "presenter");
            ReferenceSample floating = References.Single(reference => reference.Kind == "floating-control");
            ReferenceSample menu = References.Single(reference => reference.Kind == "menu");
            FloatingHostSample host = FloatingHosts;
            return FormattableString.Invariant($"{ProcessId},{Utc:O},{ElapsedSeconds:F3},\"{Stage.Replace("\"", "\"\"", StringComparison.Ordinal)}\",{Cycles},{Handles},{Gdi},{User},{PrivateBytes},{ManagedBytes},{floating.Total},{floating.Alive},{content.Total},{content.Alive},{TrackedWeakReferences},{document.Total},{document.Alive},{document.Released},{content.Released},{item.Total},{item.Alive},{item.Released},{presenter.Total},{presenter.Alive},{presenter.Released},{floating.Released},{menu.Total},{menu.Alive},{menu.Released},{host.Windows},{host.Closed},{host.Hosts},{host.NativeWindows},{host.OwnerHooks},{host.Subclasses},{host.SelfHandles},{host.OwnerHandles}");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessHandleCount(nint process, out uint handleCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetGuiResources(nint process, uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
}
