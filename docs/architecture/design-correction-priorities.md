# 设计整改优先级建议

## 目的和范围

本文记录 Host Software 当前不合理设计的整改建议和推荐顺序。它面向后续工程排期、评审和验收使用，来源是 [Issues and remediation](issues-and-remediation.md) 与 [System overview](system-overview.md)。

本文只记录建议，不代表任何生产代码、测试、构建脚本或架构索引已经被修改。它也不是固定 18 文件架构清单的一部分，不新增 registry ID，不替代问题登记目录。所有事实仍以 [issues-and-remediation.md](issues-and-remediation.md) 为准。

## 分类口径

| 分类 | 含义 | 处理方式 |
| --- | --- | --- |
| 已验证缺陷 | 当前源码中已经定位到的错误行为或未实现路径。 | 优先修复，并用最小可复现测试锁定。 |
| 设计风险 | 当前设计可以运行，但在并发、生命周期、模块边界或诊断上容易放大故障。 | 先补观测和测试，再做边界收敛。 |
| 测试缺口 | 功能依赖人工验证、环境前提或源码审查，自动化证据不足。 | 先分层补测试，再把结果接入 QA 记录。 |

## P0 确认缺陷

P0 先处理已经确认的缺陷。目标是让用户可见崩溃、未实现入口、单位误导和关键诊断缺口先退出危险状态。

| ID | 分类 | 当前不合理设计 | 推荐整改 | 验收信号 |
| --- | --- | --- | --- | --- |
| [APP-001](issues-and-remediation.md) | 已关闭缺陷 | `App_UnhandledException` 已调用 reporter 写 Debug mirror, Debug 和 Trace fallback, 且不设置 `Handled`。 | 保留 live WinUI event smoke 为环境阻塞项，不把它误记为 pass。 | APP-001 focused tests、full managed suite、x64 app build 和 independent harness 通过；live WinUI controlled-event smoke 是 `ENVIRONMENT_BLOCKED`。 |
| [UI-001](issues-and-remediation.md) | 已关闭缺陷 | `HeaderModeProperty` 已注册为 `NavigationViewHeaderMode` enum。 | 保留后续 WinUI 页面 smoke, 但当前 bool/enum contract 已由测试和 XAML 编译关闭。 | UI-001 source-contract tests 通过，x64 XAML compiler output 生成 enum member 和 setter cast。 |
| [USB-001](issues-and-remediation.md) | 已关闭缺陷 | `StopBulkIn` API 已退休，`StartBulkInAsync` 用调用方 token 和内部 runner 证明取消生命周期。 | 不再接受 StopBulkIn cancellation 作为验收，验收改为 API retirement 加 StartBulkIn cancellation proof。 | `Usb001ContractTests.cs` fake runner 证明 Starting, Running, Stopping, Stopped、fresh token restart 和 repeated cancellation。 |
| [IMG-001](issues-and-remediation.md) | 已关闭缺陷 | decoder 和 presenter 已拒绝 zero/invalid waterfall rows，valid BGRA 输出保持不变。 | 保留 IMG-002 性能风险，不再把 zero-row divide-by-zero 当作当前缺陷。 | IMG-001 focused tests 和 independent harness 覆盖 zero-row reject、malformed reject、stale output unchanged 和 valid output unchanged。 |
| [SET-002](issues-and-remediation.md) | 已关闭缺陷 | settings 和 log fallback 通过 shared compiled resolver 统一到同一 application-data root。 | 后续持久化入口继续复用 resolver 或补路径 parity 测试。 | `Set002SettingsPathTests.cs` 六例运行时测试和 data probe 证明 configured/null/empty/whitespace/custom roots 一致。 |
| [SCAN-006](issues-and-remediation.md) | 已关闭缺陷 | workflow 已拥有 warm-up enable/disable/finally，并把 cleanup failure 限定为 diagnostic。 | 不声称物理 hardware warm-up smoke 通过，当前证据是 actual service 加 independent fakes。 | SCAN-006 focused tests、full managed suite 和 harness 覆盖 false inert、enable ordering、cancel/failure cleanup、diagnostic-only cleanup failure 和 resume。 |
| [SCAN-007](issues-and-remediation.md) | 已关闭缺陷 | core/protocol/model 使用 nanoseconds，UI boundary 才执行 checked whole-microsecond conversion。 | 保留 UI `*Us` properties 作为文本边界，不允许非 UI `Us` aliases 回归。 | SCAN-007 focused tests、full suite、alias audit 和 external harness 证明 ns core/protocol、checked UI us conversion、no non-UI aliases。 |

## P1 并发和可靠性

P1 处理会让故障扩大、取消失效或诊断丢失的设计风险。这一组不一定立刻崩溃，但会让扫描、保存和排障在压力下变得不可靠。

| ID | 分类 | 当前不合理设计 | 推荐整改 | 验收信号 |
| --- | --- | --- | --- | --- |
| [SESSION-001](issues-and-remediation.md) | 已关闭缺陷 | 会话 action 已在 linked operation context 下运行，`_mutationGate` 不再包住 action body；dispose cleanup exact-once 且 bounded。 | 保持 action cancellation contract，不把 cleanup fault 快照语义混入本项。 | SESSION-001 相关 53 focused tests、409/409 managed tests、Core build 0 warnings/errors、x64 app build 0 errors 加既有 WindowsAppSDK warning，independent reviews CONFIRMED。 |
| [SCAN-001](issues-and-remediation.md) | 已关闭缺陷 | 合成、对齐、Task.Run worker、inner loops 和 UI/export 调用链已传递取消令牌，输出写入使用 temp transaction 和 rollback。 | 保留 SCAN-002 typed alignment outcomes 与 IMG-002 分配成本为独立风险，不声称 live UI 或硬件 smoke。 | `Scan001CancellationTests.cs` 和 `Scan001OutputOwnershipTests.cs` 覆盖取消、bounded exit、target preservation、pair rollback 和 publish failure。 |
| [SCAN-002](issues-and-remediation.md) | 已关闭缺陷 | 对齐结果已 typed 为 `Aligned`, `Disabled`, `Fallback` 或 `Failed`, 并携带 diagnostic kind、channel metadata 和 native OpenCV metadata。ECC 失败后 mutual information fallback 可见, fatal empty buffers 失败且无 buffers, fallback 保留可用 normalized buffers。 | 新增 alignment caller 必须镜像 diagnostics, 并保持 cancellation propagation。SCAN-003+ 回调异常隔离仍是独立开放项。 | SCAN-002 相关 26 个 focused tests 两次通过, combined 487/487 managed tests 通过；覆盖 all caller diagnostic mirroring。 |
| [SET-001](issues-and-remediation.md) | 已关闭缺陷 | 非打包 settings 已用单 gate 串行 read/save，并通过 same-volume atomic writer 写 temp、flush、replace/move，失败时 rollback 内存值。 | SET-003 已关闭分组 document 保存缺口；SET-001 继续作为单 key atomic persistence contract。 | `Set001LocalSettingsAtomicPersistenceTests.cs` 七例覆盖 DI、atomic writer、并发保存、失败保留 durable state 和 temp cleanup。 |
| [SET-003](issues-and-remediation.md) | 已关闭缺陷 | 传输、色彩和校准分组设置已使用 schema 1 versioned single documents。current document 优先；legacy 只在 document 缺失时迁移；future、malformed 或 payload 无效 document 不回读 legacy。 | SET-004 通用 schema/version 边界仍是独立开放项；新增分组设置继续先保存 document, 再发布内存 generation。 | SET-003 相关 18 个 focused tests 两次通过, combined 439/439 managed tests 通过；filesystem harness 证明迁移幂等、失败不污染 durable hash 和 legacy keys 在无 delete API 时惰性保留。 |
| [SET-005](issues-and-remediation.md) | 已关闭缺陷 | Settings 保存由 owner-scoped `SettingsSaveCoordinator` 管理。language、theme、debug、transfer 和 color 五个 family, 包括 restore/default, 都按 per-scope result commit 或 rollback, failure 先 mirror diagnostic, 再经 UI dispatcher 更新既有 InfoBar。 | 继续保持 SET-004 schema/version 边界独立。新增设置 family 必须复用 coordinator owner、per-scope error 和 bounded teardown contract。 | SET-005 相关 29 个 focused tests 两次通过, combined 487/487 managed tests 通过；normal/recovery en/zh visual captures clean, rendered failure InfoBar/accessibility evidence 为 `HARNESS_BLOCKED`。 |
| [LOG-001](issues-and-remediation.md) | 已关闭缺陷 | 文件日志 append admission 已跟踪 accepted work, `FlushAsync` 等待 snapshot 内 accepted appends, `ShutdownAsync` idempotent 并拒绝 shutdown 后文件 append。应用关闭先执行 scanner cleanup, 再对 mirror shutdown 做 caller-bounded async wait。 | 保持 APP-001 顶层异常策略独立，不在 handler 中做同步磁盘写入；post-shutdown 和 append failure diagnostic 必须先写入再完成 append work, same-thread per-instance reentrant shutdown guard 继续防自锁。 | LOG-001 相关 19 个 focused tests 两次通过, combined 487/487 managed tests 通过；覆盖 queue admission ordering、diagnostic-before-completion、same-thread reentrant shutdown guard、scanner-before-log close 和 post-shutdown file policy。 |
| [LOG-002](issues-and-remediation.md) | 已关闭缺陷 | `Mirror` 已先完成 recent entry、Debug/Trace 和 file append admission，再隔离通知 `EntryMirrored` subscribers；diagnostic sink 异常不会外逃。 | LOG-001 已单独关闭 accepted append flush 和 shutdown wait；LOG-002 继续只表达 subscriber isolation contract。 | `Log002DebugOutputMirrorTests.cs` 八例覆盖 core-first ordering、subscriber isolation、safe diagnostic sink、bounded reentry 和 append failure diagnostics。 |
| [UI-002](issues-and-remediation.md) | 已关闭设计风险 | 导航 route 已改为六值 `AppRoute`, typed `INavigationService` 和 `IPageService`, nullable enum `NavigationHelper.NavigateTo`, exhaustive `PageService` registration, 并移除生产导航 full-name route 字符串。 | 新增 route 必须走 `AppRoute`、`PageService`、Shell literal、exact Page host pair 和 route contract tests；UI-005 unset item diagnostic 仍独立开放。 | UI-002 focused 16 个 tests 两次通过；UI-002+UI-003 focused 24 个 tests 通过；full managed suite 511/511 通过；Core/app builds 通过且只保留既有 app warning；当前源码和 contract tests 覆盖六值 route 闭集，旧七路导航 UIA/visual 仅作为历史证据；Full validator 18/18, 20 Mermaid。 |
| [UI-003](issues-and-remediation.md) | 已关闭设计风险 | Page ViewModel 暴露已收敛到 `IPageViewModelHost<TViewModel>`。`PageService.Configure<TPage, TViewModel>` 约束 exact route/Page/ViewModel pair，六个真实 Page 都声明精确 host contract，`FrameExtensions` 直接读取 host 并对非 host 内容明确失败。 | 新增导航页面必须同步 `AppRoute`、`PageService` registration、Page host 声明、negative compile harness 和 production metadata probe。VM-001、UI-006 和 TEST-002 仍开放。 | UI-003 focused 8 个 tests 两次通过；negative compile missing host、wrong pairing 和 invariance 通过；real app metadata probe 通过；lifecycle callback order 锁定；旧七路导航 UIA/visual 仅作为历史证据，不描述当前六页 host 集合。 |

## P2 模块边界重构

P2 在 P0 和 P1 稳住之后处理。这里的重点不是换目录，而是让导航、生命周期、UI 编排和 ViewModel 投影有可测试边界。

| ID | 分类 | 当前不合理设计 | 推荐整改 | 验收信号 |
| --- | --- | --- | --- | --- |
| [UI-006](issues-and-remediation.md) | 设计风险 | Scan 和 ScanDebug 启用 NavigationCache，并持有较多 UI 状态副本。ScanViewModel 当前仍拥有执行命令、预览投影、settings/profile 投影和 cached output state。VM-001 extraction hard gate 为 `EXTRACTION_BLOCKED`。 | gate 解除前只补 characterization 证据；task 20 必须走 blocked branch；不提取 presenter、workspace 或 adapter。 | UI-006 source characterization 15 个 focused tests 两次通过，VM-001 4 个 focused characterization tests 两次覆盖 blocker 传播，full managed suite 520/520 通过，Core/app builds 通过且只保留既有 app warning；不声称运行时安全。 |
| [UI-007](issues-and-remediation.md) | 设计风险 | ScanDebug code-behind 仍承担 Canvas bitmap、display sizing、zoom/pan pointer、ROI drag pointer、cursor sample text、axis and ROI overlay drawing、ContentDialog completion 和当前校准照明 TextBox/ComboBox sync；ROI selection、overlay toggles、ROI input TextBox 和 apply/reset command 仍在 ViewModel/XAML 绑定面。 | VM-001 和 UI-006 仍阻塞提取。gate 解除前只补 Canvas/ROI/display/pointer/overlay/dialog/text sync characterization；不迁出 Page 逻辑，不调用生产 extraction 或硬件服务。task 21 必须走 blocked branch。 | Task 20 historical broad-filter 19 个 tests 两次通过，regression subset 9 个 tests 通过，full managed suite 534/534 通过，Core build 和 x64 app build 通过且只保留既有 app warning。证据只支持 `EXTRACTION_BLOCKED` 和 source-only ownership, 不证明 runtime WinUI safety。 |
| [VM-001](issues-and-remediation.md) | 设计风险 | cached ScanPage、构造函数 `Activate()`、Loaded 无 guard 的 Page `PropertyChanged +=`、Unloaded 单次 `-=`、`CleanupAsync` 零调用者、`_uiLifetimeCts` 只在 cleanup 取消、app close 不调用 cleanup 共同阻塞提取。 | 先明确 cached page lifecycle 和 cleanup ownership。当前 outcome 是 `EXTRACTION_BLOCKED`，VM-001 仍是 open design risk。 | Todo 18 completed as blocked evidence: evidence 4 focused characterization tests twice, full managed suite 515/515, source caller graph, Oracle `CONFIRMED_BLOCKED`。 |
| [VM-002](issues-and-remediation.md) | 设计风险 | `ScanDebugViewModel` 集中生命周期、调试扫描、校准、预览和 profile 快捷入口。task 21 已记录 `SPLIT_BLOCKED`，当前职责、commands、bindings、events、lifecycle 和 collaborators 仍留在同一 ViewModel。 | gate 解除前只补行为 characterization；不拆出预览、ROI、校准和 profile projection。VM-002 保持 open risk。 | VM002 focused source/projection tests 16/16 两次通过，UI007/UI006/VM001 regression subset 23/23 通过，FilmProfile regression 204/204 通过，final full managed suite 540/540 通过。证据只支持 source-contract 和 blocked outcome，不证明 runtime safety 或 production split。 |

## P3 测试和构建缺口

P3 不是低价值工作。它排在后面，是因为前面几组需要先定义稳定行为。VM-002 的 `SPLIT_BLOCKED` 结果已经说明没有剩余 P2 extraction task 可以继续；CAL-001 managed gap 已关闭，DNG-001 managed validation/status seam 子范围已通过，但 DNG-001 整体仍因 native/JXL 证据不可用而是 `ENVIRONMENT_BLOCKED`。剩余硬件、UI 和 native 前提要继续分层记录为通过、跳过或环境阻塞。

| ID | 分类 | 当前缺口 | 推荐整改 | 验收信号 |
| --- | --- | --- | --- | --- |
| [CAL-001](issues-and-remediation.md) | 已关闭测试缺口 | historical CAL-001 closure evidence 已有 15 个 deterministic fake tests 覆盖黑场和白场收敛、黑白振荡最佳采样回退、invalid width/ROI clamp、scan failure、motion timeout、cancellation、warm-up cleanup、0/2 号焦点电机 all-path stop 和未归一化 Brenner 锁定。当前 Todo 15 software-gate snapshot 另记录 focused calibration status 104/104、parent focused gate 183/183 和 full Core suite 1034/1034。 | 不把 managed fake harness 或 Todo 15 software gate 写成物理 hardware smoke；CAL-002 保持独立未关闭。 | historical/superseded CAL-001 closure evidence 是 CAL001 focused tests 15/15、full managed suite 555/555、Core/app builds 和 Full validator 通过；current evidence 引用 Todo 15 focused calibration status 104/104、parent focused 183/183、full Core 1034/1034；真实 scanner smoke 仍为 `ENVIRONMENT_BLOCKED`。 |
| [DNG-001](issues-and-remediation.md) | 测试缺口 | managed validation/status seam 已通过。task 24 在 this task24 environment 证明 VS2022、v143、MSBuild 和 inspected Adobe_DNG_SDK/native wrapper dependency readiness 为 `PASS`，但 native build 是 `SKIP`/not run，native bridge smoke 和 JXL stub runtime smoke 仍没有维护中的 runnable fixture/artifact 证据。native C++ packed row-stride minimum 和 blackLevelPlaneCount equality 仍是 open unverified pre-existing parity risk。 | 保留 managed sub-scope `PASS` 和 task24 dependency probe `PASS`, 整体保持 `ENVIRONMENT_BLOCKED`。后续有可加载 native artifact 和维护中的 runtime probes 后再补 native smoke、JXL stub smoke 和 parity enforcement 验证。 | Evidence .omo/evidence/task-23-design-correction-repairs.json 记录 initial RED, final focused 36/36, full managed 591/591, Core build 0 warnings/errors, changed-file diagnostics clean, Oracle `MANAGED_SUBSCOPE_APPROVED`。Evidence .omo/evidence/task-24-design-correction-repairs.json 记录 layered runner managed 591/591 `PASS`, Core `PASS` 0 errors, x64 app `PASS` with existing WindowsAppSDK PublishSingleFile warning, native dependency probe `PASS`, native build `SKIP`, timeout and cleanup `PASS`, overall runner `PASS`/exit 0。新增 blackLevelPlanes length vs SamplesPerPixel seam-not-invoked test 是 green characterization, not fabricated RED；不把 x64 managed ABI layout tests 当作 native C++ `sizeof` proof，也不把 JXL stub source inspection 当作 runtime smoke。 |
| [TEST-001](issues-and-remediation.md) | 测试缺口 | Full solution build 依赖 WinUI、Windows App SDK、Visual C++ 和 Adobe DNG SDK 本机状态。 | 拆分 managed tests、app build、native build dependency probe。 | Full QA 分别记录 Core test、Core build、solution build、native dependency probe 的 exit code。 |
| [TEST-002](issues-and-remediation.md) | 测试缺口 | 当前 source-contract filters for UI-002/UI-003, VM-001/UI-006 and UI-007/VM-002 pass, and historical native UIA captures exist for route navigation, Settings, Film Profile Editor, ScanDebug, CJK and export picker states. These are not maintained comprehensive UI automation. | 在用户不接受新 UI framework 的前提下, 保持 TEST-002 `BLOCKED`, 只记录当前证据和真实缺口。不要新增 Appium、WinAppDriver、Playwright 或其它 UI automation dependency。 | Source contracts `PASS`; current native UIA rerun may be `HARNESS_BLOCKED`; remaining gaps explicitly include NavigationCache lifecycle, ScanDebug Canvas pointer/ROI, file picker cancel/success, Settings save-error InfoBar accessibility, language switch state retention, window-close teardown and hardware-driven pages. |

## 整改原则

1. 先修已验证缺陷，再收敛设计风险，最后扩展测试矩阵。
2. 每个 registry ID 单独关闭，不把多个模块的行为混在一个大改里。
3. 修复前先补能失败的最小测试，除非缺陷只能通过手动 WinUI 或硬件 smoke 观察。
4. 不把文档建议当作实现证据。关闭任何 ID 都需要源码、测试和必要文档一起更新。
5. 并发问题优先定义所有权、取消、flush 和异常隔离，再调整 UI 呈现。
6. 模块边界重构先保行为，后拆结构。拆分 `ScanDebugViewModel` 和 code-behind 前必须有可回归的状态测试。

## 推荐执行阶段

| 阶段 | 推荐范围 | 目标 | 完成信号 |
| --- | --- | --- | --- |
| Phase 0 | `APP-001`, `UI-001`, `USB-001`, `IMG-001`, `SET-002`, `SCAN-006`, `SCAN-007` | 已移除确认缺陷和误导性 API 行为。 | 相关单元测试、source-contract 测试、managed harness、USB fake 测试和文档验证通过；APP live WinUI event smoke 与硬件 smoke 只在环境可用时补跑。 |
| Phase 1 | `SESSION-001`, `SCAN-001`, `SCAN-002`, `SET-001`, `SET-003`, `SET-005`, `LOG-001`, `LOG-002` | 稳住长操作、保存、日志和取消语义。 | 压测、取消测试、失败注入测试和日志 flush 测试通过。 |
| Phase 2 | `UI-002`, `UI-006`, `UI-007`, `VM-001`, `VM-002` | 收窄 UI、导航、ViewModel 生命周期和 ScanDebug 编排边界。UI-003 已由 compile-time Page host contract 关闭；VM-001、UI-006 和 UI-007 当前为 `EXTRACTION_BLOCKED`，VM-002 当前为 `SPLIT_BLOCKED`。 | 导航契约、页面生命周期和 UI state 后续仍需可回归；VM-001 -> UI-006 -> UI-007 blocked chain 解除前 UI-006、UI-007 和 VM-002 只做 characterization-only。task 21 full managed suite 540/540 与 FilmProfile 204/204 只支持 source-contract evidence，不关闭 VM-002。 |
| Phase 3 | `CAL-001`, `DNG-001`, `TEST-001`, `TEST-002` | CAL-001 managed gap 已关闭；DNG-001 managed seam 子范围已通过但整体 `ENVIRONMENT_BLOCKED`；构建环境前提、UI/hardware smoke 仍按各自 ID 分层记录。 | CAL-001 historical/superseded closure evidence 可引用 15/15 focused、555/555 managed、builds 和 Full validator；current Todo 15 evidence 可引用 focused calibration status 104/104、parent focused 183/183、full Core 1034/1034。DNG-001 可引用 36/36 focused、591/591 managed、Core build 0 warnings/errors 和 Oracle managed approval, 但 native/JXL smoke 继续标为 `ENVIRONMENT_BLOCKED`；真实 scanner smoke 继续标为 `ENVIRONMENT_BLOCKED`。 |
| Phase 4 | 回读 [Issues and remediation](issues-and-remediation.md) | 按最新源码重新排序剩余 `Medium` 和 `Low` 项。 | registry、模块文档和验证脚本一致，不出现新旧状态混写。 |

## 验收信号总表

| 维度 | 必须看到的信号 |
| --- | --- |
| 缺陷关闭 | 对应 ID 的复现用例先失败，修复后通过。 |
| 并发可靠性 | 取消、flush、异常隔离和失败注入都有自动化或可重复手动证据。 |
| 模块边界 | 路由、Page ViewModel 契约、缓存页面订阅和 ScanDebug UI 状态有测试保护。 |
| DNG 和 native bridge | 缺 SDK、缺 DLL、原生状态码和 JXL stub 被明确区分，不再把环境缺失误报为普通代码失败。 |
| 文档一致性 | registry ID、模块文档链接和验证脚本输出一致，文档只说已经实现的事实。 |

## 后续维护

整改开始后，每次关闭或重新分类 registry 项，都应先更新 [issues-and-remediation.md](issues-and-remediation.md)，再决定本文是否仍需要保留原推荐顺序。本文可以记录排期建议，但不能作为关闭缺陷、风险或测试缺口的唯一证据。
