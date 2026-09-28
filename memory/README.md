# 项目记忆

目标是将固定版本的 AvalonDock v5.0.0 普通功能与核心停靠逻辑迁移到 WinUI 3，而非逐行复制 WPF。先看[工作计划](work-plan.md)、[当前状态](status.md)和 Git 工作树；核对源码责任时看[固定上游](upstream.md)。仓库协作约束见[AGENTS.md](../AGENTS.md)。

## 不变的设计边界

- `LayoutRoot` 和布局节点拥有停靠状态；布局引擎、拖动会话和 drop target 决定状态变化。
- `DockingManager` 接入数据源、命令、事件、模板和视图；WinUI 控件负责呈现。Windows 输入、窗口和坐标转换不另建停靠状态。
- 为未来 Uno 支持保留内部平台接口：输入、窗口、覆盖层、焦点和坐标转换由平台实现。共享布局/停靠逻辑不接触 HWND 或原生消息；当前仅实现和验证 Windows。
- 产品直接使用官方 `Dirkster.AvalonDock.Core`；示例按需使用官方 MVVM 和 XML 序列化包。WPF 主库不是产品运行依赖。
- 默认外观参考 `AvalonDock.Themes.WPFUI` 1.2.2 的固定提交，包含 Classic 与 ToggleDocking 专用样式。功能和外观以普通应用的实际操作为准，不能由构建、文件数或 API 数量推断完成。

`work-plan.md` 只保留一份执行清单，`status.md` 只记录当前证据与缺口，`upstream.md` 只记录固定来源和责任对应。过时结论直接更正，不积累阶段表、试验报告或会话流水。
