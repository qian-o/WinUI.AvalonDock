# WinUI 示例

在仓库根目录运行：

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
```

窗口包含 Classic XAML `LayoutRoot` 工作区，以及使用官方 MVVM 包的 Toggle 工作区。Toggle 控件由本仓库实现。按钮提供新增文档、浮动与停靠、工具自动隐藏与隐藏后显示、独立窗口、XML 布局保存恢复和浅色/深色切换的操作入口。布局只保存在内存中，恢复时按 `ContentId` 连接内容。窗口关闭会调用两个 `DockingManager.Dispose()`，模拟宿主永久移除页面时的释放路径；仅临时卸载页面时不要调用 `Dispose()`，重新加载仍会恢复窗口和焦点状态。标签拖动、菜单和导航器可在界面中检查。

普通操作：拖动文档标签检查重排、浮动和停靠引导；右键文档标签检查分组菜单，使用文档窗格的下拉菜单切换文档，按 `Ctrl+Tab` 打开导航器。工具标题栏可隐藏工具，随后用“显示隐藏工具”恢复；“拆分/附回工具”检查独立窗口。

这些入口用于核对迁移行为；已操作流程与当前完成状态见[当前状态](../../../memory/status.md)。
