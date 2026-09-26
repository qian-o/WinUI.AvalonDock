// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/Shell/SystemCommands.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
namespace Microsoft.Windows.Shell;

using System;
using AvalonDock.Platforms;
using global::Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

/// <summary>
/// Provides helper members for system Commands.
/// </summary>
public static class SystemCommands
{
    /// <summary>Gets the native close-window command.</summary>
    public static XamlUICommand CloseWindowCommand
    {
        get;
    }
    /// <summary>Gets the native maximize-window command.</summary>
    public static XamlUICommand MaximizeWindowCommand
    {
        get;
    }
    /// <summary>Gets the native minimize-window command.</summary>
    public static XamlUICommand MinimizeWindowCommand
    {
        get;
    }
    /// <summary>Gets the native restore-window command.</summary>
    public static XamlUICommand RestoreWindowCommand
    {
        get;
    }
    /// <summary>Gets the application-handled native system-menu command.</summary>
    public static XamlUICommand ShowSystemMenuCommand
    {
        get;
    }

    static SystemCommands()
    {
        CloseWindowCommand = new XamlUICommand();
        MaximizeWindowCommand = new XamlUICommand();
        MinimizeWindowCommand = new XamlUICommand();
        RestoreWindowCommand = new XamlUICommand();
        ShowSystemMenuCommand = new XamlUICommand();

        // Original LayoutFloatingWindowControl.OnInitialized command-binding bodies.
        // WinUI supplies native command events instead of WPF's routed CommandBinding.
        CloseWindowCommand.ExecuteRequested += (s, args) => CloseWindow((Window)args.Parameter);
        MaximizeWindowCommand.ExecuteRequested += (s, args) => MaximizeWindow((Window)args.Parameter);
        MinimizeWindowCommand.ExecuteRequested += (s, args) => MinimizeWindow((Window)args.Parameter);
        RestoreWindowCommand.ExecuteRequested += (s, args) => RestoreWindow((Window)args.Parameter);
        ShowSystemMenuCommand.CanExecuteRequested += (s, args) => args.CanExecute = false;
    }
    /// <summary>
    /// Executes the post System Command operation.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="command">The command.</param>
    private static void PostSystemCommand(Window window, WindowSystemCommand command)
    {
        PlatformServices.CreateWindowSystemCommands(window).Post(command);
    }

    /// <summary>
    /// Executes the close Window operation.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void CloseWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        PostSystemCommand(window, WindowSystemCommand.Close);
    }

    /// <summary>
    /// Executes the maximize Window operation.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void MaximizeWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        PostSystemCommand(window, WindowSystemCommand.Maximize);
    }

    /// <summary>
    /// Executes the minimize Window operation.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void MinimizeWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        PostSystemCommand(window, WindowSystemCommand.Minimize);
    }

    /// <summary>
    /// Executes the restore Window operation.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void RestoreWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        PostSystemCommand(window, WindowSystemCommand.Restore);
    }

    /// <summary>
    /// Executes the show System Menu operation.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="screenLocation">The screen Location.</param>
    public static void ShowSystemMenu(Window window, Point screenLocation)
    {
        ArgumentNullException.ThrowIfNull(window);
        PlatformServices.CreateWindowSystemCommands(window).ShowSystemMenu(screenLocation);
    }

}
