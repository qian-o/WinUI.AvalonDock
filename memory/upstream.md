# 固定上游与功能对应

| 用途 | 固定来源 |
| --- | --- |
| 功能与核心逻辑 | [Dirkster.AvalonDock v5.0.0，提交 `408dc2896e2f41f3bb79a15207f160edee8a6792`](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) |
| 共用模型 | `Dirkster.AvalonDock.Core` 5.0.0 |
| MVVM 与布局持久化 | 官方 `Dirkster.AvalonDock.Mvvm`、`Dirkster.AvalonDock.Serializer.Xml` 5.0.0 |
| 默认外观 | [AvalonDock.Themes.WPFUI 1.2.2，提交 `fc0592716eb6e3de2c2becdbc48e67de6e331474`](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/fc0592716eb6e3de2c2becdbc48e67de6e331474) |

WPF 主库和 WPFUI 主题是源码与外观参考，不是产品运行依赖。升级上游版本需要单独核对；不要用随分支移动的源码作为迁移基线。

| 上游责任 | WinUI 迁移中的责任 |
| --- | --- |
| `Layout/`、布局引擎与策略 | 保存布局树、选择/激活、隐藏/浮动集合，决定布局变化 |
| `DockingManager`、`LayoutItem` | 接入应用数据源、命令、事件、模板和视图宿主 |
| `DragService`、drop areas、drop targets | 跟踪一次停靠操作，选择目标、产生预览并修改布局树 |
| pane、tab、grid、splitter 控件 | 把布局树投影为原生 WinUI 控件 |
| floating、auto-hide、detached、navigator | 在同一布局状态上提供不同窗口和交互呈现 |
| `ToggleDockingManager` | 用相同布局核心实现 Toggle 侧栏 |
| `LayoutSyncBridge`、`LayoutDtoMapper` | 接入官方 Core/MVVM 数据与序列化 DTO |
| WPFUI 主题 | 转译主要控件的结构、尺寸、颜色和普通交互状态 |

核对一个行为时，从正常应用操作追溯到上游责任，再决定本地代码保留、简化、替换或删除。WPF 类型可用等价的 WinUI 类型或局部适配替代；不要为了匹配声明数而模拟整套 WPF。实际改编的第三方代码和资源必须保留文件内声明及根目录的 [第三方声明](../THIRD-PARTY-NOTICES.md)。
