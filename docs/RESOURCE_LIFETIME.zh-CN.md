# 资源生命周期实测与诊断记录

记录日期：2026-10-08。本文记录本轮资源诊断、生产实现、三个示例和最终 NuGet 消费宿主的 Debug/Release 复验，以及真实窗口交互结果。

后续记录说明：本文表格和 `DF6DE310...` 包身份保留为覆盖窗口修复前的历史数值记录；最新结果见 [资源后续调查](RESOURCE_FOLLOWUP.zh-CN.md)。验收 runner 使用固定文件名，本次后续运行已替换本文链接的示例 `*-final.log` 和 PackageConsumer 同名检查/身份日志，因此这些链接现在对应新候选，不能当作旧表数值或旧包身份的原始日志核对。旧构建日志、标题栏诊断日志和本文历史表格未被重写。

本轮实际窗口验收发现两类独立问题：关闭窗口的事件订阅曾保留浮动控件等托管对象；解除该订阅后，受观测的托管对象可以释放，但原生 GDI 对象仍随浮动窗口循环持续增长。原生标题栏配置路径可以在不创建 `DockingManager` 的 WinUI 宿主中复现增长。拆分后，单独隐藏 SDK presenter 标题栏、单独启用 SDK 内容延伸标题栏，均能触发 GDI 增长；只读取高度没有同样的增长。默认自绘按钮路径改为更新库 HWND 的 caption 样式后，六份最终示例日志均完成 **130 项检查**，第 12、42、72、102 次的 GDI 均为 **20**，USER 从预热的 **70—72** 收敛为后三次的 **73**，所测托管弱引用及退出宿主资源均为 **0**。原生句柄和内存仍有增长，SDK 按钮回退路径也未获得同等资源保证；本轮结论不能延伸为长期或所有模板的稳定性保证，也不能宣称微软已经确认 SDK 缺陷。

## 验收范围和采样方法

功能与行为范围遵循 [上游兼容基线](UPSTREAM_COMPATIBILITY.zh-CN.md)。本轮不借资源优化引入新的停靠、激活、关闭或输入策略。基础诊断宿主使用仓库相同的 `Microsoft.WindowsAppSDK` **2.5.1** 和 `net10.0-windows10.0.19041.0`；参见 [PackageConsumer 项目](../verification/PackageConsumer/PackageConsumer.csproj) 与 [集中包版本](../Directory.Packages.props)。

最终复验环境为 **Windows 11 专业版，10.0.26200，build 26200；.NET SDK 10.0.401；Windows App SDK 2.5.1；win-x64**。Debug、Release 全部示例构建分别完成，均为 **0 个警告、0 个错误**。[Debug 构建日志](../artifacts/upstream-acceptance/build-Debug-final.log)、[Release 构建及打包日志](../artifacts/upstream-acceptance/build-Release-final.log)

停靠生命周期检查在同一 UI 线程执行 **102 次**“创建文档 → 浮动 → 停靠 → 打开及关闭菜单 → 关闭文档”，先执行 12 次预热，然后每 30 次采样，共记录第 12、42、72、102 次后的状态。采样前等待退出和卸载，并多轮执行 GC、终结器等待及 dispatcher 延时，避免只观察刚调用 `Close()` 的瞬时状态。具体流程见 [WorkspaceLifecycleSmokeTests.cs](../sources/Experiments/Shared/WorkspaceLifecycleSmokeTests.cs)。

采样覆盖以下相互独立的指标：

| 指标 | 采样内容 | 能说明的范围 |
|---|---|---|
| 托管弱引用 | 文档、用户内容、LayoutItem、presenter、浮动控件和菜单 | 所观测的对象是否仍可从托管根访问；全部为 0 不能证明原生或 COM 资源全部释放。 |
| 退出后的浮动宿主 | HWND 是否存在、owner hook、subclass、宿主 GCHandle 状态 | 库持有的这些原生宿主及回调资源是否残留；不能覆盖 SDK 内部所有资源。 |
| GDI 和 USER | `GetGuiResources(process, 0/1)` 的进程当前计数 | 当前 GDI、USER 对象使用量，不是峰值，也不是所有进程句柄的总数。 |
| 进程句柄 | `GetProcessHandleCount` | 进程原生句柄的总体变化，需要结合无停靠宿主基线判断。 |
| 内存 | 私有字节、`GC.GetTotalMemory(false)` | 同一环境中的趋势观察；缓存、终结器时序和运行时分配会影响数值。 |

`GetGuiResources` 的参数 0、1 分别对应 GDI 和 USER 当前对象计数，微软 API 文档可直接核对：[GetGuiResources](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguiresources)。

## 托管对象释放

文档浮动窗口原先以匿名委托订阅 WinUI `Window.Closed`，该委托捕获窗口实例。改为命名处理器后，在关闭处理器内解除同一订阅，再执行原有的 owner 激活逻辑。修改没有新增关闭行为，目的是终止已经关闭的窗口所持有的事件注册。源码见 [LayoutDocumentFloatingWindowControl.cs](../sources/WinUI.AvalonDock/Controls/LayoutDocumentFloatingWindowControl.cs) 的 `OnNativeDocumentWindowClosed`。

修正后，第 12、42、72、102 次采样中，文档、内容、LayoutItem、presenter、浮动控件和菜单的存活弱引用均为 **0**；观测到的退休浮动 HWND、owner hook、subclass、GCHandle 也均为 **0**。[模板释放诊断日志](../artifacts/upstream-acceptance/Docking-template-release-diagnostic.log)

这证明所测托管保留链及库宿主回调资源在该场景中已解除。相同日志仍显示 GDI 从 **572** 增至 **3670**，因此托管对象释放修正不能替代原生资源诊断；继续清理模板也不能单独解释或解决这一 GDI 增长。

## 停靠窗口的原生资源隔离

本轮最早的现场采样在第 12 至第 102 次循环之间记录到 GDI **594 → 3740**、进程句柄 **2559 → 11775**。该次 `Docking-Debug.log` 已被后续重跑覆盖，此处保留为早期现场观察，不把当前同名日志冒充这一原始数据的归档证据。下面各项有单独保留的日志，可以重新核对。

| 诊断变化 | 第 12 次 GDI | 第 42 次 GDI | 第 72 次 GDI | 第 102 次 GDI | 已观测结果 |
|---|---:|---:|---:|---:|---|
| 解除关闭事件、补充退出模板释放 | 572 | 1608 | 2640 | 3670 | 托管弱引用全部为 0，GDI 仍持续增长。[日志](../artifacts/upstream-acceptance/Docking-template-release-diagnostic.log) |
| 原生标题栏颜色先比较实际值，相同值不重复写入 | 522 | 1430 | 2328 | 3256 | 较前一诊断降低增长，但未消除。[日志](../artifacts/upstream-acceptance/Docking-caption-values-diagnostic.log) |
| 临时跳过整个 `SetCaptionLayout` | 20 | 20 | 20 | 20 | GDI 保持稳定，USER 恒为 47；进程句柄仍为 987 → 1528。[日志](../artifacts/upstream-acceptance/Docking-caption-disabled-diagnostic.log) |
| 仅临时跳过原生标题栏颜色设置，其余配置保留 | 578 | 1676 | 2806 | 3976 | GDI 增长仍存在，不能把颜色 setter 当作唯一原因。[日志](../artifacts/upstream-acceptance/Docking-caption-colors-disabled-diagnostic.log) |

以上属于诊断运行，不是四种可选产品行为。尤其“跳过整个标题栏配置”会影响现有窗口呈现和交互，不能直接作为最终修复。最终实现仍须保持浮动窗口模板、标题、边框、按钮、拖动区域和停靠行为，并在保留这些行为的前提下复验资源计数。

## 不使用 AvalonDock 布局的运行时对照

[RuntimeLifetimeSmokeTests.cs](../verification/PackageConsumer/RuntimeLifetimeSmokeTests.cs) 创建真实 WinUI 窗口并等待内容加载、关闭、卸载，再采样资源。以下四个运行没有构造 `DockingManager` 或停靠布局：

| 对照场景 | 第 12、42、72、102 次 GDI | 第 12 至第 102 次进程句柄 | 第 102 次存活弱引用 | 原始日志 |
|---|---|---|---|---|
| 普通 `Window` 和 `TextBox` 激活、关闭 | 16、16、16、16 | 778 → 961 | 窗口及编辑器均 0 | [Runtime-lifetime.log](../artifacts/package-consumer/Runtime-lifetime.log) |
| `DesktopWindowXamlSource` 和 `TextBox` 初始化、释放 | 16、16、16、16 | 802 → 987 | 宿主及内容均 0 | [XamlSource-lifetime.log](../artifacts/package-consumer/XamlSource-lifetime.log) |
| `MenuFlyout` 打开、关闭 | 9、9、9、9 | 912 → 962 | 菜单及内容均 0 | [Menu-lifetime.log](../artifacts/package-consumer/Menu-lifetime.log) |
| 普通 `Window` 配置自定义原生标题栏、关闭 | 882、3024、5176、7324 | 3882 → 26951 | 窗口及内容均 0 | [Caption-lifetime.log](../artifacts/package-consumer/Caption-lifetime.log) |

最后一项依次设置 `ExtendsContentIntoTitleBar = true`、`OverlappedPresenter.SetBorderAndTitleBar(true, false)` 和 caption/passthrough 非客户区矩形，并把八个标题栏按钮颜色属性设置为 `null`，重复八轮，即每窗口 **64 次颜色 setter**。这是一组组合触发条件；该测试没有逐一证明每个调用都会单独泄漏。当前库的具体调用频率也不能直接等同于这个放大的复现宿主。

该对照已证明：原生 GDI 增长不要求存在 AvalonDock 的文档树、浮动模型、菜单或子窗口宿主。与此同时，前三项的进程句柄仍有小幅增长，说明“GDI 稳定”和“所有原生资源零增长”是不同结论；本轮不能对所有环境或长期运行作零增长保证。

## SDK 标题栏 API 的独立拆分

后续 D、E、F 对照各运行 **20 个窗口**，先预热 5 次，随后每 5 次采样。三个场景都不写颜色，不配置非客户区矩形，也不创建 AvalonDock 布局；D、F 不获取 `AppWindow.TitleBar`。

| 模式与唯一新增配置 | 第 5、10、15、20 次 GDI | 第 5 至第 20 次 GDI 趋势 | 原始日志 |
|---|---|---|---|
| D：`OverlappedPresenter.SetBorderAndTitleBar(true, false)` | 127、230、337、442 | 增加 315，平均约 **21/窗口** | [Caption-D-lifetime.log](../artifacts/package-consumer/Caption-D-lifetime.log) |
| E：获取 `AppWindow.TitleBar` 并读取 `Height` | 16、16、16、16 | 保持 16 | [Caption-E-lifetime.log](../artifacts/package-consumer/Caption-E-lifetime.log) |
| F：`Window.ExtendsContentIntoTitleBar = true` | 221、426、641、848 | 增加 627，平均约 **42/窗口** | [Caption-F-lifetime.log](../artifacts/package-consumer/Caption-F-lifetime.log) |

这使“必须两套机制叠加才出现增长”的假设不再成立：D、F 的各自调用已能独立触发，而简单获取高度未复现。证据仍限于当前运行时和受控场景，不等于所有 SDK 版本的普遍结论。三个日志中窗口弱引用在每次采样均为 0；内容弱引用存在采样差异，D 最后为 5/20，E、F 最后为 0/20，不能把 D 描述为所有托管引用均为 0。

## 目前的定位结论

已有实测支持：

- 关闭事件解除修正后，所测托管对象不再按循环累积。
- 仅释放模板、仅避免相同颜色写入、或仅跳过颜色设置，都不能单独解决该原生 GDI 增长。
- 库标题栏配置路径是当前停靠循环中已定位的触发条件；普通 WinUI 窗口配置相近的标题栏机制也能复现。
- D、F 独立对照分别将触发条件收敛到 presenter 标题栏隐藏调用及内容延伸标题栏调用；读取高度没有相同增长。
- 基础窗口、XamlSource、菜单没有复现同样的 GDI 增长，但仍需保留进程句柄及内存趋势观察。

本轮采用的窄范围方案是在保留库现有 client 自绘 caption、原生拖动区域及缩放机制的情况下，避免默认路径初始化上述 SDK 标题栏机制；只在库创建的顶层 HWND 上通过微软公开 Win32 API 更新非客户区样式。具体 SDK 内部字体句柄或函数是否泄漏，仍需要原生分配堆栈等证据，不能从计数趋势直接确定。

微软的 [Title bar customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar) 说明 WinUI `Window` 和 `AppWindow` 共享标题栏对象，并将 SDK 的内容延伸方案作为一种自定义标题栏机制。它说明这些 API 的用途和正常行为，未承诺任何调用组合在本轮 SDK 版本中的资源释放结果。

公开讨论 [WindowsAppSDK #6697](https://github.com/microsoft/WindowsAppSDK/discussions/6697) 报告了一个可能涉及 `CTitleBarResourceProvider::GetAppNameFontForScale` 的字体对象泄漏堆栈。到本次查阅时，回复仅询问发生场景，尚无根因、修复提交或微软确认结论。该讨论只作为排查线索，不能替代本仓库的复现、API 拆分实验或正式缺陷确认。

## 自绘窗口边框的上游范围与 Win32 方案

固定上游 [generic.xaml](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Themes/generic.xaml) 为文档及工具浮窗安装 `Microsoft.Windows.Shell.WindowChrome`，使用自绘标题和按钮，并设置 caption 高度、resize border、glass 等参数。因此维持这些自绘窗口行为属于既有功能范围。

该提交自带的 [WindowChromeWorker.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/Shell/WindowChromeWorker.cs) **没有直接删除 `WS_CAPTION`**：它接管 `WM_NCCALCSIZE` 扩展客户区，通过 `WM_NCACTIVATE` 及标题文本/图标消息处理抑制系统标题栏绘制；其样式 helper 使用 `GetWindowLongPtr`、`SetWindowLongPtr`，并以 `SetWindowPos` 刷新 frame。不能声称“WinUI 删除 `WS_CAPTION` 与上游逐 API 相同”；可评估的是不同平台实现能否保持相同的自绘窗口结果。

最终 WinUI 适配仅针对库自己的顶层窗口：读取原 style，保存原 caption 位，清除 `WS_CAPTION`，保留其余已有位，包括 `WS_THICKFRAME`、系统菜单及最大化/最小化位，随后刷新非客户区。模板切回系统按钮路径时恢复保存的 caption 位，不用固定整组 style 覆盖当前样式。现有模板、caption 按钮、输入区域、拖动捕获和缩放处理继续履行原职责，不新增系统 hook 或用户电脑设置修改。源码见 [WindowsWindowHostService.Caption.cs](../sources/WinUI.AvalonDock/Platforms/Windows/WindowsWindowHostService.Caption.cs) 的 `SetClientCaption`。

微软 [SetWindowLongPtrW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw) 明确允许以 `GWL_STYLE` 更新窗口样式，并要求缓存 frame 数据通过 `SetWindowPos` 生效。[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) 的 `SWP_FRAMECHANGED` 会重新计算非客户区；`NOSIZE`、`NOMOVE`、`NOZORDER`、`NOOWNERZORDER`、`NOACTIVATE` 可保留大小、位置、层级及激活状态。这组保护 flag 与固定上游 worker 的 frame 刷新规则一致。实现还须按 API 文档检查错误，而不能把返回的旧值为 0 一律当作失败。

默认模板自带 client caption 按钮时，无需获取 SDK `AppWindow.TitleBar` 或调用 `SetBorderAndTitleBar`、启用 `ExtendsContentIntoTitleBar`。当自定义模板没有 client 按钮而依赖 SDK 系统按钮时，仍保留 SDK 回退路径；不能为了资源计数擅自删除消费者模板已有的按钮机制。D、F 已经在该 SDK 版本复现相关 API 的 GDI 增长，因此这类回退模板的长期资源稳定性尚未保证。

## 最终方案的首次实测

独立 WinUI 宿主的 G 模式只清除自身 HWND 的 caption 位、刷新 frame 并配置拖动/交互区域，不调用 SDK 标题栏配置 API。第 5、10、15、20 次后的 GDI 均为 **21**，USER 均为 **38**，窗口及内容弱引用均为 **0**，进程句柄为 **771 → 807**。[Caption-G-lifetime.log](../artifacts/package-consumer/Caption-G-lifetime.log)

真实停靠宿主首次运行最终生产方案后完成 **130 项检查**，包含资源回归断言；具体采样如下。[Docking-client-caption-first.log](../artifacts/upstream-acceptance/Docking-client-caption-first.log)

| 完成循环数 | GDI | USER | 进程句柄 | 私有 MiB | 托管 MiB | 所测弱引用及退出宿主资源 |
|---|---:|---:|---:|---:|---:|---|
| 12（预热） | 20 | 47 | 983 | 69.31 | 2.32 | 全部 0 |
| 42 | 20 | 47 | 1169 | 101.51 | 3.67 | 全部 0 |
| 72 | 20 | 47 | 1349 | 112.53 | 4.26 | 全部 0 |
| 102 | 20 | 47 | 1530 | 121.87 | 4.87 | 全部 0 |

本次观测消除了默认自绘按钮路径先前的线性 GDI 增长，并保持同一场景的 USER 计数、退出 HWND、hook、subclass 和 GCHandle 状态稳定。句柄与内存仍在这段短时运行中增长，尚未确定其在更长运行中是否形成平台缓存上限；不能把当前 GDI 结果写成“所有原生资源都不增长”。

实际 Docking 示例的交互验收已确认：文档浮动、client 按钮最大化和还原、标题栏移动、标题栏右键系统菜单、左右边缘及底边拖动、菜单重新停靠均可操作。截图观测到左边缘调整宽度 **944 → 834**、右边缘调整宽度 **834 → 734**、底边调整高度 **611 → 531**；重新停靠后原内容仍在主窗口，浮窗消失。通过 computer-use 操作已有示例窗口；该验收工具的输入合成不属于产品代码。

默认 WinUI `ResizeBorderThickness` 仍为 0，依赖原生 resize frame；上述尺寸调整从截图最外缘发起，向内 2–3 像素未命中。不能把实际可操作结果描述为与上游 WindowChrome 默认 10 DIP 的命中宽度相同。这是已有平台适配差异，本轮未自行扩展命中区域。

当前 102 次自动循环主要覆盖创建、浮动、停靠、菜单和关闭；真实交互检查补充了按钮、移动和边缘缩放，仍不能代替不同 DPI、所有模板和长期负载验证。

## Debug 和 Release 最终复验

Docking、ToggleDocking、Mvvm 三个示例分别运行 Debug 和 Release，六份日志均为 **130 项检查通过、无 FAIL**。每次运行独立完成相同的 102 次生命周期循环，功能与资源断言一并执行。

| 示例与配置 | 第 12、42、72、102 次 GDI | 对应 USER | 进程句柄，第 12 → 102 次 | 私有 MiB，第 12 → 102 次 | 托管 MiB，第 12 → 102 次 | 最终日志 |
|---|---|---|---|---|---|---|
| Docking Debug | 20、20、20、20 | 71、73、73、73 | 1282 → 1897 | 119.98 → 169.02 | 2.32 → 4.70 | [日志](../artifacts/upstream-acceptance/Docking-Debug-final.log) |
| Docking Release | 20、20、20、20 | 72、73、73、73 | 1245 → 1847 | 133.05 → 178.16 | 2.32 → 4.71 | [日志](../artifacts/upstream-acceptance/Docking-Release-final.log) |
| ToggleDocking Debug | 20、20、20、20 | 70、73、73、73 | 1302 → 1909 | 122.52 → 174.86 | 2.48 → 4.75 | [日志](../artifacts/upstream-acceptance/ToggleDocking-Debug-final.log) |
| ToggleDocking Release | 20、20、20、20 | 72、73、73、73 | 1269 → 1880 | 135.58 → 191.61 | 2.44 → 4.70 | [日志](../artifacts/upstream-acceptance/ToggleDocking-Release-final.log) |
| Mvvm Debug | 20、20、20、20 | 71、73、73、73 | 1286 → 1889 | 121.09 → 176.88 | 2.26 → 4.68 | [日志](../artifacts/upstream-acceptance/Mvvm-Debug-final.log) |
| Mvvm Release | 20、20、20、20 | 72、73、73、73 | 1260 → 1866 | 130.50 → 191.55 | 2.27 → 4.66 | [日志](../artifacts/upstream-acceptance/Mvvm-Release-final.log) |

六份日志的每个采样点中，文档、内容、LayoutItem、presenter、浮动控件、菜单的存活弱引用均为 **0**，退出浮动宿主的窗口、已关闭窗口、内容宿主、存活 HWND、owner hook、subclass、self/owner GCHandle 字段均为 **0**。资源断言针对预热之后的 GDI、USER 增长及已释放对象和宿主状态，不把进程总句柄设置成零增长断言。

第 12 至第 102 次之间，各运行的进程句柄增加 **602—615**；私有内存增加 **45.11—61.05 MiB**；托管内存增加 **2.26—2.42 MiB**。这些趋势仍需较长负载继续观察，当前没有证据证明已形成缓存上限。六配置结果支持默认自绘按钮路径的 GDI/USER 和对象退出回归检查通过，不支持“所有资源无泄漏”或“长期稳定已保证”的扩大结论。

此前诊断日志中的“118 checks”表示当次功能断言完成，资源采样当时是观察值，不能证明 GDI 增长已解决。本次最终结论由新的资源回归断言和具体采样共同支持，不单凭 PASS 总数。

## 最终 NuGet 候选身份与消费复验

最终候选包为 `WinUI.AvalonDock.0.1.0-alpha.nupkg`，SHA-256 为 `DF6DE3109565569D7CF3DF82B97AB6ED71D1FCE7AFA93D674BC5493A78FFC55A`。这是 Release 构建的本地候选，未发布。消费宿主必须从本地包源重新恢复并核对相同哈希，避免同版本号缓存包使结果混入前一候选。

消费宿主的 Debug、Release 运行均已完成。两配置分别通过 **27 项公开接口集成检查**（14 项消费者集成、13 项固定上游 MVVM 行为）与 **130 项共享边界及兼容性检查**，日志没有 FAIL。配置重复执行增加构建和运行证据，不将其相加声称为 314 个不同使用场景。

| 消费配置 | 公开接口检查 | 边界及资源检查 | 包身份与来源 |
|---|---|---|---|
| Debug | [27 项通过](../artifacts/package-consumer/Debug-package-smoke.log) | [130 项通过](../artifacts/package-consumer/Debug-boundary-smoke.log) | [Debug-package-identity.log](../artifacts/package-consumer/Debug-package-identity.log) |
| Release | [27 项通过](../artifacts/package-consumer/Release-package-smoke.log) | [130 项通过](../artifacts/package-consumer/Release-boundary-smoke.log) | [Release-package-identity.log](../artifacts/package-consumer/Release-package-identity.log) |

两份包身份日志确认：候选包与实际恢复 `.nupkg` 均具有上面的 SHA-256；包源为本仓库 `sources/WinUI.AvalonDock/bin/Release`，恢复到独立的 `artifacts/local-package-cache`；资源类型为 **package**，项目依赖数为 **0**。恢复及输出的产品 DLL 哈希一致，为 `ECC651735B00B3A2281F23E006D041A46CBF1603AF8926D424FF0D2E0084DF01`。因此本次消费者结果对应实际候选包，而非产品项目引用或先前同版本缓存包。流程及适用范围见 [PackageConsumer README](../verification/PackageConsumer/README.md)。

| 消费配置 | 第 12、42、72、102 次 GDI | 对应 USER | 进程句柄，第 12 → 102 次 | 私有 MiB，第 12 → 102 次 | 托管 MiB，第 12 → 102 次 |
|---|---|---|---|---|---|
| Debug | 20、20、20、20 | 71、73、73、73 | 1255 → 1872 | 127.55 → 183.29 | 2.21 → 4.62 |
| Release | 20、20、20、20 | 72、73、73、73 | 1251 → 1858 | 129.29 → 183.95 | 2.22 → 4.59 |

两配置每个采样点的所测弱引用及退出宿主字段均为 **0**。最终包复现了示例中 GDI 稳定、USER 在预热后稳定及受观测对象释放的结果，也保留句柄和内存增长：Debug、Release 的进程句柄分别增加 **617、607**，私有内存分别增加 **55.74、54.66 MiB**，托管内存分别增加 **2.41、2.37 MiB**。这些结果没有扩大长期资源稳定性、所有模板或嵌入用户控件的保证范围。

本文链接的 `artifacts` 文件是本地运行产物，未作为源码交付承诺；复验时应保存独立文件名，并更新本文对应链接，避免覆盖诊断证据。
