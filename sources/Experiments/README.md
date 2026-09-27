# 实验项目

三个项目分别验证一种经典接入场景。它们都直接引用本仓库的 WinUI 控件，并复用最小的 [Shared](Shared/) 模型和布局项样式；项目之间不共享管理器实例，启动后可独立观察布局树和窗口生命周期。

| 项目 | 主要场景 | 覆盖入口 |
| --- | --- | --- |
| [Docking](Docking/) | 经典 XAML IDE 工作区 | `LayoutRoot`、`DocumentsSource`、`AnchorablesSource`、Add/Replace/Reset、可取消关闭、隐藏/自动隐藏、浮动/停靠、独立工具窗口、XML `ContentId` 恢复、文件/布局/工具/主题菜单 |
| [ToggleDocking](ToggleDocking/) | 六区工具工作台 | `DockLayoutService` 工具模型、六个 `DockZone`、区域移动、Toggle 展开/收起、隐藏恢复、独立工具窗口、文件/布局/视图/主题菜单 |
| [Mvvm](Mvvm/) | 官方 MVVM 数据驱动工作区 | `RootDock`/`DocumentDock`/`ToolDock`、`DockLayoutService`、文档 Add/Save/Close/Replace/Reset、工具 Add/Replace/Reset、活动项回写、窗口策略、XML `ContentId` 恢复、文件/编辑/工具/布局/主题菜单 |

## 操作重点

- 在 `Docking` 中打开“文件”菜单管理文档源，在“编辑”菜单标记活动文档为已修改，测试关闭取消和修改项关闭策略；在“布局”菜单保存/恢复、浮动/停靠，在“工具”菜单测试自动隐藏、显示和拆分。“替换文档源”和“重置文档源”检查集合同步。
- 在 `ToggleDocking` 中通过“布局”菜单把当前工具移动到六个 `DockZone`，通过“视图”菜单切换工具和布局优先级，拖动工具到停靠引导检查区域预览。
- 在 `Mvvm` 中使用“文件”菜单打开、保存和关闭文档；“编辑”菜单可标记活动文档为已修改，并验证下一个、替换和重置命令；“工具”菜单验证工具集合；“布局”菜单验证窗口策略和 XML 布局恢复。
- 三个项目都可以拖动文档标签和工具标题栏，右键文档标签可检查关闭、分组、浮动和停靠命令。窗口关闭时调用当前管理器的 `Dispose()`。
- 可用 `--smoke-test <绝对报告路径>` 运行自动场景检查；程序会创建真实 WinUI 窗口，验证模型引用、Add/Replace/Reset、关闭取消、区域移动、窗口策略、XML 恢复和 `Dispose()` 后自动退出。例如：

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug -- --smoke-test "$env:TEMP\Docking-smoke.txt"
dotnet run --project .\sources\Experiments\ToggleDocking\ToggleDocking.csproj -c Debug -- --smoke-test "$env:TEMP\ToggleDocking-smoke.txt"
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug -- --smoke-test "$env:TEMP\Mvvm-smoke.txt"
```
```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```
