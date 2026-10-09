using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

/// <summary>Tests a real package consumer through public product APIs.</summary>
internal static class PackageSmokeTests
{
    internal static void StartIfRequested(MainWindow window)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int option = Array.IndexOf(arguments, "--package-smoke");
        if (option < 0)
        {
            return;
        }
        string output = option + 1 < arguments.Length ? arguments[option + 1]
            : Path.Combine(AppContext.BaseDirectory, "package-smoke.log");
        window.Manager.Loaded += Run;

        async void Run(object sender, RoutedEventArgs args)
        {
            window.Manager.Loaded -= Run;
            List<string> checks = [];
            DockingManager? mvvmManager = null;
            try
            {
                await Settle();
                DockingManager manager = window.Manager;
                Assembly assembly = typeof(DockingManager).Assembly;
                checks.Add($"INFO: product assembly {assembly.Location}");
                checks.Add($"INFO: product informational version {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
                Check(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.StartsWith("0.1.0-alpha", StringComparison.Ordinal) == true,
                    "loaded product assembly has candidate package version");
                Check(manager.IsLoaded && manager.GetLayoutItemFromModel(window.FirstDocument()!)?.View.IsLoaded == true,
                    "package provides working default theme, templates and content presenters");
                Check(manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2,
                    "consumer document source imports both models");
                LayoutDocument document = FindDocument(window.FirstModel);
                Check(document.ContentId == window.FirstModel.Id && document.Title == window.FirstModel.Title,
                    "consumer style binds model title and stable identifier");
                window.FirstModel.Title = "Updated consumer title";
                await Settle();
                Check(document.Title == window.FirstModel.Title, "consumer title binding remains live");

                GenericTheme theme = new();
                manager.Theme = theme;
                await Settle();
                Check(manager.Resources.MergedDictionaries.Any(value => value.Source == theme.GetResourceUri()),
                    "package theme URI resolves packaged resource dictionary");
                manager.Theme = null;
                await Settle();
                Check(!manager.Resources.MergedDictionaries.Any(value => value.Source == theme.GetResourceUri()),
                    "package theme can be removed without a missing resource error");

                WorkspaceDocument secondModel = window.SecondModel;
                FindDocument(secondModel).Float();
                await Settle();
                Check(FindDocument(secondModel).IsFloating, "package consumer document creates floating window");
                byte[] saved = window.SaveLayout();
                Check(Encoding.UTF8.GetString(saved).Contains(secondModel.Id, StringComparison.Ordinal),
                    "upstream XML serializer writes stable consumer ContentId");
                FindDocument(secondModel).Dock();
                await Settle();
                window.RestoreLayout(saved);
                await Settle();
                Check(ReferenceEquals(FindDocument(secondModel).Content, secondModel) && FindDocument(secondModel).IsFloating,
                    "XML restore reconnects original consumer model and floating state");
                Check(manager.Layout.Descendents().OfType<LayoutAnchorable>().Any(value => ReferenceEquals(value.Content, window.ToolModel)),
                    "XML restore retains consumer tool model");
                FindDocument(secondModel).Dock();
                await Settle();

                WorkspaceDocument retired = window.FirstModel;
                WorkspaceDocument replacement = new()
                {
                    Id = "consumer-replacement", Title = "Replacement", Text = "New consumer model", IsModified = false
                };
                window.ReplaceFirst(replacement);
                await Settle();
                Check(!manager.Layout.Descendents().OfType<LayoutDocument>().Any(value => ReferenceEquals(value.Content, retired)),
                    "consumer collection replacement removes retired document");
                Check(FindDocument(replacement).ContentId == replacement.Id,
                    "consumer collection replacement imports replacement with stable identifier");

                manager.Dispose();
                mvvmManager = new DockingManager
                {
                    LayoutItemTemplate = manager.LayoutItemTemplate,
                    LayoutItemContainerStyleSelector = MainWindow.CreateItemStyleSelector()
                };
                window.Content = mvvmManager;
                WorkspaceDocument mvvmDocument = new()
                {
                    Id = "consumer-mvvm-document", Title = "MVVM document", Text = "MVVM package model", IsModified = false
                };
                DocumentDock documentDock = new() { VisibleDockables = new ObservableCollection<IDockable> { mvvmDocument } };
                RootDock root = new()
                {
                    VisibleDockables = new ObservableCollection<IDockable> { documentDock },
                    ActiveDockable = mvvmDocument
                };
                mvvmManager.DockLayout = root;
                await Settle();
                LayoutDocument nativeDocument = mvvmManager.Layout.Descendents().OfType<LayoutDocument>()
                    .Single(value => ReferenceEquals(value.Content, mvvmDocument));
                Check(nativeDocument.ContentId == mvvmDocument.Id && nativeDocument.Title == mvvmDocument.Title,
                    "upstream MVVM model maps to real package layout");
                mvvmManager.DockLayout = null;
                await Settle();
                checks.AddRange(await LayoutSyncSmokeTests.RunAsync(mvvmManager));
                File.WriteAllLines(output, checks.Append($"PASS: {checks.Count(value => value.StartsWith("PASS:", StringComparison.Ordinal))} checks"));

                LayoutDocument FindDocument(object content) => manager.Layout.Descendents().OfType<LayoutDocument>()
                    .Single(value => ReferenceEquals(value.Content, content));
                void Check(bool condition, string description)
                {
                    if (!condition)
                    {
                        throw new InvalidOperationException(description);
                    }
                    checks.Add($"PASS: {description}");
                }
            }
            catch (Exception exception)
            {
                File.WriteAllLines(output, checks.Append($"FAIL: {exception}"));
                Environment.ExitCode = 1;
            }
            finally
            {
                mvvmManager?.Dispose();
                window.Manager.Dispose();
                window.Close();
                Application.Current.Exit();
            }
        }
    }

    private static async Task Settle() => await Task.Delay(180);
}
