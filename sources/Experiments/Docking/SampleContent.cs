using AvalonDock.Mvvm;

namespace WinUI.AvalonDock.Experiments.Docking;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class SampleDocument : Document
{
    private string text = string.Empty;

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value);
    }
}

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class SampleTool : ToolboxBase
{
    private string text = string.Empty;

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value);
    }
}
