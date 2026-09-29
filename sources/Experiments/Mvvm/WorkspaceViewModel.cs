using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AvalonDock.Core;
using AvalonDock.Mvvm;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Mvvm;

public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    private int nextDocumentNumber = 1;
    private int nextToolNumber = 1;
    private string statusText = "MVVM workspace using DockLayoutService, RootDock, and model content.";

    public WorkspaceViewModel()
    {
        Documents = [];
        Tools =
        [
            CreateTool("mvvm-files", "Files", DockZone.LeftTop, "File navigation provided by a ToolboxBase model."),
            CreateTool("mvvm-search", "Search", DockZone.LeftBottom, "Search results controlled by the model collection."),
            CreateTool("mvvm-inspector", "Inspector", DockZone.RightTop, "Properties for the active document."),
            CreateTool("mvvm-output", "Output", DockZone.BottomLeft, "Build and debug output appears here.")
        ];
        LayoutService = new DockLayoutService(Tools);

        OpenCommand = new RelayCommand(_ => OpenDocument($"Document {nextDocumentNumber}", "This document was created by the ViewModel.\n\nUse Edit to mark it as modified."));
        MarkModifiedCommand = new RelayCommand(_ => MarkActiveDocumentModified());
        SaveCommand = new RelayCommand(_ => SaveActiveDocument());
        CloseActiveCommand = new RelayCommand(_ => CloseActiveDocument());
        NextCommand = new RelayCommand(_ => ActivateNextDocument());
        ReplaceCommand = new RelayCommand(_ => ReplaceActiveDocument());
        ResetCommand = new RelayCommand(_ => ResetDocuments());
        AddToolCommand = new RelayCommand(_ => AddTool());
        ReplaceToolCommand = new RelayCommand(_ => ReplaceTool());
        ResetToolsCommand = new RelayCommand(_ => ResetTools());
        ToggleFloatingCommand = new RelayCommand(_ => ToggleFloating());

        OpenDocument("Main Document", "This note was added through DockLayoutService.OpenDocument.\n\nUse Edit to mark it as modified, then save it before closing.\n\nDrag the tab to rearrange or dock the document.\n");
        OpenDocument("Model Overview", "RootDock, DocumentDock, and ToolDock come from the MVVM package.\n\nThe window binds DockLayout and ICommand while the model tree stores the layout state.\n");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DockLayoutService LayoutService
    {
        get;
    }

    public RootDock Layout => (RootDock)LayoutService.Layout;

    public ObservableCollection<WorkspaceDocument> Documents
    {
        get;
    }

    public ObservableCollection<WorkspaceTool> Tools
    {
        get;
    }

    public WorkspaceDocument? ActiveDocument => Layout.ActiveDockable as WorkspaceDocument;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand OpenCommand
    {
        get;
    }

    public ICommand MarkModifiedCommand
    {
        get;
    }

    public ICommand SaveCommand
    {
        get;
    }

    public ICommand CloseActiveCommand
    {
        get;
    }

    public ICommand NextCommand
    {
        get;
    }

    public ICommand ReplaceCommand
    {
        get;
    }

    public ICommand ResetCommand
    {
        get;
    }

    public ICommand AddToolCommand
    {
        get;
    }

    public ICommand ReplaceToolCommand
    {
        get;
    }

    public ICommand ResetToolsCommand
    {
        get;
    }

    public ICommand ToggleFloatingCommand
    {
        get;
    }

    public WorkspaceDocument OpenDocument(string title, string text)
    {
        WorkspaceDocument document = new()
        {
            Id = $"mvvm-document-{nextDocumentNumber++}",
            Title = title,
            Text = text,
            IsModified = false
        };
        LayoutService.OpenDocument(document);
        Documents.Add(document);
        Layout.ActiveDockable = document;
        SetStatus($"Opened {document.Title}. Document models: {Documents.Count}.");
        return document;
    }

    public void CloseDocument(WorkspaceDocument document)
    {
        LayoutService.CloseDocument(document);
        OnDocumentClosed(document);
    }

    public void OnDocumentClosed(WorkspaceDocument document)
    {
        if (!Documents.Remove(document))
        {
            return;
        }

        SetStatus($"Closed {document.Title}. Document models: {Documents.Count}.");
        OnPropertyChanged(nameof(ActiveDocument));
    }

    public void ActivateDocument(WorkspaceDocument document)
    {
        Layout.ActiveDockable = document;
        OnPropertyChanged(nameof(ActiveDocument));
        SetStatus($"Active document: {document.Title}.");
    }

    public void SetStatus(string message) => StatusText = message;

    public void AddToolForScenario(WorkspaceTool tool)
    {
        if (Tools.Contains(tool))
        {
            return;
        }

        Tools.Add(tool);
        AddToolToLayout(tool);
        SetStatus($"Added tool model {tool.Title}.");
    }

    public void RemoveToolForScenario(WorkspaceTool tool)
    {
        RemoveToolFromLayout(tool);
        Tools.Remove(tool);
        SetStatus($"Removed tool model {tool.Title}.");
    }

    private void SaveActiveDocument()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("No active document.");
            return;
        }

        document.IsModified = false;
        SetStatus($"Saved {document.Title}.");
    }

    private void MarkActiveDocumentModified()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("No active document.");
            return;
        }

        document.IsModified = true;
        SetStatus($"Marked {document.Title} as modified.");
    }

    private void CloseActiveDocument()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("No active document.");
            return;
        }

        if (document.IsModified)
        {
            SetStatus($"{document.Title} has unsaved changes. Save it first.");
            return;
        }

        CloseDocument(document);
    }

    private void ActivateNextDocument()
    {
        if (Documents.Count == 0)
        {
            SetStatus("No documents are open.");
            return;
        }

        int index = ActiveDocument is { } active ? Documents.IndexOf(active) : -1;
        ActivateDocument(Documents[(index + 1) % Documents.Count]);
    }

    private void ReplaceActiveDocument()
    {
        if (ActiveDocument is not { } oldDocument)
        {
            SetStatus("No active document.");
            return;
        }

        int index = Documents.IndexOf(oldDocument);
        WorkspaceDocument replacement = new()
        {
            Id = $"mvvm-document-{nextDocumentNumber++}",
            Title = $"{oldDocument.Title} (Replacement)",
            Text = "The ViewModel's Replace command added this document to the layout.",
            IsModified = false
        };
        LayoutService.CloseDocument(oldDocument);
        Documents.RemoveAt(index);
        LayoutService.OpenDocument(replacement);
        Documents.Insert(index, replacement);
        Layout.ActiveDockable = replacement;
        SetStatus($"Replaced {oldDocument.Title}.");
    }

    private void ResetDocuments()
    {
        foreach (WorkspaceDocument document in Documents.ToArray())
        {
            LayoutService.CloseDocument(document);
        }

        Documents.Clear();
        OpenDocument("Reset Document", "The ViewModel reset the document collection.\n\nYou can continue opening, replacing, and saving documents.\n");
        SetStatus("Document collection reset.");
    }

    private void AddTool()
    {
        WorkspaceTool tool = CreateTool(
            $"mvvm-tool-{nextToolNumber}",
            $"Tool {nextToolNumber}",
            DockZone.RightBottom,
            "A ToolDock displays this runtime tool through VisibleDockables.");
        nextToolNumber++;
        AddToolForScenario(tool);
    }

    private void ReplaceTool()
    {
        WorkspaceTool? oldTool = Tools.FirstOrDefault();
        if (oldTool is null)
        {
            AddTool();
            return;
        }

        WorkspaceTool replacement = CreateTool(
            $"mvvm-tool-{nextToolNumber}",
            $"{oldTool.Title} (Replacement)",
            oldTool.Zone,
            "The ViewModel's Replace command added this tool to the layout.");
        nextToolNumber++;
        int index = Tools.IndexOf(oldTool);
        RemoveToolFromLayout(oldTool);
        Tools[index] = replacement;
        AddToolToLayout(replacement);
        SetStatus($"Replaced tool {oldTool.Title}.");
    }

    private void ResetTools()
    {
        foreach (WorkspaceTool tool in Tools.ToArray())
        {
            RemoveToolFromLayout(tool);
        }

        Tools.Clear();
        WorkspaceTool resetTool = CreateTool("mvvm-reset-tool", "Reset Tool", DockZone.LeftTop, "The tool collection was reset.");
        Tools.Add(resetTool);
        AddToolToLayout(resetTool);
        SetStatus("Tool collection reset.");
    }

    private void ToggleFloating()
    {
        Layout.AllowFloatingWindows = !Layout.AllowFloatingWindows;
        Layout.AllowDetachedWindows = Layout.AllowFloatingWindows;
        SetStatus(Layout.AllowFloatingWindows ? "Floating and detached windows are enabled." : "New floating and detached windows are disabled.");
    }

    private void AddToolToLayout(WorkspaceTool tool)
    {
        IToolDock? dock = FindToolDock(Layout, tool.Zone);
        if (dock is null || dock.VisibleDockables is not { } visibleDockables || visibleDockables.Any(item => ReferenceEquals(item, tool)))
        {
            return;
        }

        visibleDockables.Add(tool);
    }

    private void RemoveToolFromLayout(WorkspaceTool tool)
    {
        foreach (IToolDock dock in EnumerateDocks(Layout).OfType<IToolDock>())
        {
            if (dock.VisibleDockables is not { } visibleDockables)
            {
                continue;
            }

            IDockable? match = visibleDockables.FirstOrDefault(item => ReferenceEquals(item, tool));
            if (match is not null)
            {
                visibleDockables.Remove(match);
                return;
            }
        }
    }

    private static IToolDock? FindToolDock(IDockable defaultLayout, DockZone zone)
    {
        DockAlignment alignment = zone switch
        {
            DockZone.LeftTop or DockZone.LeftBottom => DockAlignment.Left,
            DockZone.RightTop or DockZone.RightBottom => DockAlignment.Right,
            _ => DockAlignment.Bottom
        };
        return EnumerateDocks(defaultLayout).OfType<IToolDock>().FirstOrDefault(item => item.Alignment == alignment)
            ?? EnumerateDocks(defaultLayout).OfType<IToolDock>().FirstOrDefault();
    }

    private static IEnumerable<IDockable> EnumerateDocks(IDockable? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;
        if (node is not IDock dock || dock.VisibleDockables is not { } visibleDockables)
        {
            yield break;
        }

        foreach (IDockable child in visibleDockables)
        {
            foreach (IDockable nested in EnumerateDocks(child))
            {
                yield return nested;
            }
        }
    }

    private static WorkspaceTool CreateTool(string id, string title, DockZone zone, string text)
    {
        return new WorkspaceTool
        {
            Id = id,
            Title = title,
            Zone = zone,
            IsOpenByDefault = true,
            Text = text
        };
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> execute;

    public RelayCommand(Action<object?> execute)
    {
        this.execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add
        {
        }
        remove
        {
        }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}
