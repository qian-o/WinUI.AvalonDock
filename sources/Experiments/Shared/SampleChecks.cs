using Microsoft.UI.Xaml;

namespace WinUI.AvalonDock.Experiments.Shared;

public sealed class SampleChecks
{
    private readonly List<string> results = [];

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public void Record(string message) => results.Add($"通过：{message}");

    public static Task SettleAsync() => Task.Delay(250);

    public static void RunWhenLoaded(Window window, Func<SampleChecks, Task> run)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, "--smoke-test");
        if (index < 0)
        {
            return;
        }

        if (index + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[index + 1]))
        {
            throw new ArgumentException("--smoke-test 后必须提供仓库外的绝对结果文件路径。");
        }

        string reportPath = arguments[index + 1];
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Loaded += OnLoaded;

        async void OnLoaded(object sender, RoutedEventArgs args)
        {
            root.Loaded -= OnLoaded;
            SampleChecks checks = new();
            try
            {
                await SettleAsync();
                await run(checks);
                window.Close();
                checks.Record("主窗口关闭并释放管理器");
                File.WriteAllLines(reportPath, checks.results);
                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                checks.results.Add($"失败：{exception}");
                File.WriteAllLines(reportPath, checks.results);
                Environment.ExitCode = 1;
                window.Close();
            }
        }
    }
}
