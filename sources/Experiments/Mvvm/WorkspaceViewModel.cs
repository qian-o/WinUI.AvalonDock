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
    private string statusText = "MVVM：官方 DockLayoutService、RootDock 与模型内容。";

    public WorkspaceViewModel()
    {
        Documents = [];
        Tools =
        [
            CreateTool("mvvm-files", "文件", DockZone.LeftTop, "左侧文件导航：由 ToolboxBase 模型提供。"),
            CreateTool("mvvm-search", "搜索", DockZone.LeftBottom, "左侧搜索结果：由模型集合控制。"),
            CreateTool("mvvm-inspector", "检查器", DockZone.RightTop, "右侧属性检查：跟随活动文档。"),
            CreateTool("mvvm-output", "输出", DockZone.BottomLeft, "底部输出：适合构建和调试信息。")
        ];
        LayoutService = new DockLayoutService(Tools);

        OpenCommand = new RelayCommand(_ => OpenDocument($"文档 {nextDocumentNumber}", "通过 ViewModel 创建的文档。\n\n可在编辑菜单标记文档为已修改。"));
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

        OpenDocument("主文档", "这是由 DockLayoutService.OpenDocument 加入的便笺。\n\n在编辑菜单标记为已修改后，点击“保存活动文档”再测试关闭。\n\n拖动标签可以验证文档重排和停靠。\n");
        OpenDocument("模型说明", "RootDock、DocumentDock 和 ToolDock 由官方 MVVM 包提供。\n\n窗口只绑定 DockLayout 和 ICommand，布局状态仍由模型树保存。\n");
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
        SetStatus($"已打开 {document.Title}，文档模型数 {Documents.Count}。");
        return document;
    }

    public void CloseDocument(WorkspaceDocument document)
    {
        LayoutService.CloseDocument(document);
        Documents.Remove(document);
        SetStatus($"已关闭 {document.Title}，文档模型数 {Documents.Count}。");
        OnPropertyChanged(nameof(ActiveDocument));
    }

    public void ActivateDocument(WorkspaceDocument document)
    {
        Layout.ActiveDockable = document;
        OnPropertyChanged(nameof(ActiveDocument));
        SetStatus($"活动文档：{document.Title}。");
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
        SetStatus($"已添加工具模型 {tool.Title}。");
    }

    public void RemoveToolForScenario(WorkspaceTool tool)
    {
        RemoveToolFromLayout(tool);
        Tools.Remove(tool);
        SetStatus($"已移除工具模型 {tool.Title}。");
    }

    private void SaveActiveDocument()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("当前没有活动文档。");
            return;
        }

        document.IsModified = false;
        SetStatus($"已保存 {document.Title}。");
    }

    private void MarkActiveDocumentModified()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("当前没有活动文档。");
            return;
        }

        document.IsModified = true;
        SetStatus($"已标记 {document.Title} 为已修改。");
    }

    private void CloseActiveDocument()
    {
        if (ActiveDocument is not { } document)
        {
            SetStatus("当前没有活动文档。");
            return;
        }

        if (document.IsModified)
        {
            SetStatus($"{document.Title} 有未保存修改，请先保存。");
            return;
        }

        CloseDocument(document);
    }

    private void ActivateNextDocument()
    {
        if (Documents.Count == 0)
        {
            SetStatus("当前没有文档。");
            return;
        }

        int index = ActiveDocument is { } active ? Documents.IndexOf(active) : -1;
        ActivateDocument(Documents[(index + 1) % Documents.Count]);
    }

    private void ReplaceActiveDocument()
    {
        if (ActiveDocument is not { } oldDocument)
        {
            SetStatus("当前没有活动文档。");
            return;
        }

        int index = Documents.IndexOf(oldDocument);
        WorkspaceDocument replacement = new()
        {
            Id = $"mvvm-document-{nextDocumentNumber++}",
            Title = $"{oldDocument.Title}（替换）",
            Text = "这个文档通过 ViewModel 的 Replace 命令重新接入布局。",
            IsModified = false
        };
        LayoutService.CloseDocument(oldDocument);
        Documents.RemoveAt(index);
        LayoutService.OpenDocument(replacement);
        Documents.Insert(index, replacement);
        Layout.ActiveDockable = replacement;
        SetStatus($"已替换 {oldDocument.Title}。");
    }

    private void ResetDocuments()
    {
        foreach (WorkspaceDocument document in Documents.ToArray())
        {
            LayoutService.CloseDocument(document);
        }

        Documents.Clear();
        OpenDocument("重置后的文档", "文档集合已通过 ViewModel 重置。\n\n现在可以继续打开、替换和保存文档。\n");
        SetStatus("文档集合已重置。");
    }

    private void AddTool()
    {
        WorkspaceTool tool = CreateTool(
            $"mvvm-tool-{nextToolNumber}",
            $"工具 {nextToolNumber}",
            DockZone.RightBottom,
            "运行时添加的工具模型由 ToolDock 的 VisibleDockables 呈现。");
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
            $"{oldTool.Title}（替换）",
            oldTool.Zone,
            "这个工具通过 ViewModel 的 Replace 命令重新接入布局。");
        nextToolNumber++;
        int index = Tools.IndexOf(oldTool);
        RemoveToolFromLayout(oldTool);
        Tools[index] = replacement;
        AddToolToLayout(replacement);
        SetStatus($"已替换工具 {oldTool.Title}。");
    }

    private void ResetTools()
    {
        foreach (WorkspaceTool tool in Tools.ToArray())
        {
            RemoveToolFromLayout(tool);
        }

        Tools.Clear();
        WorkspaceTool resetTool = CreateTool("mvvm-reset-tool", "重置工具", DockZone.LeftTop, "工具集合已重置。");
        Tools.Add(resetTool);
        AddToolToLayout(resetTool);
        SetStatus("工具集合已重置。");
    }

    private void ToggleFloating()
    {
        Layout.AllowFloatingWindows = !Layout.AllowFloatingWindows;
        Layout.AllowDetachedWindows = Layout.AllowFloatingWindows;
        SetStatus(Layout.AllowFloatingWindows ? "模型已允许浮动和独立窗口。" : "模型已禁止新的浮动和独立窗口。");
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
