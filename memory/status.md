# 当前状态

更新于 2026-09-28。以固定的 [AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 普通 Windows 使用流程为基准，要求功能与核心停靠逻辑一致，不追求 WPF API 数量或逐行复制。

## 实验项目重构

`Sources/Experiments` 已从一个混合窗口重构为三个独立、可运行的经典示例：

- `Docking`：经典 XAML `LayoutRoot`、`DocumentsSource`、`AnchorablesSource`，以及 Add/Replace/Reset、可取消关闭、隐藏/自动隐藏、浮动/停靠、独立工具窗口和 XML `ContentId` 恢复；菜单按文件、编辑、布局、工具和主题分组。
- `ToggleDocking`：官方 `DockLayoutService` 工具模型、六个 `DockZone`、区域移动、工具 Toggle、隐藏恢复和独立工具窗口；菜单按文件、布局、视图和主题分组。
- `Mvvm`：官方 `RootDock`、文档/工具模型和 `DockLayoutService`，覆盖模型驱动的打开、关闭、活动项、工具状态、窗口策略和 XML 恢复；菜单按文件、编辑、工具、布局和主题分组。

三个项目只共享 [Experiments/Shared](../sources/Experiments/Shared/) 中的示例模型和布局项样式；每个管理器和窗口都独立创建、独立释放，便于按场景定位问题。

## 本轮修复

- 对照固定版 WPFUI 的标签与菜单细节，修正文档标签右内边距和 20×20 关闭按钮；文档选择菜单标题左对齐，空图标不再触发原生图标列的 28 像素占位，模板图标绑定文档模型后可在图标加载时恢复图标列。文档菜单项的原生文本与标题同步，选择器补齐本地化提示及右侧间距。工具标签标题恢复居中，Toggle 禁用侧栏按钮和空工具窗格遵循参考状态。移除暗色文档表面的旧硬编码色，Light/Dark 改用同名主题色。Docking 真窗口检查已覆盖按钮图形、菜单标题距左边界、无图标及图标加载后的占位、自动化名称与两种主题色；实际弹出菜单已确认文字靠左。
- 窄窗格有六个文档时，原来的透明溢出标签仍占布局宽度，把右侧选择按钮挤出窗格；现按选择按钮预留宽度，隐藏标签释放自身宽度。动态加入的文档先于 `LayoutItem` 创建菜单项时缺少激活命令，现于布局项创建后补齐。Docking 真窗口检查已验证选择按钮留在窗格内，并可从菜单激活被隐藏的标签。
- 三个示例的内容模板改为自动换行的 `TextBlock`，移除文档和工具说明中的输入框边框与输入焦点；Docking、Mvvm 增加“标记活动文档为已修改”菜单入口，继续支持关闭取消与保存流程的操作验证。
- 修复 Classic 分隔条拖动时的预览开销：预览覆盖窗按分隔条实际矩形建立，尺寸和外观不变时移动已绘制的帧，不再每次重新套用模板并截取整个管理器。ToggleDocking 拖动缓存六区几何，只在目标或布局变化时重绘主指示层；随指针移动的标题浮签改为独立的小覆盖窗，释放前刷新几何以确定最终停靠区。
- 默认外观参考已更新到 `AvalonDock.Themes.WPFUI` 1.2.2（`fc0592716eb6e3de2c2becdbc48e67de6e331474`）。通用管理器模板支持 `Padding`，文档标签关闭按钮和右侧留白采用紧凑尺寸；ToggleDocking 有专用左右导航框、40 像素图标侧栏、细选中指示条、圆角工具窗格、32 像素标题栏、单工具隐藏标签条，以及虚线六区拖动预览和标题浮签。新版 Classic 工具窗格将填充与描边分层，描边保持在内容之上；只有直接托管在浮动窗口且恰好一个工具时才隐藏描边与圆角，两个工具共用浮动窗格时保留标签、圆角和活动描边。Light/Dark 颜色继续使用 WinUI 主题资源。
- Toggle 拖动指示层改用实际 `LayoutRootPanel`、左右导航框和可见窗格的屏幕边界，不再从管理器外框与侧栏宽度反推；底部窗格上方的分割器从侧区命中范围中扣除，侧栏底部按钮区按真实起点截断。预览轮廓直接使用目标区完整边界，不再额外向内收缩；插入线以分隔点为中心绘制，并只在两端保留 6 像素间距。运行时坐标探针确认六个内容区的外边界与 `LayoutRootPanel` 完全一致；Toggle smoke-test 另检查选中细条精确贴合按钮边缘并垂直居中。
- Tab 文档与工具标签头、单工具窗格标题栏的拖拽命中区域和文字对齐已修复：自定义头部根控件使用透明背景和双向拉伸，原生 `TabViewItem` 头部内容跟随拉伸对齐；文档与工具标题统一为左对齐、垂直居中，标签文字以外的空白区域也会进入现有拖动服务。原生工具标签在首次实现前产生的临时宽度不再长期缓存，进入可视树后会用完整标题重新测量，首次打开页面即可显示标签文字。
- `Docking` 示例的工具菜单改用 `LayoutAnchorable.Float()` / `Dock()` 切换标准浮动状态，创建与拖拽浮出相同的 `LayoutAnchorableFloatingWindowControl`，可继续参与停靠拖放；独立顶层窗口仍由名称明确的 `DetachAnchorableToWindow` 路径负责，不再冒充普通浮动命令。
- 文档浮动窗回停到直接挂在 `LayoutPanel` 下的文档窗格时，使用父布局原位替换；中心导入提前返回，避免左右工具区顺序被固定插入到索引 0 改变。
- 布局项样式区分消费者样式与管理器样式；`LayoutItemContainerStyle` 和 `StyleSelector` 清空时撤销管理器样式及绑定，消费者直接设置的样式仍可恢复。
- 浮动工具停入文档区按整个浮动窗格的所有 `LayoutAnchorable.CanDockAsTabbedDocument` 校验，并在回调后再次校验提交条件。
- Navigator 支持 Escape 取消、Shift+Tab 反向切换，释放 Shift 不会提前确认；XAML 与原生焦点记录互斥，恢复失败时才尝试另一类焦点目标。
- 新增 `DockingManager.Dispose()` 永久释放入口：解除布局与来源订阅、MVVM/Toggle 状态、覆盖层、自动隐藏计时器、浮动/独立窗口宿主和原生回调，同时保留可序列化布局模型。临时 `Unloaded` 仍只隐藏并在重新加载时恢复。
- `IPlatformServices`、`IKeyboardInputService`、`IChildWindowHostOwner` 将共享停靠逻辑与平台实现隔开；普通浮动/自动隐藏内容使用无句柄 `ChildWindowHost`，legacy `HwndHost`、HWND、Win32 消息和 DesktopWindowXamlSource 仅保留在 Windows provider。后续 Uno 可替换 provider，不需要修改布局树与 drop 规则。

## 原版功能逐项核对

| 原版普通功能 | 迁移代码 | 操作证据与状态 |
| --- | --- | --- |
| 布局树、文档/工具窗格、分割、尺寸、选中与活动项 | `Layout/`、Classic/Toggle 布局引擎 | 本次看到 Classic、Toggle 文档与工具窗格；分割、尺寸及状态路径经代码核对。 |
| 文档新增、关闭、重排、分组、浮动与停靠 | `DockingManager.Sources`、`LayoutItem`、文档窗格及浮动窗 | 本次看到文档浮动及返回主窗；此前用户确认真实鼠标拖动。菜单与分组路径经代码核对。 |
| 工具四边停靠、隐藏/显示、自动隐藏/固定、停入文档区 | `LayoutAnchorable`、工具控件及 drop target | 此前示例操作过隐藏/显示和自动隐藏；四边及文档区路径经代码核对。 |
| 标签、窗格、浮动窗拖动及中心/边缘/外侧引导 | `DragService`、`OverlayWindow.Targets`、`DropTarget` | 此前用户确认文档真实拖动和引导停回；本次未重新拖动每种目标。 |
| 浮动窗口、重新停靠、最大化/还原 | `LayoutFloatingWindowControl`、`Platforms/Windows` | Classic smoke-test 已创建标准工具浮动宿主并停靠回原布局；此前看到文档浮动窗口、标签及内容，最大化/还原沿用此前操作证据。 |
| v5 独立工具窗口拆分/附回 | `DockingManager.DetachedWindows`、示例按钮 | 此前示例操作过；本次核对了取消操作不提前附回窗口的代码路径。 |
| Classic、Toggle 及 Toggle 六个停靠区 | 两种管理器、`ToggleLayoutEngine` | 本次看到两种模式和 Toggle 工具栏；六区切换代码路径保留。 |
| `DocumentsSource`、`AnchorablesSource`、MVVM | `DockingManager.Sources`、`LayoutSyncBridge`、官方 MVVM 包 | 三个示例分别验证源集合 Add/Replace/Reset、模型引用身份、文档关闭回写、官方 `DockLayoutService` 和 RootDock；自动场景检查已通过。 |
| 命令、事件及可取消关闭/隐藏/停靠/浮动 | `Commands`、`LayoutItem`、`DockingManager` | 修复了默认浮动命令重复发事件、拆分窗口取消后仍附回及示例把独立窗口误作普通浮动入口的问题；Classic smoke-test 已覆盖工具浮动/停靠命令。 |
| 菜单、侧栏切换、Ctrl+Tab 导航器 | `DockingManager.Menus`、`Navigator`、Toggle 控件 | 此前示例操作过文档菜单与导航器；本次代码路径核对。 |
| XML 布局保存/恢复、`ContentId` 重连 | 官方 XML 包、`LayoutDtoMapper`、三个示例恢复回调 | Classic 和 MVVM smoke-test 已验证内存 XML 保存、恢复及内容引用重连；Toggle 项目不重复覆盖 XML 持久化。 |
| 模板、默认主题及 Light/Dark | `Themes/`、模板属性、示例主题按钮 | 已对照 WPFUI 1.2.2 同步 Classic 双工具浮动窗格轮廓；Toggle 专用模板及六区拖动范围仍按原有检查，Light/Dark 使用同一组 WinUI 主题资源。 |

**结论：**本次代码审查没有发现固定 Windows 普通功能清单中仍缺实现的项目；表中未实操的行为不能算作本次操作验收通过。Uno 后端是以后新增的平台支持，不计入当前迁移缺口。

## 本次代码审查与验证

- 内容模板调整后完整解决方案 Debug 构建通过，0 警告、0 错误；Docking、ToggleDocking、Mvvm 三个示例 smoke test 均通过。Docking 实际窗口确认文档说明以文本块呈现、无输入框边框，修改项菜单路径由场景检查验证。
- 性能修复后产品、Docking 与 ToggleDocking 示例构建均通过；两个示例现有 smoke test 通过。实际窗口中将两个文档拆为左右窗格并拖动分隔条，确认窄条预览出现且释放后宽度改变；在 Toggle 示例中从左下工具标题拖过六区到右下，确认目标高亮、标题浮签和最终落位。未记录拖动帧时间基准。
- 修正了 MVVM 工具侧边导入顺序、内容引用身份判断、工具 `CanMove` 回调、浮动事件重复发送、拆分工具取消操作的状态顺序、多显示器浮动边界和运行时自动隐藏延时；删除无调用的重复屏幕定位方法。保留原有版权声明。
- 依据 [Zenith.NET](https://github.com/qian-o/Zenith.NET) 的现有源码，统一 224 个手写 C# 文件的四空格、LF、文件作用域命名空间、控制流大括号、导入顺序、字段命名、显式局部类型及可推断类型的 `new()`；按实际生命周期标注并处理可空值。手写源码中的 `#nullable disable` 已清零；仅原样保留上游自动生成的 `Resources.Designer.cs` 中 1 处。`.editorconfig` 固定风格规则，`IDE0003`、`IDE0008`、`IDE0011`、`IDE0090`、`IDE0161`、`IDE1006` 检查通过。
- 三个示例项目已统一为 `Docking`、`ToggleDocking`、`Mvvm`，顶部操作区使用 WinUI `MenuBar`：Docking 对应经典 File/Edit/Layout/Tools/Theme 工作流，ToggleDocking 对应 File/Layout/View/Theme 六区侧栏工作流，Mvvm 对应 File/Edit/Tools/Layout/Theme 模型工作流；原有场景验证入口保留在菜单中。
- 本轮验证：`dotnet build .\WinUI.AvalonDock.slnx -c Debug --no-restore /p:BuildInParallel=false` 通过，0 警告、0 错误；Docking、ToggleDocking、Mvvm 三个 `--smoke-test` 均通过，其中 Classic 检查首次工具标签尺寸以及标准工具浮动/停靠，Toggle 检查专用管理器、侧栏、窗格、标题模板、区域指示方向和 Light/Dark 资源切换；`dotnet format whitespace --verify-no-changes` 通过。
- 对照 WPFUI 1.2.2 的源码差异，本次上游样式改动集中在 Classic `LayoutAnchorablePaneControl.xaml`：双工具浮动时保留顶层圆角描边，单工具直接浮动时隐藏描边。Docking 真实窗口与 smoke test 均确认双工具浮动外观；场景检查还验证活动描边、双工具缩回单工具后的隐藏状态以及停靠返回。ToggleDocking smoke test 和空白格式检查通过。
- `DockingManager` 示例用 `ObservableCollection` 明确提供 Add、Replace、Reset 入口；`Mvvm` 示例使用官方 MVVM 包和 `ICommand` 绑定；三个应用的 XAML、启动入口、清单、共享模型和 smoke-test 基础设施均已纳入解决方案。
- 产品直接引用官方 Core；示例按需引用官方 MVVM 与 XML 序列化包。Windows 平台服务仍隔离输入、窗口、覆盖层、焦点、窗口顺序和坐标转换；共享布局与停靠逻辑未引入 HWND 或原生消息。仓库未提交、推送或发布。
