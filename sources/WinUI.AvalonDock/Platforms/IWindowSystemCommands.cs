using Windows.Foundation;

namespace AvalonDock.Platforms;

/// <summary>原生窗口命令和系统菜单操作。</summary>
internal interface IWindowSystemCommands
{
    void Post(WindowSystemCommand command);
    /// <summary>Shows the system menu at desktop logical coordinates using the window's current scale.</summary>
    void ShowSystemMenu(Point screenLocation);
}

internal enum WindowSystemCommand
{
    Close, Maximize, Minimize, Restore
}
