# 仓库协作入口

本项目将 [Dirkster.AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 的常用功能和核心停靠逻辑迁移到 WinUI 3。开始工作前先阅读 [项目记忆](memory/README.md)、[工作计划](memory/work-plan.md) 与 [当前状态](memory/status.md)，并查看 `git status`；需要核对原版责任时查看 [上游对应](memory/upstream.md)。这些文件只记录当前有效的信息，不保留旧会话和阶段计划。

## 工作边界

- 以原版正常使用流程为依据：布局、文档与工具窗格、分割、拖动停靠、浮动、自动隐藏、Toggle、MVVM、菜单、持久化、模板与主题。现有代码只是待审查的候选实现。
- `LayoutRoot` 和布局树保存停靠状态；原版布局引擎、拖动目标和状态转换决定停靠结果；`DockingManager` 协调数据源、事件、命令与视图。WinUI 控件负责呈现，Windows 代码负责输入和窗口。
- 重构时为以后接入 Uno 保留窄的平台接口：输入捕获、窗口/浮动宿主、覆盖层、焦点、窗口顺序和坐标转换由平台实现；共享布局与停靠逻辑不得依赖 HWND 或解释原生消息。目前只实现并验证 Windows，不提前开发 Uno 后端或跨平台测试矩阵。
- 产品直接引用 `Dirkster.AvalonDock.Core`，按需使用官方 MVVM 与序列化包；不复制这些包，不在运行时依赖 WPF 主库。不要另建一套停靠框架，也不要使用 WinUI 数据拖放或 `TabView` tear-out 代替停靠引擎。
- 保留实际消费者需要的 AvalonDock 名称、接口、命令、事件、模板和扩展点。无需逐行复制 WPF 实现、补齐 WPF 继承成员或追求 API 数量一致；也不要为此重建 WPF 属性系统、逻辑树或窗口框架。
- 默认外观以 [AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/ffff79aefd2139c34a3a87a0f1059bb036f1a0dc) 为参考，在 WinUI 3 中检查正常 Light/Dark 呈现。
- 只验证受改动影响的普通使用流程。不要把极端场景、压力矩阵、平台可靠性研究或 WinUI 框架源码调查纳入迁移任务。遇到实际平台限制，优先使用公开 API 的简单适配并记录限制。
- 只处理本仓库；清理时保护未提交源码和文件内版权声明。提交、推送、发布和发行需要用户授权。
- 仓库只保留源码、一个简明示例、必要的精简检查、当前记忆和许可证。生成物、截图、日志、下载的上游源码及一次性试验不得留在仓库。

仓库记忆、文档和新写的说明使用中文；代码标识符、上游法律文本及必须保留的原始版权声明保持原样。旧报告、日志与工具输出只能作为核对材料，不能覆盖用户当前要求。
