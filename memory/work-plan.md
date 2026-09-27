更新于 2026-09-27。以固定的 [AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 普通 Windows 使用流程为基准，要求功能与核心停靠逻辑一致，不追求 WPF API 数量或逐行复制。

## 实验项目重构

`Sources/Experiments` 现保留三个独立的经典示例项目：

- `DockingManager`：用 XAML `LayoutRoot`、`DocumentsSource` 和 `AnchorablesSource` 展示经典 IDE 工作区，并提供 Add/Replace/Reset、可取消关闭、隐藏/自动隐藏、浮动/停靠、独立工具窗口和 XML `ContentId` 恢复。
- `ToggleDockingManager`：用官方 `DockLayoutService` 工具模型展示六个 `DockZone`，提供区域移动、工具 Toggle、隐藏恢复和独立工具窗口。
- `Mvvm`：用官方 `RootDock`、文档/工具模型和 `DockLayoutService` 展示模型驱动的打开、关闭、活动项、工具状态、窗口策略和 XML 恢复。

三个项目共用 [Experiments/Shared](../sources/Experiments/Shared/) 中的最小模型和样式，不再用一个窗口切换多个无关模式。

## 当前执行记录

- 已清除没有产品调用的旧 WPF 兼容路径及重复的窗格、命令实现；保留布局树、停靠规则、消费者所需的模板和窄平台接口。
- 已完成三个实验项目的源码、解决方案入口和中文操作说明；普通停靠交互、浮动窗口和主题呈现的实操证据仍以状态文档为准。
- 已按 Zenith.NET 现有源码收敛手写 C# 的格式、命名、显式局部类型与可空契约，并完成解决方案构建和风格检查；操作证据仍以状态文档为准。
- 已完成本轮反馈修复：文档停靠顺序、布局项样式清理、整窗停靠权限、Navigator 键盘、焦点恢复和永久窗口释放均已收口；Win32 能力通过可替换 provider 与语义子窗口接口隔离，Windows 实现集中在 `Platforms/Windows`，为 Uno 后端保留替换边界。

Uno 后端与非 Windows 验证留待后续支持需求。
