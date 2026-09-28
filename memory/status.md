# 当前状态

更新于 2026-09-28。以固定的 [AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) 普通 Windows 使用流程为基准，要求功能与核心停靠逻辑一致，不追求 WPF API 数量或逐行复制。

## 实验项目重构

`Sources/Experiments` 已从一个混合窗口重构为三个独立、可运行的经典示例：

- `Docking`：经典 XAML `LayoutRoot`、`DocumentsSource`、`AnchorablesSource`，以及 Add/Replace/Reset、可取消关闭、隐藏/自动隐藏、浮动/停靠、独立工具窗口和 XML `ContentId` 恢复；菜单按文件、编辑、布局、工具和主题分组。
- `ToggleDocking`：官方 `DockLayoutService` 工具模型、六个 `DockZone`、区域移动、工具 Toggle、隐藏恢复和独立工具窗口；菜单按文件、布局、视图和主题分组。
- `Mvvm`：官方 `RootDock`、文档/工具模型和 `DockLayoutService`，覆盖模型驱动的打开、关闭、活动项、工具状态、窗口策略和 XML 恢复；菜单按文件、编辑、工具、布局和主题分组。

三个项目只共享 [Experiments/Shared](../sources/Experiments/Shared/) 中的示例模型和布局项样式；每个管理器和窗口都独立创建、独立释放，便于按场景定位问题。

## 本轮修复

- 用户截图中的 Classic 上下文档分组在下方文档浮出后，剩余窗格已扩展，但中央停靠指示器仍停在旧上半区中心。原因是 `DropArea.DetectionRect` 在拖动开始时只测量一次，同一旧边界还用于停靠命中。现对已连接的窗格按当前屏幕边界读取检测矩形，未连接或隐藏的空窗格保留原有回退边界。Docking 真实窗口 smoke 场景按“上下分组→浮出下方文档”检查同一停靠区域对象的边界扩展和原下半区命中，并在检查后停回；尚未以真实鼠标重演截图中的指示器外观。
- 对照固定版 WPFUI 的标签与菜单细节，修正文档标签右内边距和 20×20 关闭按钮；文档选择菜单标题左对齐，空图标不再触发原生图标列的 28 像素占位，模板图标绑定文档模型后可在图标加载时恢复图标列。文档菜单项的原生文本与标题同步，选择器补齐本地化提示及右侧间距。工具标签标题恢复居中，Toggle 禁用侧栏按钮和空工具窗格遵循参考状态。移除暗色文档表面的旧硬编码色，Light/Dark 改用同名主题色。Docking 真窗口检查已覆盖按钮图形、菜单标题距左边界、无图标及图标加载后的占位、自动化名称与两种主题色；实际弹出菜单已确认文字靠左。
- 窄窗格有六个文档时，原来的透明溢出标签仍占布局宽度，把右侧选择按钮挤出窗格；现按选择按钮预留宽度，隐藏标签释放自身宽度。动态加入的文档先于 `LayoutItem` 创建菜单项时缺少激活命令，现于布局项创建后补齐。Docking 真窗口检查已验证选择按钮留在窗格内，并可从菜单激活被隐藏的标签。
- 三个示例的内容模板改为自动换行的 `TextBlock`，移除文档和工具说明中的输入框边框与输入焦点；Docking、Mvvm 增加“标记活动文档为已修改”菜单入口，继续支持关闭取消与保存流程的操作验证。
- 修复 Classic 分隔条拖动时的预览开销：预览覆盖窗按分隔条实际矩形建立，尺寸和外观不变时移动已绘制的帧，不再每次重新套用模板并截取整个管理器。ToggleDocking 拖动缓存六区几何，只在目标或布局变化时重绘主指示层；随指针移动的标题浮签改为独立的小覆盖窗，释放前刷新几何以确定最终停靠区。
- 默认外观参考已更新到 `AvalonDock.Themes.WPFUI` 1.2.2（`fc0592716eb6e3de2c2becdbc48e67de6e331474`）。通用管理器模板支持 `Padding`，文档标签关闭按钮和右侧留白采用紧凑尺寸；ToggleDocking 有专用左右导航框、40 像素图标侧栏、细选中指示条、圆角工具窗格、32 像素标题栏、单工具隐藏标签条，以及虚线六区拖动预览和标题浮签。新版 Classic 工具窗格将填充与描边分层，描边保持在内容之上；只有直接托管在浮动窗口且恰好一个工具时才隐藏描边与圆角，两个工具共用浮动窗格时保留标签、圆角和活动描边。Light/Dark 颜色继续使用 WinUI 主题资源。
- Toggle 拖动指示层使用实际 `LayoutRootPanel`、左右导航框和可见窗格的屏幕边界。用户截图指出一对分组只剩一个展开窗格时，完整实测窗格与空区的半幅预览重叠；现将单个窗格占据的分组对半切分，两区均空时从面板推算并对半切分，两区均展开时仍使用各自实测边界以对齐分隔条。预览轮廓使用目标区完整边界；侧栏底部按钮区按真实起点截断，插入线以分隔点为中心绘制。Toggle 示例真实窗口场景检查已覆盖左右侧及底部各对区域的双窗格、两种单窗格和双空布局，确认预览边界不重叠且中心命中正确；本次未再手工拖动检查轮廓。侧栏选中细条另有贴边和居中场景检查。
- Tab 文档与工具标签头、单工具窗格标题栏的拖拽命中区域和文字对齐已修复。文档标签按固定版 AvalonDock 的自然宽度及 WPFUI 的 100～240 像素限制排列；两个短标题因同受 100 像素下限约束而等宽，空间不足时隐藏完整标签。工具标签按自然宽度排列，仅溢出时均分压缩。原生文档标签回停后的重新测量会恢复最小宽度，短标题不再缩成紧凑标签。头部透明命中面与外层标签的空白按下转发覆盖文字至关闭按钮之间的区域，关闭按钮独立处理。原生工具标签进入可视树后会用完整标题重新测量，首次打开页面即可显示标签文字。
- `Docking` 示例的工具菜单改用 `LayoutAnchorable.Float()` / `Dock()` 切换标准浮动状态，创建与拖拽浮出相同的 `LayoutAnchorableFloatingWindowControl`，可继续参与停靠拖放；独立顶层窗口仍由名称明确的 `DetachAnchorableToWindow` 路径负责，不再冒充普通浮动命令。
- 文档浮动窗回停到直接挂在 `LayoutPanel` 下的文档窗格时，使用父布局原位替换；中心导入提前返回，避免左右工具区顺序被固定插入到索引 0 改变。
- 布局项样式区分消费者样式与管理器样式；`LayoutItemContainerStyle` 和 `StyleSelector` 清空时撤销管理器样式及绑定，消费者直接设置的样式仍可恢复。
- 浮动工具停入文档区按整个浮动窗格的所有 `LayoutAnchorable.CanDockAsTabbedDocument` 校验，并在回调后再次校验提交条件。
- Navigator 支持 Escape 取消、Shift+Tab 反向切换，释放 Shift 不会提前确认；XAML 与原生焦点记录互斥，恢复失败时才尝试另一类焦点目标。
- 新增 `DockingManager.Dispose()` 永久释放入口：解除布局与来源订阅、MVVM/Toggle 状态、覆盖层、自动隐藏计时器、浮动/独立窗口宿主和原生回调，同时保留可序列化布局模型。临时 `Unloaded` 仍只隐藏并在重新加载时恢复。
- `IPlatformServices`、`IKeyboardInputService`、`IChildWindowHostOwner` 将共享停靠逻辑与平台实现隔开；普通浮动/自动隐藏内容使用无句柄 `ChildWindowHost`，legacy `HwndHost`、HWND、Win32 消息和 DesktopWindowXamlSource 仅保留在 Windows provider。后续 Uno 可替换 provider，不需要修改布局树与 drop 规则。
- 本次按固定上游责任分组复查普通功能。修复 Toggle 侧栏“隐藏”在取消后仍移除按钮、视图菜单先附回独立窗口再执行可取消的隐藏/浮动命令，以及示例“附回当前工具”误附回全部窗口；修复 MVVM 示例通过文档标签关闭后独立 `Documents` 列表残留已关闭模型。`LayoutItem` 首次应用样式时释放模型播种的本地依赖属性值，使 `CanClose` 样式可覆盖工具的默认值。

## 原版功能逐项核对

| 原版普通功能 | 迁移代码 | 操作证据与状态 |
| --- | --- | --- |
| 布局树、文档/工具窗格、分割、尺寸、选中与活动项 | `Layout/`、Classic/Toggle 布局引擎 | 本次看到 Classic、Toggle 文档与工具窗格；分割、尺寸及状态路径经代码核对。 |
| 文档新增、关闭、重排、分组、浮动与停靠 | `DockingManager.Sources`、`LayoutItem`、文档窗格及浮动窗 | 本次看到文档浮动及返回主窗；此前用户确认真实鼠标拖动。菜单与分组路径经代码核对。 |
| 工具四边停靠、隐藏/显示、自动隐藏/固定、停入文档区 | `LayoutAnchorable`、工具控件及 drop target | 此前示例操作过隐藏/显示和自动隐藏；四边及文档区路径经代码核对。 |
| 标签、窗格、浮动窗拖动及中心/边缘/外侧引导 | `DragService`、`OverlayWindow.Targets`、`DropTarget` | 此前用户确认文档真实拖动和引导停回；本次未重新拖动每种目标。 |
| 浮动窗口、重新停靠、最大化/还原 | `LayoutFloatingWindowControl`、`Platforms/Windows` | Classic smoke-test 已创建标准工具浮动宿主并停靠回原布局；此前看到文档浮动窗口、标签及内容，最大化/还原沿用此前操作证据。 |
| v5 独立工具窗口拆分/附回 | `DockingManager.DetachedWindows`、示例按钮 | 本轮真实窗口场景检查确认取消隐藏/浮动保留独立窗口，附回当前工具不影响其他独立窗口；此前示例也操作过拆分与附回。 |
| Classic、Toggle 及 Toggle 六个停靠区 | 两种管理器、`ToggleLayoutEngine` | 本次看到两种模式和 Toggle 工具栏；六区切换代码路径保留。 |
| `DocumentsSource`、`AnchorablesSource`、MVVM | `DockingManager.Sources`、`LayoutSyncBridge`、官方 MVVM 包 | 三个示例分别验证源集合 Add/Replace/Reset、模型引用身份、文档关闭回写、官方 `DockLayoutService` 和 RootDock；MVVM 另验证标签关闭后列表同步及下一个文档命令。 |
| 命令、事件及可取消关闭/隐藏/停靠/浮动 | `Commands`、`LayoutItem`、`DockingManager` | 本轮真实窗口场景检查确认 Toggle 隐藏和浮动被取消时保留侧栏按钮与独立窗口；Classic 验证工具浮动/停靠及 `CanClose` 样式。其他停靠取消路径经代码核对。 |
| 菜单、侧栏切换、Ctrl+Tab 导航器 | `DockingManager.Menus`、`Navigator`、Toggle 控件 | 此前示例操作过文档菜单与导航器；本次代码路径核对。 |
| XML 布局保存/恢复、`ContentId` 重连 | 官方 XML 包、`LayoutDtoMapper`、三个示例恢复回调 | Classic 和 MVVM smoke-test 已验证内存 XML 保存、恢复及内容引用重连；Toggle 项目不重复覆盖 XML 持久化。 |
| 模板、默认主题及 Light/Dark | `Themes/`、模板属性、示例主题按钮 | 已对照 WPFUI 1.2.2 同步 Classic 双工具浮动窗格轮廓；Toggle 专用模板及六区拖动范围仍按原有检查，Light/Dark 使用同一组 WinUI 主题资源。 |

**结论：**固定 Windows 普通功能清单中的主要实现路径已完成本轮代码审查，发现的上述细节缺陷已修复；未发现明确缺失的普通功能类别。代码审查和场景检查不能替代每种停靠目标的现场落位：文档与工具浮动后分别停入中心、边缘、外侧及其他浮动宿主，自动隐藏固定、浮动窗最大化/还原仍缺少本轮逐项实操证据。Uno 后端不计入当前迁移范围。

## 本次代码审查与验证

- 内容模板调整后完整解决方案 Debug 构建通过，0 警告、0 错误；Docking、ToggleDocking、Mvvm 三个示例 smoke test 均通过。Docking 实际窗口确认文档说明以文本块呈现、无输入框边框，修改项菜单路径由场景检查验证。
- 性能修复后产品、Docking 与 ToggleDocking 示例构建均通过；两个示例现有 smoke test 通过。实际窗口中将两个文档拆为左右窗格并拖动分隔条，确认窄条预览出现且释放后宽度改变；在 Toggle 示例中从左下工具标题拖过六区到右下，确认目标高亮、标题浮签和最终落位。未记录拖动帧时间基准。
- 修正了 MVVM 工具侧边导入顺序、内容引用身份判断、工具 `CanMove` 回调、浮动事件重复发送、拆分工具取消操作的状态顺序、多显示器浮动边界和运行时自动隐藏延时；删除无调用的重复屏幕定位方法。保留原有版权声明。
- 依据 [Zenith.NET](https://github.com/qian-o/Zenith.NET) 的现有源码，统一 224 个手写 C# 文件的四空格、LF、文件作用域命名空间、控制流大括号、导入顺序、字段命名、显式局部类型及可推断类型的 `new()`；按实际生命周期标注并处理可空值。手写源码中的 `#nullable disable` 已清零；仅原样保留上游自动生成的 `Resources.Designer.cs` 中 1 处。`.editorconfig` 固定风格规则，`IDE0003`、`IDE0008`、`IDE0011`、`IDE0090`、`IDE0161`、`IDE1006` 检查通过。
- 三个示例项目已统一为 `Docking`、`ToggleDocking`、`Mvvm`，顶部操作区使用 WinUI `MenuBar`：Docking 对应经典 File/Edit/Layout/Tools/Theme 工作流，ToggleDocking 对应 File/Layout/View/Theme 六区侧栏工作流，Mvvm 对应 File/Edit/Tools/Layout/Theme 模型工作流；原有场景验证入口保留在菜单中。
- 本轮验证：`dotnet build .\WinUI.AvalonDock.slnx -c Debug --no-restore /p:BuildInParallel=false` 通过，0 警告、0 错误；Docking、ToggleDocking、Mvvm 三个 `--smoke-test` 均通过。Classic 另检查短文档标签首次加载及浮动回停后的 100 像素下限、长标题的内容宽度、空白区域命中及原有溢出选择；真实鼠标从头部留白和关闭按钮旁的间隙分别拖出文档，浮动窗可拖回主文档窗格，回停后两个短标签仍各为 100 像素，关闭按钮点击没有触发浮动。`dotnet format whitespace --verify-no-changes` 通过。
- 本次审查修复后，解决方案 Debug 构建再次以 0 警告、0 错误通过，三个示例 `--smoke-test` 均创建真实窗口并通过；新增检查覆盖 Classic `CanClose` 样式、Toggle 隐藏/浮动取消与多独立窗口选择性附回、MVVM 文档标签关闭后的模型列表与下一个文档。`dotnet format whitespace --verify-no-changes` 与 `git diff --check` 通过。旧工作树改动仍保留，未提交、推送或发布。
- 对照 WPFUI 1.2.2 的源码差异，本次上游样式改动集中在 Classic `LayoutAnchorablePaneControl.xaml`：双工具浮动时保留顶层圆角描边，单工具直接浮动时隐藏描边。Docking 真实窗口与 smoke test 均确认双工具浮动外观；场景检查还验证活动描边、双工具缩回单工具后的隐藏状态以及停靠返回。ToggleDocking smoke test 和空白格式检查通过。
- `DockingManager` 示例用 `ObservableCollection` 明确提供 Add、Replace、Reset 入口；`Mvvm` 示例使用官方 MVVM 包和 `ICommand` 绑定；三个应用的 XAML、启动入口、清单、共享模型和 smoke-test 基础设施均已纳入解决方案。
- 产品直接引用官方 Core；示例按需引用官方 MVVM 与 XML 序列化包。Windows 平台服务仍隔离输入、窗口、覆盖层、焦点、窗口顺序和坐标转换；共享布局与停靠逻辑未引入 HWND 或原生消息。仓库未提交、推送或发布。
