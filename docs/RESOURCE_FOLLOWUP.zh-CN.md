# 资源生命周期后续调查：312 次循环与句柄类型

记录日期：2026-10-08。本文接续 [资源生命周期实测与诊断记录](RESOURCE_LIFETIME.zh-CN.md)，单独记录扩大循环次数、停止操作后的空闲观察，以及外部 Process Snapshotting（PSS）句柄快照。旧文档中的 102 次候选包验收、包哈希和最终示例数据保持原样，本文件不把新诊断冒充旧候选包复验。

本轮四个 Debug 运行均已完成：普通 WinUI `Window`、`DesktopWindowXamlSource`、`MenuFlyout`，以及 AvalonDock 文档创建、浮动、停靠、菜单和关闭循环。每个运行先预热 12 次，再执行三批各 100 次操作，共 **312 次**；随后停止这些循环 **30 秒**，每 5 秒记录一次无强制 GC 的状态，最后再 GC 并采样。

这组结果确认：在当前机器和运行时中，纯 WinUI 场景也能产生停止操作后仍存在的 Composition/Event 句柄增长，因而该现象不要求构造 `DockingManager`。后续匹配对象组合的纯 SDK 场景也完成 312 次循环，句柄增长与停靠场景接近，见下文。仍不能据此宣布 SDK 缺陷根因已确定，也不能宣布库全部资源安全。

## 运行范围与证据

沿用本轮环境：Windows 11 专业版 **10.0.26200 / build 26200**，.NET SDK **10.0.401**，Windows App SDK **2.5.1**，`net10.0-windows10.0.19041.0` / `win-x64`。本表仅包含 **Debug** 后续调查，不扩展为 Release、其他 Windows 版本、其他 SDK 或其他用户控件的结论。包版本见 [Directory.Packages.props](../Directory.Packages.props)；前一轮环境和候选包验收见 [旧记录](RESOURCE_LIFETIME.zh-CN.md)。

| 场景 | 本轮实际操作 | PID | 完成证据 |
|---|---|---:|---|
| Window | 普通 WinUI 窗口和 TextBox 激活、关闭，无停靠布局 | 3500 | [日志](../artifacts/resource-investigation/Debug-Window-312.log)、[应用 CSV](../artifacts/resource-investigation/Debug-Window-312.csv)、[PSS 索引](../artifacts/resource-investigation/Debug-Window-312-handle-snapshots.csv) |
| XamlSource | 同一主窗口中创建、初始化、释放 DWXS 和 TextBox，无停靠布局 | 26384 | [日志](../artifacts/resource-investigation/Debug-XamlSource-312.log)、[应用 CSV](../artifacts/resource-investigation/Debug-XamlSource-312.csv)、[PSS 索引](../artifacts/resource-investigation/Debug-XamlSource-312-handle-snapshots.csv) |
| Menu | 新建 MenuFlyout 和内容，打开、关闭，再移除内容，无停靠布局 | 23324 | [日志](../artifacts/resource-investigation/Debug-Menu-312.log)、[应用 CSV](../artifacts/resource-investigation/Debug-Menu-312.csv)、[PSS 索引](../artifacts/resource-investigation/Debug-Menu-312-handle-snapshots.csv) |
| Docking | 创建文档 → Float → Dock → 新 MenuFlyout 打开/关闭 → 文档 Close | 20572 | [日志](../artifacts/resource-investigation/Debug-Docking-312.log)、[应用 CSV](../artifacts/resource-investigation/Debug-Docking-312.csv)、[PSS 索引](../artifacts/resource-investigation/Debug-Docking-312-handle-snapshots.csv) |

前三份日志各记录 312 次循环和 30 秒尾部空闲完成；Docking 日志最后为 **158 项检查通过**。这个通过数包含按采样阶段执行的 GDI/USER、弱引用和库宿主检查，**没有把进程总句柄零增长列为通过条件**。实现见 [RuntimeLifetimeSmokeTests.cs](../verification/PackageConsumer/RuntimeLifetimeSmokeTests.cs) 和 [WorkspaceLifecycleSmokeTests.cs](../sources/Experiments/Shared/WorkspaceLifecycleSmokeTests.cs)。

上述四运行采集期间没有为它们的句柄增长修改产品实现。随后独立覆盖窗口实验复现了另一条托管保留链，见下文；该路径不参与这些程序化文档浮动循环，不能用它解释四运行的 Composition/Event 增量。组合 SDK 场景、覆盖窗口后验、独立托管堆采集与只读根分析均已取得结果。覆盖窗口修复后已生成本地新候选包，完成两配置消费验收及六示例矩阵，身份和范围见下文；旧记录中的候选包哈希只对应旧验收。

## 应用内计数：操作结束与空闲后的区别

下表直接使用各进程自身 CSV 中的计数。增长从第 12 次预热后算起，至 312 次后的尾部空闲及最终 GC，共包含 **300 次新增操作**。

| 场景 | 第 12 次句柄 | 第 312 次句柄 | 空闲 30 秒、无 GC | 空闲后最终 GC | 预热至最终净增 | GDI：预热 → 最终 | USER：预热 → 第 312 次 → 最终 |
|---|---:|---:|---:|---:|---:|---|---|
| Window | 1012 | 1618 | 1614 | 1619 | **+607** | 16 → 16 | 39 → 41 → 41 |
| XamlSource | 999 | 1602 | 1596 | 1603 | **+604** | 16 → 16 | 40 → 41 → 41 |
| Menu | 1001 | 1794 | 1596 | 1601 | **+600** | 9 → 9 | 33 → 70 → 34 |
| Docking | 1201 | 3008 | 2999 | 3004 | **+1803** | 20 → 20 | 49 → 50 → 50 |

纯 Window、XamlSource 和 Menu 的最终增长在这次 300 次区间约为 **2 个句柄/操作**，Docking 约为 **6 个句柄/操作**。这些是本次区间的平均观察值，不是每次 API 调用固定分配数量或未来运行的增长率保证。

Menu 的第 312 次 checkpoint 记录了 **1794 句柄、70 USER**；5 秒空闲时已经回落到 **1597 句柄、34 USER**，最终为 **1601 句柄、34 USER**。因此既有可以回落的短时资源，也有 30 秒及最终 GC 后仍未回到预热水平的增长，不能把瞬时峰值全部归为持续保留。其余场景尾部空闲也有少量计数波动；强制 GC 后数值偶尔上升几项不代表 GC 本身制造了已定位的泄漏。

| 场景 | 私有内存 MiB：预热 → 最终 GC | 净增 MiB | 托管内存 MiB：预热 → 最终 GC |
|---|---|---:|---|
| Window | 89.42 → 103.38 | +13.96 | 0.450 → 0.573 |
| XamlSource | 88.65 → 97.98 | +9.32 | 0.450 → 0.568 |
| Menu | 94.18 → 118.45 | +24.27 | 0.445 → 0.539 |
| Docking | 115.20 → 178.84 | +63.64 | 2.326 → 8.443 |

MiB 按字节除以 1,048,576 计算，来自 `privateBytes` 和 `managedBytes`，不是工作集或原生堆对象归属分析。托管内存增加与所测文档对象弱引用清零可以同时发生；尚不能仅凭总量判断是哪一类对象或缓存保留。

四场景的应用 checkpoint 均显示所测 host/content 存活弱引用为 **0**。Docking 的 document、LayoutItem、presenter、floating control、menu 也全部回落为 **0**；采样时 `nativeWindows`、`ownerHooks`、`subclasses`、`selfHandles`、`ownerHandles` 均为 **0**。这只覆盖显式观测的对象及库宿主资源，不能覆盖全部 WinRT/COM/Composition 内部对象。当前版本会清除已经释放的诊断弱引用，CSV 中 `retiredWindows` 等数量为 0 不应解释为从未创建过浮动窗口。

## 外部 PSS 快照的来源和自检

诊断工具独立位于 [NativeHandleSnapshot.cs](../verification/diagnostics/NativeHandleSnapshot.cs)、[Capture-ProcessHandles.ps1](../verification/diagnostics/Capture-ProcessHandles.ps1) 和 [Run-ResourceInvestigation.ps1](../verification/diagnostics/Run-ResourceInvestigation.ps1)，不编译进产品或包消费宿主。

工具使用微软公开的 [PssCaptureSnapshot](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/nf-processsnapshot-psscapturesnapshot) 读取目标进程快照，再用 [PssWalkSnapshot](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/nf-processsnapshot-psswalksnapshot) 枚举句柄；请求句柄、名称、基础和类型专用信息。输出的 `Type`、`Name` 及 Event 状态来自 [PSS_HANDLE_ENTRY](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/ns-processsnapshot-pss_handle_entry)。类型名称为空的条目保留为空，不猜测它们的类型。

每次采集在 `finally` 中释放 walk marker、调用 [PssFreeSnapshot](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/nf-processsnapshot-pssfreesnapshot) 释放采集进程内的快照，并关闭诊断工具自己打开的 process handle。工具不向目标进程注入代码，不调用 `CloseHandle` 关闭目标拥有的句柄，也不销毁目标窗口或共享 compositor。当前结构布局仅支持 x64 诊断进程。

已归档的 [自检日志](../artifacts/resource-investigation/diagnostic-self-check.log) 记录：

- 工具识别出测试程序自有、状态已知的 **manual-reset 且 signaled 的 Event**，检查通过。
- 连续 **25 次**快照和清理后，自检进程句柄 **572 → 570**。该总量仍受自检进程运行时活动影响；这支持没有观察到“每采一次就固定累积”的诊断器清理问题，不能替代所有失败路径和所有 PSS 类型的完整验证。

`ManualReset` 表示事件的复位方式，`Signaled` 只表示捕获该事件时的信号状态。它不代表“已经释放”“可以由库安全关闭”，也不能单独证明创建者或持有者。Type 名称 `Composition` 也不能直接映射成某一个公开 WinRT 类或特定 SDK 缺陷。

## 快照时序限制

应用先写出 checkpoint CSV，外部脚本通过 `WaitForExit(1000)` 间隔检查，发现最新阶段后才采集 PSS。PSS 索引的 `utc` 在采集之后写入，应用 CSV 的 `utc` 是应用计数时间；四运行共 42 个索引条目，两者相差 **0.044—0.952 秒**。这是本轮实测延迟，不能作为以后运行的最大延迟保证。

因此索引中的 `stage/cycles` 仅表示“外部看到了这个阶段”，不保证进程仍停留在该阶段，也不保证 `snapshotEntries` 与 `observedHandles` 相等。下一轮操作、异步拆卸、弹出层和后续检查均可能发生在两次采样之间。部分短阶段在下次轮询前已被新 checkpoint 替代，故 Window/Docking 各 11 份 PSS 快照，XamlSource/Menu 各 10 份，少于每个应用 CSV 的 12 行。

两个实际例子说明这个限制：

- Menu 第 312 次应用计数是 **1794**，约 **0.580 秒**后的 PSS 为 **1601**；应用在 5 秒空闲时也回落为 1597。不能用这份 PSS 追溯第 312 次应用 checkpoint 的全部临时句柄。
- Docking 最终 GC 的应用计数是 **3004**，对应外部快照为 **3033**；其后边界检查还会继续创建窗口等临时对象。不能把多出的 29 项直接认作生命周期循环结束时的遗留资源。

下面使用各运行确实捕获到的相同文字阶段做类型对照，并另列共同存在的 **25 秒空闲阶段**。它们仍是异步快照，不按精确的每轮创建次数反推分配器行为。

## 句柄类型与 Event 状态

| 场景 | PSS 总条目：预热 → 第 312 次 → 空闲 25 秒 | Composition：预热 → 第 312 次 → 空闲 25 秒 | Event：预热 → 第 312 次 → 空闲 25 秒 | manual-reset 且 signaled Event：预热 → 第 312 次 → 空闲 25 秒 |
|---|---|---|---|---|
| Window | 1035 → 1617 → 1616 | 23 → 316 → 316 | 346 → 645 → 645 | 58 → 352 → 352 |
| XamlSource | 1006 → 1601 → 1596 | 20 → 316 → 316 | 343 → 641 → 639 | 55 → 352 → 351 |
| Menu | 1040 → 1601 → 1601 | 35 → 316 → 316 | 348 → 640 → 640 | 58 → 351 → 351 |
| Docking | 1200 → 3012 → 3003 | 63 → 963 → 963 | 404 → 1312 → 1310 | 96 → 998 → 997 |

本表原始快照：Window [预热](../artifacts/resource-investigation/Debug-Window-312-handles-01.csv) / [312 次](../artifacts/resource-investigation/Debug-Window-312-handles-04.csv) / [25 秒空闲](../artifacts/resource-investigation/Debug-Window-312-handles-09.csv)；XamlSource [预热](../artifacts/resource-investigation/Debug-XamlSource-312-handles-01.csv) / [312 次](../artifacts/resource-investigation/Debug-XamlSource-312-handles-04.csv) / [25 秒空闲](../artifacts/resource-investigation/Debug-XamlSource-312-handles-09.csv)；Menu [预热](../artifacts/resource-investigation/Debug-Menu-312-handles-01.csv) / [312 次](../artifacts/resource-investigation/Debug-Menu-312-handles-04.csv) / [25 秒空闲](../artifacts/resource-investigation/Debug-Menu-312-handles-09.csv)；Docking [预热](../artifacts/resource-investigation/Debug-Docking-312-handles-01.csv) / [312 次](../artifacts/resource-investigation/Debug-Docking-312-handles-04.csv) / [25 秒空闲](../artifacts/resource-investigation/Debug-Docking-312-handles-09.csv)。

从预热到 25 秒空闲，Composition / Event 分别增加 **293 / 299**（Window）、**296 / 296**（XamlSource）、**281 / 292**（Menu）、**900 / 906**（Docking）。Event 增量主要出现在 manual-reset 且 signaled 一组，其余 Event 状态只有小幅变化。File 在这三个阶段分别恒为 **48、48、48、69**；类型为空的条目为 **221 → 220、215 → 215、219 → 218、228 → 228**；Thread 数量也只在少数几项内波动。这些快照支持主要持续增量集中于 Composition/Event，而不是 File、Thread 或未知类型整体线性增加。

这些仍然是 **类型和状态的证据**。快照没有提供 native allocation stack、具体 WinRT 对象身份、COM 引用持有者，或某个事件注册 token 到句柄的映射。尤其当前证据尚未证明 Composition 与 Event 一一配对，也未证明它们究竟属于长期缓存、资源拆卸缺陷、SDK、操作系统组件或它们的组合。

## 匹配对象组合的纯 SDK 对照

PID **11704** 的 Debug 组合运行没有创建 DockingManager 或停靠布局。每轮创建一个顶层 Window 和一个额外 DWXS，将同一个 TextBox 从主树移入浮动岛再返回，随后新建一个 MenuFlyout 并打开、隐藏；caption 采用本库 HWND 样式方案并配置 Caption/Passthrough InputRegions。事件采用命名处理器，清理顺序匹配库：解除 TakeFocusRequested → 清私有 Border.Child → source.Content=null → source.Dispose → Window.Content=null/Close → 重挂编辑器 → 菜单 → 移除编辑器。日志注明组合宿主在初始、浮动和返回三阶段显式聚焦，强于停靠循环自身的显式聚焦次数，因此这是接近工作量的对照，不是逐指令等价证明。[日志](../artifacts/resource-investigation/Debug-Composite-312.log)、[应用 CSV](../artifacts/resource-investigation/Debug-Composite-312.csv)、[PSS 索引](../artifacts/resource-investigation/Debug-Composite-312-handle-snapshots.csv)、[实现](../verification/PackageConsumer/RuntimeLifetimeSmokeTests.cs)

| 指标 | 第 12 次预热 | 第 312 次 | 空闲 30 秒、无 GC | 空闲后最终 GC |
|---|---:|---:|---:|---:|
| 进程句柄 | 1085 | 2900 | 2893 | 2898 |
| GDI | 21 | 21 | 21 | 21 |
| USER | 40 | 41 | 41 | 41 |
| 私有内存 MiB | 98.33 | 163.29 | 155.28 | 153.40 |
| 托管内存 MiB | 0.482 | 0.685 | 0.748 | 0.652 |

预热至最终 GC 句柄 **+1813**，在该 300 次区间同样约 **6 个/操作**；与 Docking 的 **+1803** 接近。Window、TextBox、DWXS、Menu 四类已观测弱引用在采样时均为 **0**，最后各 **0/312**。这支持完整组合工作量可以在不构造 AvalonDock 的条件下产生同数量级增长，不等于所有增长已经逐项归属到 SDK，也不覆盖两个宿主之间的内存差异。

| 组合 PSS 指标 | 预热 | 第 312 次 | 空闲 25 秒 |
|---|---:|---:|---:|
| 总条目 | 1133 | 2899 | 2897 |
| Composition | 64 | 940 | 940 |
| Event | 382 | 1273 | 1272 |
| manual-reset 且 signaled Event | 87 | 976 | 976 |

原始快照：[预热](../artifacts/resource-investigation/Debug-Composite-312-handles-01.csv)、[312 次](../artifacts/resource-investigation/Debug-Composite-312-handles-04.csv)、[25 秒空闲](../artifacts/resource-investigation/Debug-Composite-312-handles-09.csv)。预热至 25 秒空闲的 Composition / Event 净增为 **876 / 890**，主要增量类型与 Docking 一致。组合预热 PSS 在应用计数约 **0.903 秒**后取得，循环已经可能继续；相同阶段文字不意味着精确配对了 300 次操作。组合 11 份快照与 checkpoint 的实测时间差为 **0.119—0.988 秒**，遵循上文的异步时序限制。

组合还增加了只读 Island 观察。各 checkpoint 的 `threadIslands`、`threadOpenIslands`、`threadConnectedIslands` 始终为 **1 / 1 / 1**，读取错误为 **0**；所观测到的当前线程 Island 数量没有按循环增长。这只是 API 枚举的结果，不能覆盖 SDK 内部所有 Composition 对象。

对每个 DWXS 的关联 Island，诊断只在同一个同步方法内短暂取得引用：释放前读状态，移走用户树、source.Content=null 和 source.Dispose 后立即再读，不跨 await，也不保存 Island 在 tracker、回调或返回对象中。全部 **312 次**读到释放前 **IsClosed=false / IsConnected=true**，释放后立即为 **IsClosed=false / IsConnected=false**，读取错误 **0**。这里没有主动关闭借入的 Island。立即读到的 `IsClosed=false` 既不是我们已经确认的释放契约违约，也不能单独判为泄漏；需与后续线程枚举数量、弱引用和 native 资源调查一起理解。[ContentIsland API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentisland?view=windows-app-sdk-2.0)

## 独立覆盖窗口：托管保留复现和修复后验

在四场景之外，使用测试自有 destination 窗口和编辑器，循环 **20 次**“创建 OverlayWindow → Show/Hide/Show → 关闭 overlay 和 destination → GC”，没有注入键鼠。修复前 [Overlay-before.log](../artifacts/upstream-acceptance/Overlay-before.log) 最终记录 **FAIL**：

| 阶段 | overlay / private template view / surface / destination 存活 | destination Window / editor 存活 | overlay native registry / 退休 native HWND | GDI / USER | 进程句柄 |
|---|---|---|---|---|---:|
| 4 组关闭及 GC 后 | 各 4/4 | 各 0/4 | 0 / 0 | 64 / 40 | 1146 |
| 12 组关闭及 GC 后 | 各 12/12 | 各 0/12 | 0 / 0 | 64 / 40 | 1172 |
| 20 组关闭及 GC 后 | 各 20/20 | 各 0/20 | 0 / 0 | 64 / 40 | 1204 |

日志中 external `AppWindow.Closing` 取消关闭、Show/Hide/Show 渲染、编辑器焦点和 native HWND 关闭检查通过；失败发生在已经关闭的 overlay 及其私有视图、surface、destination 仍按数量保留。**native HWND 已关闭和 GDI/USER 稳定，仍不足以证明托管生命周期正确。**

已实施的窄修只位于 [OverlayWindow.cs](../sources/WinUI.AvalonDock/Controls/OverlayWindow.cs)：将库注册的 `Window.Closed`、`AppWindow.Closing`、私有 view `Loaded` 和 Template DP callback 改为可解除的命名处理器/保存 token；真正关闭后统一解除，并仅在 `Window.Content` 仍是本库私有 template view 时清空该内容，再释放 surface。surface 的原生绘制与 GDI 释放代码没有改变。

只读比对显示，此修改保留已有 `OnClosing` 和 native `args.Cancel` 传递，没有在取消关闭时提前释放；Public `Close`、`CloseHost`、Hide、停靠目标和拖动规则保持原逻辑。清理不写用户 destination/editor 的 DataContext、Binding、Style 或内容。固定上游的关闭取消仍遵循 WPF Window；解除本库 WinUI native 事件订阅属于已关闭对象的适配清理，不引入新的停靠功能。[固定上游 OverlayWindow.cs](https://github.com/Dirkster99/AvalonDock/blob/408dc2896e2f41f3bb79a15207f160edee8a6792/source/Components/AvalonDock/Controls/OverlayWindow.cs)

修复后的源码宿主已分别完成 Debug、Release **20 次**真实窗口运行，日志末尾均为 **RESULT: PASS**。[Debug 后验](../artifacts/upstream-acceptance/Overlay-after-Debug.log)、[Release 后验](../artifacts/upstream-acceptance/Overlay-after-Release.log)

| 修复后场景 | 第 4 / 12 / 20 组的六类弱引用 | native registry / 退休 HWND | GDI：第 4 → 12 → 20 组 | USER：第 4 → 12 → 20 组 | 句柄：第 4 → 12 → 20 组 |
|---|---|---|---|---|---|
| Debug，PID 21528 | overlay、view、surface、destination、destination-window、editor 均 0 | 0 / 0 | 64 → 64 → 64 | 40 → 40 → 40 | 1142 → 1178 → 1210 |
| Release，PID 38324 | 同上六类均 0 | 0 / 0 | 64 → 46 → 50 | 40 → 40 → 40 | 1115 → 1126 → 1162 |

两个配置均通过完整 Show/Hide/Show 渲染、编辑器焦点、native 窗口退出及 external AppWindow.Closing 取消关闭检查。所测覆盖窗口保留链在该后验中已消除；Release 的 GDI 数量有回落和小幅变化，不能概括成“两个配置均恒定 64”。句柄总量仍有增加，20 次后验也不构成长时间或所有模板的零增长保证。本项验证的是当前修复后源码宿主，不是旧候选包内容的新声明。

## 覆盖窗口修复后的本地候选包

这次新增生命周期修复已进入新生成的本地 Release `.nupkg`，版本仍为 **0.1.0-alpha**；相同版本号不代表相同包内容。新候选身份为：

| 文件 | SHA256 |
|---|---|
| WinUI.AvalonDock.0.1.0-alpha.nupkg | `19CA9B0C42CF0A38CBA7B74D5082CF82818676DFA941E216C5169693AE3929CE` |
| 包内、隔离缓存及消费输出的 WinUI.AvalonDock.dll | `E01CDB73951646DD3CFFE73660631E8AB811FA254AF49D86CB830CE06B2F0714` |

旧 [RESOURCE_LIFETIME](RESOURCE_LIFETIME.zh-CN.md) 中包哈希 `DF6DE3109565569D7CF3DF82B97AB6ED71D1FCE7AFA93D674BC5493A78FFC55A` 的候选身份与验收属于前一轮，不能据当前源码和新包覆盖它的历史结论。下列 `artifacts/package-consumer` 身份/验收日志及六示例 `*-final.log` 路径已由本轮 runner 复用，当前内容证明的是新候选与新源码，不用于追溯旧哈希的原始日志内容。此次只生成和验收本地包，没有发布到 nuget.org。

独立 PackageConsumer 两配置均从本地 Release 包源还原到 `artifacts/local-package-cache`，`AssetType=package`、`ProjectReferences=0`，包与 restored package 哈希吻合，product DLL 哈希吻合。证据：[Debug 身份](../artifacts/package-consumer/Debug-package-identity.log)、[Release 身份](../artifacts/package-consumer/Release-package-identity.log)、[消费宿主说明](../verification/PackageConsumer/README.md)。

本轮全解决方案 Debug、Release 构建分别完成，均 **0 警告、0 错误**，耗时 **26.74 / 23.76 秒**。[本轮 Debug 构建](../artifacts/upstream-acceptance/Solution-Debug-final-build.log)、[本轮 Release 构建](../artifacts/upstream-acceptance/Solution-Release-final-build.log)。Release 全解决方案构建临时传入 `GeneratePackageOnBuild=false` 以保留已经冻结并验收的候选包，不是永久修改项目打包配置；旧 `build-Release-final.log` 保留为旧记录，不用它冒充本轮构建。

| 新包消费配置 | 公共集成检查 | boundary / 上游兼容检查 | 独立 Overlay 20 次 |
|---|---|---|---|
| Debug | [27 PASS](../artifacts/package-consumer/Debug-package-smoke.log) | [130 PASS](../artifacts/package-consumer/Debug-boundary-smoke.log) | [RESULT: PASS](../artifacts/package-consumer/Debug-overlay-lifetime.log) |
| Release | [27 PASS](../artifacts/package-consumer/Release-package-smoke.log) | [130 PASS](../artifacts/package-consumer/Release-boundary-smoke.log) | [RESULT: PASS](../artifacts/package-consumer/Release-overlay-lifetime.log) |

新包 boundary 的 102 次文档循环，第 12、42、72、102 次 GDI 均 **20**；USER 为 Debug **49、49、50、50**，Release **48、49、50、50**；所观测弱引用及退出宿主资源字段为 0。句柄仍为 Debug **1184 → 1733**、Release **1181 → 1729**，不作为零增长保证。

消费包内独立 Overlay 20 次与上文源码宿主运行是不同进程、不同日志。包消费 Debug 的 GDI 为 **54、54、50**，USER **39、40、40**；Release 的 GDI 为 **64、64、64**，USER **39、39、40**。两配置均六类弱引用 0、overlay registry 和退休 native HWND 0。它确认修复进入实际安装的候选包，不把两个后验运行的具体计数混合使用。

六个示例的 Debug/Release 最终矩阵已完成，逐份日志的末尾均核对为 **130 项检查通过**：

| 示例 | Debug | Release |
|---|---|---|
| Docking | [130 PASS](../artifacts/upstream-acceptance/Docking-Debug-final.log) | [130 PASS](../artifacts/upstream-acceptance/Docking-Release-final.log) |
| ToggleDocking | [130 PASS](../artifacts/upstream-acceptance/ToggleDocking-Debug-final.log) | [130 PASS](../artifacts/upstream-acceptance/ToggleDocking-Release-final.log) |
| Mvvm | [130 PASS](../artifacts/upstream-acceptance/Mvvm-Debug-final.log) | [130 PASS](../artifacts/upstream-acceptance/Mvvm-Release-final.log) |

默认 runner 另按配置执行独立 Overlay 回归，两份最终日志各完成 **20 次**循环并以 `RESULT: PASS` 结束：[Debug 最终 Overlay](../artifacts/upstream-acceptance/Overlay-Debug-final.log)、[Release 最终 Overlay](../artifacts/upstream-acceptance/Overlay-Release-final.log)。这些配置复跑是同一检查集在不同宿主/配置中的重复验证，不能把检查总数相加当作独立用户场景数量。候选源码与包身份在验收时冻结，再核对 `.nupkg` 哈希仍为上述 `19CA9B0C...3929CE`。

## 独立托管堆：容量增长与强根证据

独立 profiling 运行 PID **30792** 完成 312 次 Docking 循环和尾部空闲，日志末尾为 **158 项检查通过**。它另行采集两个 gcdump 和一个后续 roots dump，不能与上文无托管采集运行的 CSV 拼接。[独立日志](../artifacts/resource-investigation-managed/Debug-Docking-312.log)、[独立 CSV](../artifacts/resource-investigation-managed/Debug-Docking-312.csv)、[完整只读分析](../artifacts/resource-investigation-managed/heap-analysis.md)

两个 gcdump 报告为 **3,936,831 → 9,170,903 GC Heap bytes**、**44,909 → 67,539 objects**，报告间差值 **5,234,072 bytes / 22,630 objects**。`12` 是外部看到第 12 次 checkpoint 后的采集触发点，应用仍继续操作，不能称严格停在第 12 次时刻。GC 请求、工具采集、导出和后续 dump 都影响时序；普通 CSV `GC.GetTotalMemory`、gcdump、SOS 也有不同统计规则。[前一 heapstat](../artifacts/resource-investigation-managed/Debug-Docking-312-12-heapstat.txt)、[后一 heapstat](../artifacts/resource-investigation-managed/Debug-Docking-312-312-heapstat.txt)

gcdump report 的 `Object Bytes` 是类型代表尺寸，数组还按大小桶分类，不能直接将该列乘 Count 得到各数组类型的精确总占用。完整数量增量单独保存在 [heap-type-deltas.csv](../artifacts/resource-investigation-managed/heap-type-deltas.csv)；下列终点数组容量来自后续 SOS，而不是这种乘法。[微软 report 实现](https://github.com/dotnet/diagnostics/blob/main/src/Tools/dotnet-gcdump/CommandLine/PrintReportHelper.cs#L77)

| 已核实的类型或容量 | 证据 | 能说明的范围 |
|---|---|---|
| `ManagedObjectWrapperHolder[]` | 后续 SOS-live **312 个 / 3,184,320 bytes**，占该时刻 **6,416,602 bytes** SOS-live 堆约 **49.63%** | 运行中的强可达数组容量；312 是数组数，不能解释成 312 个退出窗口被保留。 |
| 最大抽样数组和 List | 数组 **524,312 bytes / 65,536 slots**，List `_size=56,506`；前 12 个槽位全是同一个 holder | 抽样已证明同一个 wrapper 重复占据列表槽位，不能从抽样宣称每个数组的所有槽位都相同。 |
| `LocalizedStringExtension` | 两 gcdump **346 → 3613**，增量 **3267**；64 bytes/实例，浅层增量 **209,088 bytes** | 必须记录数量增长，不能仅凭类型名判定库强根泄漏。 |

最大数组的 holder 包装的是当前仍在使用的 `LayoutDocumentPaneControl`。SOS 找到的 3 条 managed root 经仍执行的测试 async state machine 和当前 Workspace / 文档 / 布局 / pane 控件，进入 ComWrappers 的 List 和数组。保留当前 workspace 符合测试状态；容量增长证据是相同 holder 被反复追加，而不是已退出窗口实例沿库集合保留。[数组和槽位输出](../artifacts/resource-investigation-managed/heap-holder-array-elements.txt)、[root 输出](../artifacts/resource-investigation-managed/heap-extra-roots.txt)、[holder 与当前 pane](../artifacts/resource-investigation-managed/heap-holder-and-avalondock-live.txt)

目标实际运行时为 **.NET 10.0.12**，与 .NET SDK 10.0.401 是不同版本信息。保存的目标 DLL IL 确认 `GetOrCreateComInterfaceForObject` 调用 `RegisterManagedObjectWrapperForDiagnostics`，后者对所属对象的 List 执行 `Add`，没有去重；与微软固定 `v10.0.12` 源码一致。结合 root、重复槽位与 IL，**可推断本例一项主要 live backing-array 容量来自该运行时诊断关系表的重复追加机制**。这项机制不要求正在 attach 调试器，不意味着所有托管增长都已归因，也不解释 PSS 的 Composition/Event 句柄。[目标 IL 输出](../artifacts/resource-investigation-managed/heap-comwrappers-il-and-handles.txt)、[固定版本 ComWrappers 源码](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ComWrappers.cs#L707)

LocalizedStringExtension 在后续 SOS 中为 **live 0 / dead 3613**，两个抽样实例各 `Found 0 unique roots`。这尚未证明本库静态集合或事件强引用了这些实例。SOS 的 managed live/dead 分类不等于完整审计跨 COM / native reference tracker 引用，也不保证下次 GC 一定回收；不能把它们全部宣布安全，或把它们全部宣布强根泄漏。[Localized live 输出](../artifacts/resource-investigation-managed/heap-localized-live.txt)、[Localized dead 输出](../artifacts/resource-investigation-managed/heap-localized-dead.txt)、[抽样 root 输出](../artifacts/resource-investigation-managed/heap-extra-roots.txt)

本轮只读分析没有反射清空运行时诊断表、改运行时设置或主动释放借用 COM 对象。后续应在固定检查点、匹配 interop 工作量的纯 SDK 宿主中进一步验证。库内重复属性写入是否放大 interop 次数可以作为窄优化评估，但任何减少必要调用都须保留上游行为、绑定、样式及回归证据。

## 对产品边界的含义与下一步

当前正常关闭代码已经解除库注册的 `Window.Closed`、`AppWindow.Closing/Changed`、非客户区 `RegionsChanged` 和 DWXS `TakeFocusRequested`，并执行 `Content=null`、`DesktopWindowXamlSource.Dispose()`。只读核对没有发现一个已经证实可解释当前增量的遗漏释放调用。这不等于不存在其他路径问题。

不能为清理计数而关闭从用户宿主借到的 `ContentIsland`、`InputFocusController`、UI 线程共享 compositor、dispatcher 或主窗口；也不能把临时 `Unloaded` 改成永久释放，改变固定上游的重新加载、独立浮动、取消关闭或焦点行为。具体兼容边界见 [上游兼容基线](UPSTREAM_COMPATIBILITY.zh-CN.md)。

后续调查需要分别回答：

1. 已完成组合宿主确认同类、同数量级增长；继续用 native allocation stack 或版本对照确定具体归属。不能只靠单项计数相加，或组合总数接近，宣布每个句柄已经定位。
2. 已找到当前 pane 关联 ComWrappers 诊断数组的重复容量和 managed root；继续用固定检查点、匹配 interop 工作量确认其趋势，并核实跨 COM/native 引用。托管采集仍须独立记录，不混入未采堆空闲数据。
3. 覆盖窗口已完成上述修复后验，仍需区分 Detached、用户自定义模板等尚未被这一文档循环覆盖的资源路径。已经复现并修正的覆盖窗口保留链不能解释未执行该路径的当前文档循环。
4. 若组合仍增长，进一步结合 Island 数量、native allocation stack、版本对照定位具体资源归属；不通过主动关闭共享 SDK 对象迎合计数。

可在仓库根目录用独立输出目录复跑已完成的五个 312 次场景，避免覆盖本次归档；先确保对应 Debug 宿主已构建：

```powershell
.\verification\diagnostics\Run-ResourceInvestigation.ps1 -Configuration Debug -Cycles 312 -Batch 100 -IdleSeconds 30 -ResultsDirectory .\artifacts\resource-investigation-rerun
```

以上工作用于缩小资源归属范围。30 秒空闲、GDI/USER 稳定及所测弱引用清零，均不能替代长时间运行、其他环境、其他模板和实际用户负载下的稳定性验证。
