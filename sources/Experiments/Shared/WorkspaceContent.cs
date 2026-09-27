using AvalonDock.Mvvm;

namespace WinUI.AvalonDock.Experiments.Shared;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class WorkspaceDocument : Document
{
    private string text = string.Empty;

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value))
            {
                IsModified = true;
            }
        }
    }
}

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class WorkspaceTool : ToolboxBase
{
    private string text = string.Empty;

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value);
    }
}

