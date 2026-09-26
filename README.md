# WinUI.AvalonDock

WinUI.AvalonDock 是将 [Dirkster.AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 迁移到 WinUI 3 的非官方项目。目标是让 WinUI 应用沿用 AvalonDock 的布局树、停靠逻辑和常用接入方式，创建带文档、工具窗格、浮动窗口和可保存布局的桌面工作区。

项目直接使用 `Dirkster.AvalonDock.Core`，并以 [AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/e8a3da4e9761ff549e5a2afca2bdf891a681baf2) 作为默认外观参考。WPF 控件和窗口机制由 WinUI 3 的控件及 Windows 窗口能力承载。

当前运行目标是 Windows；平台接口已为以后接入 Uno 留出位置，现阶段不提供 Uno 实现。

原版功能清单、逐项核对结果和验证范围见[当前状态](memory/status.md)；执行范围见[工作计划](memory/work-plan.md)。设计边界和上游对应见[项目记忆](memory/README.md)与[固定上游](memory/upstream.md)。

## 从源码构建

在 Windows 上安装 .NET 10 SDK，并确保 NuGet 可恢复项目依赖，然后在仓库根目录运行：

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```

构建确认项目可编译；普通停靠交互和外观的操作结果见[当前状态](memory/status.md)。

运行单一示例：

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
```

示例包含经典 XAML 布局、MVVM/Toggle、常用停靠操作和内存中的 XML 布局保存恢复；操作说明见 [示例 README](sources/Experiments/Docking/README.md)。

## 许可与归属

本项目采用 [Microsoft Public License（MS-PL）](LICENSE)，并保留适用的上游版权声明。改编的第三方代码和资源见 [第三方声明](THIRD-PARTY-NOTICES.md)。本项目与 AvalonDock 官方项目无从属关系。
