# WinUI.AvalonDock

WinUI.AvalonDock 是将 [Dirkster.AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 迁移到 WinUI 3 的非官方项目。目标是让 WinUI 应用沿用 AvalonDock 的布局树、停靠逻辑和常用接入方式，创建带文档、工具窗格、浮动窗口和可保存布局的桌面工作区。

项目直接使用 `Dirkster.AvalonDock.Core`，并以 [AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/ffff79aefd2139c34a3a87a0f1059bb036f1a0dc) 作为默认外观参考。WPF 控件和窗口机制由 WinUI 3 的控件及 Windows 窗口能力承载。

当前运行目标是 Windows；平台接口已为以后接入 Uno 留出位置，现阶段不提供 Uno 实现。共享停靠逻辑只依赖 `IPlatformServices`、`IKeyboardInputService` 和 `IChildWindowHostOwner` 等语义接口；Win32、HWND、窗口 subclass、DesktopWindowXamlSource、坐标转换和原生键盘状态集中在 `Platforms/Windows` provider。宿主永久移除 `DockingManager` 时应在 UI 线程调用 `Dispose()`；临时视觉树卸载不需要调用它。

原版功能清单、逐项核对结果和验证范围见[当前状态](memory/status.md)；执行范围见[工作计划](memory/work-plan.md)。设计边界和上游对应见[项目记忆](memory/README.md)与[固定上游](memory/upstream.md)。

## 从源码构建

在 Windows 上安装 .NET 10 SDK，并确保 NuGet 可恢复项目依赖，然后在仓库根目录运行：

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```

构建确认项目可编译；普通停靠交互和外观的操作结果见[当前状态](memory/status.md)。

运行三个经典示例：

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
dotnet run --project .\sources\Experiments\ToggleDocking\ToggleDocking.csproj -c Debug
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug
```

示例按场景拆分为 [Docking 经典工作区](sources/Experiments/Docking/)、[ToggleDocking 六区工作台](sources/Experiments/ToggleDocking/) 和 [MVVM 数据驱动工作区](sources/Experiments/Mvvm/)，共用最小的示例模型与布局项样式。每个程序都提供文档、工具窗格、停靠状态、主题和释放路径的操作入口；详细覆盖范围见[实验项目说明](sources/Experiments/README.md)。

## 许可与归属

本项目采用 [Microsoft Public License（MS-PL）](LICENSE)，并保留适用的上游版权声明。改编的第三方代码和资源见 [第三方声明](THIRD-PARTY-NOTICES.md)。本项目与 AvalonDock 官方项目无从属关系。
