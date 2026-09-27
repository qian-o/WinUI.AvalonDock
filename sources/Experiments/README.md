# 实验项目

三个项目分别验证一种经典接入场景。它们都直接引用本仓库的 WinUI 控件，并复用最小的 [Shared](Shared/) 模型和布局项样式；项目之间不共享管理器实例，启动后可独立观察布局树和窗口生命周期。

| 项目 | 主要场景 | 覆盖入口 |
| --- | --- | --- |
| [DockingManager](DockingManager/) | 经典 XAML IDE 工作区 | `LayoutRoot`、`DocumentsSource`、`AnchorablesSource`、Add/Replace/Reset、可取消关闭、隐藏/自动隐藏、浮动/停靠、独立工具窗口、XML `ContentId` 恢复、主题 |
| [ToggleDockingManager](ToggleDockingManager/) | 六区工具工作台 | `DockLayoutService` 工具模型、六个 `DockZone`、区域移动、Toggle 展开/收起、隐藏恢复、独立工具窗口、主题 |
| [Mvvm](Mvvm/) | 官方 MVVM 数据驱动工作区 | `RootDock`/`DocumentDock`/`ToolDock`、`DockLayoutService`、文档 Add/Save/Close/Replace/Reset、工具 Add/Replace/Reset、活动项回写、窗口策略、XML `ContentId` 恢复、主题 |

## 操作重点

- 在 `DockingManager` 中先编辑文档，再点击“关闭活动项”，确认可取消关闭；打开“允许关闭修改项”后再关闭。用“替换文档源”和“重置文档源”检查集合的 Replace/Reset 同步。
- 在 `ToggleDockingManager` 中点击六个区域按钮，把当前工具移动到六个 `DockZone`；点击工具按钮检查展开/收起，拖动工具到停靠引导检查区域预览。
- 在 `Mvvm` 中编辑文档后点击“保存活动文档”，再点击“关闭活动文档”；“替换活动文档”和“重置文档集合”验证模型命令，工具按钮验证 Add/Replace/Reset 集合同步。
- 三个项目都可以拖动文档标签和工具标题栏，右键文档标签可检查关闭、分组、浮动和停靠命令。窗口关闭时调用当前管理器的 `Dispose()`。
- 可用 `--smoke-test <绝对报告路径>` 运行自动场景检查；程序会创建真实 WinUI 窗口，验证模型引用、Add/Replace/Reset、关闭取消、区域移动、窗口策略、XML 恢复和 `Dispose()` 后自动退出。例如：

```powershell
dotnet run --project .\sources\Experiments\DockingManager\DockingManager.csproj -c Debug -- --smoke-test "$env:TEMP\DockingManager-smoke.txt"
dotnet run --project .\sources\Experiments\ToggleDockingManager\ToggleDockingManager.csproj -c Debug -- --smoke-test "$env:TEMP\ToggleDockingManager-smoke.txt"
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug -- --smoke-test "$env:TEMP\Mvvm-smoke.txt"
```
```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```
