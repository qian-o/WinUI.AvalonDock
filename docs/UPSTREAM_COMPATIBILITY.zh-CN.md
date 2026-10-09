# Dirkster.AvalonDock 行为边界与本轮验收基线

核对日期：2026-10-08。

本轮开发遵循用户指定的范围：不扩大 Dirkster.AvalonDock 的功能边界，不自行改变其停靠、关闭、隐藏、激活、导航及模型同步语义。优化以修正 WinUI 适配偏差、保证原生回调生命周期和补充验收证据为主。相同代码结构不等于相同行为；WPF 原生提供的属性继承、容器生成及模态窗口机制也属于对照范围。

## 固定基线

仓库 `Directory.Packages.props` 当前使用 `Dirkster.AvalonDock.Core`、`Mvvm`、`Serializer.Xml` **5.0.0**；移植源码注明的上游提交为 **`408dc2896e2f41f3bb79a15207f160edee8a6792`**。本次直接读取该提交的源文件核对，未仅依赖之前的审查结论。

用户提供的 [Dirkster.AvalonDock NuGet 页面](https://www.nuget.org/packages/Dirkster.AvalonDock) 在核对时显示稳定版本 5.0.1，5.0.0 仍在版本列表中。因此本轮使用仓库已经固定的 5.0.0 提交作为行为基线，不自动升级依赖，也不把上游未来版本的行为混入此次验收。

公开链接：

- [5.0.0 NuGet 页面](https://www.nuget.org/packages/Dirkster.AvalonDock/5.0.0)
- [固定上游提交](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792)
- [上游 DockingManager.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/DockingManager.cs)

## 允许调整和必须保留的边界

| 机制 | 上游证据及行为 | 本轮处理 |
|---|---|---|
| 文档和工具内容的继承属性 | `DockingManager.InternalAddLogicalChild` 使用 WPF `AddLogicalChild`，`LayoutItem.View` 是库创建的 `ContentPresenter`。继承属性的优先级低于用户本地值、绑定及样式。 | 保留必要的 WinUI presenter 桥接；避免直接给用户内容写入本地 `DataContext`、字体等。用户原有属性的自然优先级属于兼容要求。 |
| 焦点记录及原生 hook | `FocusElementManager` 使用进程级静态 manager 列表和焦点表，第一次 manager 注册时创建一个 hook。`WindowHookHandler.Attach` 将 `WH_CBT` 安装到当前线程。 | 不把本轮演变成新的多 UI 线程支持；验证同一 UI 线程多个 manager 的焦点归属和释放。保留 hook 的线程范围及 `CallNextHookEx`。 |
| Ctrl+Tab 导航的模态范围 | `DockingManager.ShowNavigatorWindow` 调用 WPF `Window.ShowDialog()`；WPF 会禁用当前 UI 线程原本可见且启用的顶层窗口。 | 保留同 UI 线程范围；不能擅自缩小成当前工作区。验证关闭或异常后只恢复本次禁用的窗口。 |
| 导航器关闭后的激活恢复 | WPF 保存 `GetActiveWindow()`，当对话框关闭时仍为活动窗口才调用 `SetActiveWindow(previousActive)`。 | WinUI 模态适配改为同线程活动窗口保存和恢复，移除该关闭路径对桌面全局前台 HWND 的保存及恢复。用户显式激活内容的既有 helper 另有职责，未改其行为。 |
| 导航器的 owner | WPF 创建导航器时设置 `Owner = Window.GetWindow(manager)`。 | WinUI 设置库导航 HWND 的 `GWLP_HWNDPARENT` 为所属 XamlRoot 的顶层窗口，恢复 owner 关系。保留原 presenter 及同线程模态循环。 |
| URI 主题替换 | 上游按 `FirstOrDefault(dictionary.Source == oldTheme.GetResourceUri())` 删除第一个同源字典。 | 恢复该规则，即使消费者也加入相同 URI 字典；不能将“只删除库拥有的字典”宣称为严格上游等价。 |
| `DictionaryTheme` | 上游追踪安装的字典实例。 | WinUI 因多 XamlRoot 的字典所有权需要使用独立字典容器；保留实例追踪和删除。复制容器不增加新的主题 API。 |
| MVVM 文档关闭 | 上游 `LayoutSyncBridge` 订阅 `DocumentClosed`，完成关闭后从桥接文档来源和文档树移除模型。 | 保留文档关闭同步与取消关闭语义。 |
| MVVM 工具关闭 | 同一上游 bridge 未订阅 `AnchorableClosed`；实际工具关闭移除 native 布局节点，但 bridge 来源及 `ToolDock.VisibleDockables` 模型保留。重连布局会再次导入模型。 | 撤回上一轮新增的工具模型同步移除，验收现有上游行为，不将重新导入模型标为已修复。隐藏与取消关闭继续保留模型。 |
| 原生 subclass 和 managed callback | WinUI 的 native host 需自己管理 HWND subclass 和 managed 引用。 | 保留回调指针位宽及卸载失败时不提前释放 `GCHandle` 的修复；这属于实现安全性，不改变停靠或用户命令语义。 |
| 鼠标主键 | WPF 交互按逻辑主键处理；`GetAsyncKeyState` 针对物理键。 | 保留读取 `SM_SWAPBUTTON` 的适配，遵循系统现有主键设置，不改变系统配置。 |
| 默认自绘浮窗 | 上游 WindowChrome 配合模板提供自绘 caption、按钮、拖动及 resize。 | 默认路径仅更新库 HWND 的 caption 样式，避免当前 SDK 标题栏配置的 GDI 增长；实测保留窗口交互。SDK 按钮回退模板保留，尚无相同资源保证。平台 API 及边缘命中宽度不声明逐项相同，见 [资源记录](RESOURCE_LIFETIME.zh-CN.md)。 |
| 文档浮窗关闭事件 | 关闭后的窗口不应由适配层事件注册持续保活；owner 激活仍在原有关闭时机执行。 | 命名 native `Closed` 处理器在关闭时自解除，保留原激活及模型语义。 |

## 关键源码对照

### 焦点和多 UI 线程

[上游 FocusElementManager.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/FocusElementManager.cs) 的静态字段见第 18—24 行，首次 manager 安装和最后 manager 解除 hook 见第 29—84 行。

[上游 WindowHookHandler.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/WindowHookHandler.cs) 的 `Attach` 传入当前线程 ID；`HookProc` 在 `HCBT_SETFOCUS` 通知后继续调用 hook 链。

这些源码没有按 dispatcher 或线程分组的焦点状态。它们也不能单独证明上游明确禁止多个 UI 线程，但不足以支持“多 UI 线程已获保证”的承诺。因此本轮不重构出新支持范围，不把同线程多工作区的验收延伸为多线程保证。

### 模态窗口

固定上游 `DockingManager.cs` 第 3878—3883 行创建带 owner 的 `NavigatorWindow` 并调用 `ShowDialog`。

其 WPF 框架行为可在 [微软 WPF Window.cs 源码](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs) 的 `ShowDialog`、`ThreadWindowsCallback`、`EnableThreadWindows` 和 `DoDialogHide` 中核对：

- 保存当前线程的活动窗口。
- 枚举当前线程的顶层窗口，只收集原本可见且启用的窗口。
- 禁用收集的窗口；退出对话框时重新启用仍存在的窗口。
- 仅当对话框关闭时为活动窗口，且先前活动 HWND 仍存在，才恢复该活动窗口。

因此“打开导航器会暂时禁用同线程其他主窗口”是 WPF 模态机制本身的行为，不应在此次机制优化中改成工作区局部模态。与此同时，WinUI 曾用 `GetForegroundWindow()` 保存其他应用的前台窗口，这与线程局部活动窗口不是同一语义，改回线程局部恢复属于适配修正。

微软对应 API 文档：[GetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getactivewindow)、[SetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setactivewindow)。

WinUI 的 owner 关系同样需要恢复。微软 [Manage app windows](https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows) 的模态窗口部分说明，WinUI owner 需要 Win32 interop；其链接的 [WinUI Gallery ModalWindow 示例](https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/SampleSupport/SamplePages/ModalWindow.xaml.cs) 使用 `SetWindowLongPtr` 与 `GWLP_HWNDPARENT` 设置 owner。该 API 对顶层窗口设置 owner，不是将内容改成原生 child。[SetWindowLongPtrW 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw)

本轮只设置库模态窗口的 owner，不使用 `SetParent`，不对宿主窗口写入 parent，也不改 `OverlappedPresenter.IsModal`。后者仅描述对 owner 的模态阻挡，不能替代上游 `ShowDialog` 的同线程窗口枚举。API 的 HWND 修改只作用于库创建的导航窗口。

### 用户内容的属性优先级

固定上游 `DockingManager.cs` 第 2052—2063 行调用 `AddLogicalChild`；第 3855 和 3875 行将内容纳入逻辑树；[LayoutItem.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/LayoutItem.cs) 第 59—70 行创建并绑定 `View`。

WPF 的继承值不等于 `SetValue` 写入本地值。本地绑定也有本地值优先级，样式优先于继承；对已有绑定调用 `SetValue` 或 `ClearValue` 可能移除绑定。微软文档：[依赖属性值优先级](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/properties/dependency-property-value-precedence)。

因此上一轮保护用户内容的本地值、绑定及样式，以及仅在库 presenter 上桥接继承属性，属于恢复 WPF 的自然优先级。它不是“任何情况下都不改变 DataContext”：没有用户本地值或样式时，内容仍应获得上游预期的 manager 继承上下文；DataTemplate 内部上下文则仍应跟随所呈现的数据内容。

模板退出时也只应释放模板实际持有的库 View。用户当前模板上的活动绑定不应被重新赋值意外清除；退休模板释放已转移的 native 父子关系是 WinUI 的生命周期适配职责。

### 菜单机制

[上游 ContextMenuEx.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/ContextMenuEx.cs) 继承 WPF `ContextMenu`，容器生成由 WPF 处理。

[上游 DropDownButton.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/DropDownButton.cs) 的 `OnClick` 明确设置菜单根节点的 `DataContext = DropDownContextMenuDataContext`。菜单子项随后自然继承，子项自己的本地上下文、绑定和样式具有更高优先级。WinUI `MenuFlyout` 无相同根节点继承机制，需要适配层给没有用户上下文的子项提供上下文。

WPF [ItemsControl.cs 的 ApplyItemContainerStyle](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ItemsControl.cs) 会保留用户本地 `Style`；对于未指定本地样式的容器，非空 `ItemContainerStyle` 仍会应用；清空容器样式时只清除生成器之前安装的样式。验收必须同时覆盖这三种情况，不能将“直接提供的菜单项永远忽略容器样式”当成上游规则。

[上游 Generic.xaml](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Themes/generic.xaml) 的默认菜单通过本地绑定和 converter 定义可见性。这些绑定的优先级高于同一个菜单项的样式 setter。WinUI 带 `AvalonDock.Menu.*` 标签的默认项使用对应的本地策略，保留消费者显式本地值和绑定的同时，策略值仍高于 `Style.Visibility` setter。消费者自建且未加上述策略标签的菜单项不参与该默认策略，继续使用自己的本地值、绑定及样式。菜单上下文是继承适配，不能将其优先级与默认项的本地可见性策略混为一谈。

### 主题方法的精确差别

| 上游位置 | 旧主题处理 | 新主题处理 |
|---|---|---|
| `DockingManager.OnThemeChanged`，第 1536—1584 行 | 旧主题为 `DictionaryTheme` 时删除追踪实例；其他主题删除首个 URI 匹配实例。 | 字典主题安装并追踪实例；URI 主题创建字典并追加。 |
| [LayoutFloatingWindowControl.UpdateThemeResources](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/LayoutFloatingWindowControl.cs)，第 363—398 行 | 与 manager 相同，仅当 `oldTheme != null` 执行删除。 | 从当前 manager 获取主题。 |
| [DetachedAnchorableWindow.UpdateThemeResources](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/DetachedAnchorableWindow.cs)，第 148—177 行 | `oldTheme != null` 时优先删除已追踪实例；没有追踪实例才按旧主题 URI 删除首个匹配项。 | 从显式 `newTheme` 参数安装主题，只追踪字典主题。 |
| [NavigatorWindow.UpdateThemeResources](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/NavigatorWindow.cs)，第 452—481 行 | 与 manager 相同，仅当 `oldTheme != null` 删除。 | 从当前 manager 获取主题并追加。manager 的调用不传旧主题，因此不会在这一调用中删除此前字典。 |

导航器没有参数的资源更新会追加，这是实际代码行为。本轮保留该行为，不新增资源去重策略。WinUI floating 的模板更新可能重复请求资源同步；现有相同主题引用短路用于避免模板布局重复安装，没有扩大主题功能。

### MVVM 工具关闭

[上游 LayoutSyncBridge.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/LayoutSyncBridge.cs) 第 155—165 行只订阅及解除 `ActiveContentChanged` 和 `DocumentClosed`；第 288—306 行只处理文档关闭同步。虽然下方递归函数包含工具分支，固定提交没有从工具关闭事件调用该分支。

上一轮新增的 `AnchorableClosed` 同步确实修复了模型保留问题，但也改变了固定上游的可观察结果。依据本轮约束已撤回此新增分支。需要永久移除工具模型的消费者应操作自己持有的模型集合；库不在此次优化中额外增加默认同步语义。

`sources/Experiments/Shared/LayoutSyncSmokeTests.cs` 现在验证：取消关闭不删除、隐藏不删除、实际关闭移除 native 节点且保留模型及来源、仅重建模型树时来源保留、布局重新连接后再次导入。模型树替换不保证立即重建已经关闭的 native 节点，因为 bridge 来源集合可能没有变化。它是兼容性验收，不是工具关闭缺陷已经修复的证明。

## 验收与成熟度的表述

本轮新增同 UI 线程工作区生命周期、模态窗口和实际 NuGet 消费宿主检查。构建及执行结果应以本轮产物和日志为准，之前的“60 项全部通过”不能替代本轮调整后的验收。

最终解决方案 Debug、Release 构建均为 **0 警告、0 错误**；Docking、ToggleDocking、Mvvm 在两个配置下各完成 **130 项检查**。运行环境为 Windows 11 专业版 build 26200、.NET SDK 10.0.401、Windows App SDK 2.5.1，同 UI 线程；具体资源样本及实际交互见 [资源生命周期记录](RESOURCE_LIFETIME.zh-CN.md)。仓库根目录 `.\verification\Run-BoundaryChecks.ps1` 可串行重现六套检查。独立 NuGet 宿主在两个配置下分别通过 **27 项公开 API 集成检查与 130 项边界检查**，通过候选包及程序集 hash、实际来源和 `project.assets.json` 类型证明消费的是本地包，详见 [消费项目](../verification/PackageConsumer/README.md)。

这些检查可以支持在已覆盖的 Windows/WinUI、单 UI 线程和明确输入路径下的可用性结论；不能保证所有未知宿主控件、多个 UI 线程或长期高负载环境。此次基线核对也不是法律合规意见，不从“公开系统 API”推导所有调用方式均无害。

今后的变更应先写出固定上游对应的命令、事件、状态及属性优先级，再写出 WinUI 上同一结果的验收。若修复必须改变上游可观察行为，应明确记录为上游差异；在本次授权范围内不直接引入该行为改变。

2026-10-08 的覆盖窗口后续修复仍保留取消关闭、显示隐藏和焦点规则，仅在实际关闭后解除库自己的事件及 callback，并清空仍属于库私有 view 的窗口内容，不操作借入的目标控件。独立源码和实际包后验、新候选身份与剩余资源限制见 [资源后续调查](RESOURCE_FOLLOWUP.zh-CN.md)。
