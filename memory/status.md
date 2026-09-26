# 当前状态

更新于 2026-09-26。以固定的 [AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 普通 Windows 使用流程为基准，要求功能与核心停靠逻辑一致，不追求 WPF API 数量或逐行复制。

## 原版功能逐项核对

| 原版普通功能 | 迁移代码 | 操作证据与状态 |
| --- | --- | --- |
| 布局树、文档/工具窗格、分割、尺寸、选中与活动项 | `Layout/`、Classic/Toggle 布局引擎 | 本次看到 Classic、Toggle 文档与工具窗格；分割、尺寸及状态路径经代码核对。 |
| 文档新增、关闭、重排、分组、浮动与停靠 | `DockingManager.Sources`、`LayoutItem`、文档窗格及浮动窗 | 本次看到文档浮动及返回主窗；此前用户确认真实鼠标拖动。菜单与分组路径经代码核对。 |
| 工具四边停靠、隐藏/显示、自动隐藏/固定、停入文档区 | `LayoutAnchorable`、工具控件及 drop target | 此前示例操作过隐藏/显示和自动隐藏；四边及文档区路径经代码核对。 |
| 标签、窗格、浮动窗拖动及中心/边缘/外侧引导 | `DragService`、`OverlayWindow.Targets`、`DropTarget` | 此前用户确认文档真实拖动和引导停回；本次未重新拖动每种目标。 |
| 浮动窗口、重新停靠、最大化/还原 | `LayoutFloatingWindowControl`、`Platforms/Windows` | 本次看到文档浮动窗口、标签及内容；返回主窗后界面工具的窗口读取报错，最大化/还原沿用此前操作证据。 |
| v5 独立工具窗口拆分/附回 | `DockingManager.DetachedWindows`、示例按钮 | 此前示例操作过；本次核对了取消操作不提前附回窗口的代码路径。 |
| Classic、Toggle 及 Toggle 六个停靠区 | 两种管理器、`ToggleLayoutEngine` | 本次看到两种模式和 Toggle 工具栏；六区切换代码路径保留。 |
| `DocumentsSource`、`AnchorablesSource`、MVVM | `DockingManager.Sources`、`LayoutSyncBridge`、官方 MVVM 包 | 修复了工具首次/动态/Reset 导入时的侧边对齐顺序，集合按内容对象引用身份同步；本次看到官方 MVVM 示例布局，未逐个操作 Replace/Reset。 |
| 命令、事件及可取消关闭/隐藏/停靠/浮动 | `Commands`、`LayoutItem`、`DockingManager` | 修复了默认浮动命令重复发事件及拆分窗口取消后仍附回的问题；事件路径经代码核对，未逐个触发回调。 |
| 菜单、侧栏切换、Ctrl+Tab 导航器 | `DockingManager.Menus`、`Navigator`、Toggle 控件 | 此前示例操作过文档菜单与导航器；本次代码路径核对。 |
| XML 布局保存/恢复、`ContentId` 重连 | 官方 XML 包、`LayoutDtoMapper`、示例回调 | 此前示例操作过保存/恢复；本次未重新恢复。JSON 包未接入此单一示例。 |
| 模板、默认主题及 Light/Dark | `Themes/`、模板属性、示例主题按钮 | 此前对照固定 WPFUI 参考检查过浅色、深色与浮动窗；本次模板构建通过，未重新切换主题。 |

**结论：**本次代码审查没有发现固定 Windows 普通功能清单中仍缺实现的项目；表中未实操的行为不能算作本次操作验收通过。Uno 后端是以后新增的平台支持，不计入当前迁移缺口。

## 本次代码审查与验证

- 修正了 MVVM 工具侧边导入顺序、内容引用身份判断、工具 `CanMove` 回调、浮动事件重复发送、拆分工具取消操作的状态顺序、多显示器浮动边界和运行时自动隐藏延时；删除无调用的重复屏幕定位方法。保留原有版权声明。
- 依据 [Zenith.NET](https://github.com/qian-o/Zenith.NET) 的现有源码，统一 224 个手写 C# 文件的四空格、LF、文件作用域命名空间、控制流大括号、导入顺序、字段命名、显式局部类型及可推断类型的 `new()`；按实际生命周期标注并处理可空值。手写源码中的 `#nullable disable` 已清零；仅原样保留上游自动生成的 `Resources.Designer.cs` 中 1 处。`.editorconfig` 固定风格规则，`IDE0003`、`IDE0008`、`IDE0011`、`IDE0090`、`IDE0161`、`IDE1006` 检查通过。
- 完整解决方案 Debug `--no-restore` 构建通过：0 警告、0 错误；手写 C# 的空白格式和上述风格规则检查通过。示例可正常启动并显示主窗，再正常关闭。可空性改动的正常调用路径已做代码复核，未发现可证实的回归；本次没有逐项重做界面操作，其他功能的操作证据见上表。
- 产品直接引用官方 Core；示例按需引用官方 MVVM 与 XML 序列化包。Windows 平台服务仍隔离输入、窗口、覆盖层、焦点、窗口顺序和坐标转换；共享布局与停靠逻辑未引入 HWND 或原生消息。仓库未提交、推送或发布。
