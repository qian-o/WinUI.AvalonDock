# 系统 API 与用户内容边界审查

审查日期：2026-10-08。基线提交：`476ff22506e20413ca3eb6a240db911ecf4f6812`，以及本次未提交的修复。

本次审查关注包在运行时对系统、宿主应用和用户控件的影响。这里的“侵入”指越过必要的窗口及输入边界、覆盖应用持有的状态，并不等同于版权法律判断。微软公开支持的 API 仍可能具有副作用；判断依据同时包括 API 文档、调用参数、对象所有权和释放路径。

## 系统调用的范围

| 机制 | 用途和影响范围 | 边界或限制 |
| --- | --- | --- |
| `WH_CBT` / `SetWindowsHookExW` | 观察安装线程的原生焦点变化，支持嵌入 HWND 的焦点恢复 | 明确传入 `GetCurrentThreadId()`，没有使用全局低级键鼠钩子；回调继续 `CallNextHookEx`，不阻止焦点改变。manager 卸载和释放时解除订阅。 |
| WinUI 键鼠事件、`GetAsyncKeyState`、`GetCursorPos` | 拖动、取消、导航与坐标读取 | 不合成用户输入。鼠标状态读取尊重 `SM_SWAPBUTTON`；高频拖动计时器只在拖动会话中运行，结束或取消时停止。 |
| `SetCapture` / `ReleaseCapture` | 拖动过程中跟踪本线程窗口之外的指针位置 | 捕获是暂时的；结束、取消、丢失捕获和 HWND 销毁时清理。捕获期间快捷键可能受 Windows 捕获机制影响。 |
| `SetWindowSubclass` / `RemoveWindowSubclass` | 本应用窗口消息、关闭和拖动处理 | 回调状态在成功卸载或窗口销毁之前保持有效；卸载失败后重试，不提前释放托管引用。 |
| `SetWindowPos`、窗口样式、消息、菜单和图标 | 管理库创建的浮动、独立及覆盖窗口，或所属 XamlRoot 的窗口 | 常规路径不主动寻找并修改其他进程窗口。公开原生子窗口扩展点另有所有权约定，见下文。 |
| `WindowFromPoint`、窗口矩形、Z order、DWM 属性读取 | 判断桌面其他窗口是否遮挡停靠目标 | 可读取其他应用的窗口信息，不改变这些窗口或读取其文档内容。 |
| GDI 位图、DC、图标、layered window | 绘制停靠覆盖层与窗口图标 | 作用于库窗口；释放位图、图标及 DC。`GetDC(0)` 用于建立 compatible DC，不是桌面截图路径。 |
| `EnumThreadWindows` / `EnableWindow` | 模态导航窗口期间暂时禁用同 UI 线程已启用、可见的顶层窗口 | 当前为线程范围的应用模态语义，包含同线程其他主窗口；关闭时恢复。本轮保留该语义。 |
| `SetForegroundWindow` / `SetFocus`；模态的 `GetActiveWindow` / `SetActiveWindow` | 用户显式激活内容后恢复焦点；模态关闭时恢复当前 UI 线程的先前活动窗口 | 显式内容激活受 Windows 前台切换限制；模态恢复使用线程局部活动 HWND，仅当关闭的模态窗口仍为活动窗口时恢复，不记录其他应用的前台 HWND。 |

在 `sources/WinUI.AvalonDock` 源码中，没有发现注册表或系统配置写入、网络发送、外部进程启动、跨进程内存写入、全局热键注册、`SendInput`、`keybd_event`、`mouse_event` 或 `SetCursorPos` 路径。此结论限于审查的源码；不是对所有传递依赖、消费者扩展、外部布局序列化包或所有部署环境的安全认证。

公开 `Compatibility.HwndHost` 的 `BuildWindowCore` 扩展允许应用提供子 HWND。适配器会布局、显示并在释放时清理它，因此返回的窗口应由宿主拥有，父 HWND 应为传入的 parent。目前检查窗口有效性、子窗口样式和父窗口，不验证 PID/thread。库不会自行把外进程窗口嵌入；消费者主动嵌入外进程窗口时须自行管理其生命周期。Windows 不允许调用线程直接销毁其他线程创建的窗口。

## 本次修复

1. 用户传入的文档和工具内容根节点不再安装 `InheritedPresentation`。`DataContext`、字体、语言、前景色等继承桥接只安装在库拥有的 presenter 上；用户内容保持自然继承、本地值、绑定及样式的优先级。独立窗口同样桥接到库 presenter，并跟随 manager 的属性变化。
2. 继承值刷新和清理显式保护绑定表达式，避免将绑定的当前值误认成库自己的镜像值。
3. 当前自定义模板的内容绑定保持有效；退休模板只释放其承载的库 View，消费者后续替换的内容不被清空。退休模板为移交 WinUI 唯一 native parent 所做的清理，不修改文档内部的绑定。
4. 初轮曾将主题移除改为按安装实例清理；在随后要求严格保持 Dirkster 上游行为的验收中，已恢复 URI 主题按首个相同 URI 字典移除的规则。`DictionaryTheme` 的独立字典容器复制仍是 WinUI 单一 owner 所需的适配。消费者应避免在同一个目标资源集合中重复安装相同 URI 主题。
5. 菜单容器保留消费者本地 `Style`、显式 null 及绑定；未设置本地样式的直接菜单项和生成项仍应用 `ItemContainerStyle`，清理只释放生成器仍持有的样式。菜单上下文尊重本地值、绑定及 Style setter。默认菜单可见性策略模拟上游本地绑定，优先于 Style setter，并保护用户后续显式本地值和绑定；未标记的自建菜单不参与默认策略。临时菜单 presenter 样式恢复原绑定或未设置状态。
6. owner 消息回调卸载失败时保留 `GCHandle` 并重试，窗口销毁时最终清理；回调标识避免已关闭 HWND 的复用覆盖待释放的回调。
7. 拖动状态检测读取 Windows 已有的鼠标按钮映射，以对应逻辑主键的物理按钮进行检查，不改动用户设置。
8. 初轮曾加入 MVVM 工具关闭后自动移除 `ToolDock.VisibleDockables` 模型的同步；后续对照确认钉住的上游只同步文档关闭。在严格保持上游行为的约束下，已撤回工具关闭自动同步。工具关闭退出 native 布局，但模型保留、重建或重新连接布局可再次导入，应用按上游约定管理工具模型集合。
9. 模态导航窗口设置所属顶层 HWND 为 native owner，并按上游线程局部活动窗口语义恢复焦点。保留同 UI 线程模态范围、窗口 presenter 配置和嵌套消息循环。
10. 文档浮动窗口的 native `Closed` 事件使用命名处理器，在原有关闭处理时解除订阅，然后执行原有 owner 激活逻辑，终止已关闭窗口的托管保留链。
11. 默认自绘 caption 保留标题、按钮、拖动和 resize 行为，改为只更新库创建 HWND 的 `WS_CAPTION` 位，保留其他已有样式并刷新 frame，避免当前运行时 SDK 标题栏配置路径的持续 GDI 增长。消费者模板需要 SDK 系统按钮时保留回退路径；具体隔离实验、实际计数和限制见 [资源生命周期记录](RESOURCE_LIFETIME.zh-CN.md)。

## 结构与性能

当前主要职责已由布局模型、manager 操作、内容 presenter、菜单及主题辅助类、平台接口和 Windows 实现分隔。本次收敛的规则是：系统副作用留在平台适配层，消费者内容不承担框架桥接状态，临时写入只释放自己仍持有的值，资源卸载按实际对象实例追踪。

自动隐藏定时器在显示自动隐藏内容后运行，隐藏或 Dispose 时停止；拖动定时器只在活跃拖动期间运行。没有发现导入包就持续运行的后台轮询线程。正常 `Loaded`、`Unloaded` 和 Dispose 路径会成对注册及解除焦点、窗口和内容订阅。

本轮增加 102 次文档创建、浮动、回停、菜单打开关闭及关闭循环，采样句柄、GDI/USER、托管及私有内存和弱引用。它用于观察短周期释放趋势，不是 CPU 基准或长时间性能保证。大量文档及复杂模板、主题复制成本和不同 DPI 环境仍需覆盖。

## 仍需专门验证的机制

- **多 UI 线程焦点管理**：`FocusElementManager` 的 manager 列表及 native handler 为进程级静态状态，而 `WH_CBT` 和原生子窗口注册是线程范围。这与钉住上游实现一致；保持单 UI 线程支持边界，不在本轮新增多 UI 线程支持。
- **模态窗口范围**：导航器暂时禁用同 UI 线程的其他可见且已启用的主窗口。这与上游使用 WPF `ShowDialog` 的行为一致，保留该范围并验证关闭后的启用状态恢复，不收窄为 workspace 模态。
- **未加载文档的焦点**：固定上游在 `ActiveContent` 改变时同步尝试恢复记忆焦点，没有启用延后恢复逻辑。WinUI 虚拟化文档直接设置 `IsActive` 时，用户编辑器可能尚未加载；本轮保留该行为边界。可视、已加载编辑器的恢复与未加载路径分别验证和记录。
- **原生子窗口扩展点**：是否强制同进程、同线程句柄需要与已有 HWND 嵌入契约一起设计，避免直接限制有效集成场景。
- **依赖及长时间压力**：传递依赖和所有消费者扩展未在本次逐行审查；长时间性能及不同 DPI、鼠标映射、多线程环境须另行验证。

## 可重复的验证

解决方案 Debug 和 Release 构建后，三个示例均支持显式启用的 `--boundary-smoke <输出日志绝对路径>`。检查在真实 WinUI dispatcher、XamlRoot 和浮动/独立窗口上执行；成功后写入每项 PASS 并退出，失败写入异常并返回非零退出状态。正常启动示例时不执行该测试。

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug --no-restore
dotnet build .\WinUI.AvalonDock.slnx -c Release --no-restore
& .\sources\Experiments\Docking\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Docking.exe --boundary-smoke C:\Temp\Docking-boundary.log
```

`C:\Temp` 应预先存在；ToggleDocking 与 Mvvm 使用对应输出目录。测试源码在 `sources/Experiments/Shared/BoundarySmokeTests.cs`，仅编译进示例应用，不进入 NuGet 库。日志记录文档关闭取消及源同步、用户属性和绑定保留、浮动回停、独立窗字体继承、主题字典所有权、菜单真实打开关闭后的绑定活性，以及模板模型上下文。

原生回调验证使用真实隐藏 Win32 HWND，包含移除失败后重试、销毁清理及重复卸载周期。鼠标交换设置只做文档和分支语义验证，没有改变电脑的鼠标配置；已有交换配置下的实际操作属于待补环境覆盖。

最终源码的验收环境为 Windows 11 专业版 `10.0.26200`（build 26200）、.NET SDK `10.0.401`、Windows App SDK `2.5.1`，同一 UI 线程。解决方案 Debug、Release 最终构建均为 0 警告、0 错误；Docking、ToggleDocking、Mvvm 在两配置下各完成 **130 项检查**。最终日志使用 `artifacts/upstream-acceptance/*-final.log`，资源样本和真实窗口交互记录见 [资源生命周期记录](RESOURCE_LIFETIME.zh-CN.md)。所有样例每轮包含 102 次资源生命周期循环；测试计数包括功能与资源断言，不能把计数本身视为安全认证。

可以通过仓库根目录的 `.\verification\Run-BoundaryChecks.ps1` 重复运行全部样例；完成构建后可加 `-NoBuild`。这些检查会创建实际窗口、改变焦点，并测试同线程模态范围，应串行运行；与独立包消费脚本分开执行。

2026-10-08 的后续验收另外增加独立覆盖窗口生命周期回归，默认由上述 runner 在两配置下串行运行，不改变原有 130 项计数。覆盖窗口关闭后的订阅与私有视图清理、312 次资源循环、SDK 组合基线、独立托管堆分析以及新候选包身份见 [资源后续调查](RESOURCE_FOLLOWUP.zh-CN.md)；此前资源记录与旧候选包身份保留为历史证据。

最终本地 NuGet 候选还由独立宿主在 Debug、Release 下分别完成 **27 项公开 API 集成检查与 130 项边界检查**。该宿主使用 `PackageReference`、独立包缓存及本地源映射，不引用产品项目；包及 DLL 的缓存/输出 hash 一致，`project.assets.json` 的产品类型为 package，项目依赖为 0。公共集成检查覆盖实际包模板和资源、绑定持续更新、来源集合替换、上游 XML 保存恢复与 ContentId 回连，以及固定上游的 MVVM 行为。完整命令及身份、资源证据见 [独立 NuGet 消费宿主](../verification/PackageConsumer/README.md)。

初轮结果：Debug 和 Release 全解决方案构建均为 0 警告、0 错误，三个示例在两个配置下各完成当时的 60 项检查。此结果记录于 `artifacts/boundary-audit`，部分断言属于随后撤回的行为改善，不能作为当前严格上游一致性的验收结果。当前基线及后续验收见 [上游兼容性约束](UPSTREAM_COMPATIBILITY.zh-CN.md)。原生 HWND 回归还覆盖待卸载 callback 与新 host 的独立标识、100 次正常清理。Release 本地生成的 NuGet 包包含 LICENSE、README、PRI、Generic.xaml 和语言资源；没有发布操作。

## 微软文档依据

- [SetWindowsHookExW：线程及全局 hook 的作用范围](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw)
- [GetAsyncKeyState：物理鼠标按钮与 SM_SWAPBUTTON](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate)
- [GetSystemMetrics：只读系统指标](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics)
- [SetCapture：鼠标捕获与限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcapture)
- [RemoveWindowSubclass：返回值及线程要求](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-removewindowsubclass)
- [EnumThreadWindows：枚举线程顶层窗口](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumthreadwindows)
- [SetForegroundWindow：前台切换限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
- [DestroyWindow：窗口所有线程限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-destroywindow)
