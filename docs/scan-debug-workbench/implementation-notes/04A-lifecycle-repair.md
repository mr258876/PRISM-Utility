# 04A 后续：VM-001 / UI-006 生命周期修复记录

## 基线、范围与结论

- 基线：`dev`，SHA `36e2695e2638230ecba5a137c7ba779292f3c1c5`。本报告记录阶段 04A 评估后的指定修复，不替代[原 04A 评估](04A-lifecycle-and-extraction.md)，也不改写其基线结论。
- 本次交付包含本报告、6 个生产文件和 9 个测试文件。生产文件：`PRISM Utility/App.xaml.cs`、`PRISM Utility/PrismVisualQaCaptureService.cs`、`PRISM Utility/ViewModels/ScanDebugViewModel.cs`、`PRISM Utility/ViewModels/ScanViewModel.cs`、`PRISM Utility/Views/ScanDebugPage.xaml.cs`、`PRISM Utility/Views/ScanPage.xaml.cs`。
- 修改的既有测试：`PrismUtility.Core.Tests/FilmProfileSourceContractTests.cs`、`PrismUtility.Core.Tests/Log001DebugOutputMirrorLifecycleTests.cs`、`PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs`、`PrismUtility.Core.Tests/ScanDebugGeometryUi007CharacterizationTests.cs`、`PrismUtility.Core.Tests/ScanDebugViewModelVm002CharacterizationTests.cs`、`PrismUtility.Core.Tests/ScanPageLifecycleVm001CharacterizationTests.cs`、`PrismUtility.Core.Tests/ScanWorkspaceUi006CharacterizationTests.cs`。新增测试：`PrismUtility.Core.Tests/AppCloseLifecycleSourceContractTests.cs`、`PrismUtility.Core.Tests/ScanDebugPageReentryUi006SourceContractTests.cs`。
- `ScanPage` Page 级重复 `Loaded` guard 是本次新增；VM session event guard 是基线已有。Debug Page/VM 的 attach-detach、workspace 与 bitmap 清理，以及 App 关闭时对 session manager 的关闭能力已有基线，不将其冒称为本次新建。
- 页面生命周期不拥有设备共享资源：session manager 管理共享 session、USB lease 和硬件访问；debug mirror shutdown 是应用关闭流程的最后阶段。页面离开不隐式停止扫描或释放硬件。命令绑定、持久化格式与硬件调用语义未变；原 `Window.Closed` 清理改为可取消的 `AppWindow.Closing` 预关闭流程，VM terminal cleanup 语义有意改变。未开始 04B，VM-001/UI-006 仍开放，本报告不批准提取或将 registry issue 标为 Closed。

## 修复映射

| 要求 | 本次修复 | 证据与边界 |
| --- | --- | --- |
| P4-01：入口、操作与资源 owner | 保留 session manager 对共享 session/lease 的所有权；App 负责终止清理协调，最后关闭 debug mirror。 | App close source contract、managed tests 与两次离线 UIA close 运行覆盖其相应范围。设备操作及真实 lease 行为未作硬件验证。 |
| P4-02：旧生命周期工作不得污染新 owner | Scan Page 增加 Page 级 Loaded guard；VM terminal token 在异步/排队工作执行时复查。App 开始关闭时同步阻止新 QA start；若关闭中止则撤销阻止。Page/VM 在 await 后检查 terminal 状态。 | source contract、managed tests；普通及 QA 条件 Release 离线 UIA 关闭通过。非所有异步交错均由运行场景覆盖。 |
| P4-02：缓存页重入与当前 owner | ScanDebug Page 将 reservation 与“最后成功 ATTACH 的 Page owner”分开；仅成功 attach 的 Page 可执行匹配的 deactivate，避免 Loaded/Unloaded 振荡误换 owner。VM prompt/notice 无 handler 时立即拒绝/完成；待 subscriber 的等待可由 terminal CTS 取消。 | reentry source contract 与 managed tests。校准 fake command 测试证明迟到确认不发布 candidate；**未证明 manager lease release**。Page 自有 dialog 在 unload 时的 dismiss 仍是残余门禁。 |
| P4-02：QA capture 故障恢复与 terminal admission | `StopAsync` 的 finally 清理已完成且匹配的任务并重置 `_stopping`；timer/Page 故障会被报告；终止信号同步阻止 QA Start、timer 入场与请求循环接受新请求，已接受任务继续 drain。 | managed/source tests 与 QA 条件构建；QA 原生离线两次进入产生递增 ready 时间戳，但尚未以真实 accepted capture 与 unload 重叠或注入 QA 故障后重试验证。不能把源码顺序等同于该运行证据。 |
| P4-02：应用退出与 VM terminal cleanup | App 同步进入 shutdown 阻止新 QA start；可取消关闭被中止时调用 `CancelShutdown`。关闭继续时先清理 VM，再关闭 settings、scanner session manager，最后停止 debug mirror。VM terminal cleanup 取消相关 lifetime/scan token，并隔离迟到 UI 工作，不接管设备资源释放。 | App close source contract、managed tests、普通及 QA 条件离线 UIA close 通过。pending dialog、失败重试与重复关闭边界仍待验证。 |
| P4-T01：连续进入／离开 | Page Loaded guard、ScanDebug activation/owner 防护及资源清理。 | 离线 UIA 对 Scan/Log/ScanDebug 共 7 次访问通过；不证明长期内存趋势或持续导航下 dispatcher 残留。 |
| P4-T02：离开后迅速返回 | ScanDebug Page owner reservation/attach 分离、VM subscriber wait terminal cancellation。 | managed/source contract 通过；活跃操作期间快速真实导航/重入未运行。 |
| P4-T08：应用退出 | App preclose、VM cleanup、共享 manager shutdown 与 mirror 最后停止。 | 两条离线 Release UIA trace 均为 7/7 visits PASS，close PASS、exit 0、无强制结束。pending dialog、重复 close 和真实设备 session 关闭未验证。 |
| P4-T09：命令／host/workspace 回归 | 修改的 7 个既有测试与 2 个新增 source contract 测试文件。 | 当前完整 Core suite 1477/1477 PASS；不等同于全部 WinUI、硬件或视觉路径验证。 |

## RED → GREEN 与最终验证

- 修复前聚焦 managed suite 为 **34/34 PASS**。缺口包括 Scan Page Page-level Loaded guard、terminal scan/UI token 防护、Debug Page 重入 owner、App 实际 VM terminal cleanup caller 与 preclose 顺序。相应测试曾先捕获缺失行为为 RED，再随修复转为 GREEN。历史 RED/GREEN 仅说明相应测试开发过程，不替代下面的最终集成证据。
- 当前完整 managed suite：`dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 -- RunConfiguration.MaxCpuCount=1`，**PASS 1477/1477**。最后追加的 QA 入场源码测试实际运行 **1/1 PASS**；代理结果因证书错误不可用，不宣称该条的 RED 运行证据。
- Core x64 build：`dotnet build 'PRISM Utility.Core/PrismUtility.Core.csproj' --no-restore -p:Platform=x64 -v:q`，**PASS，0 warnings / 0 errors**。应用 Release build：`dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -p:PrismVisualQa=true -m:1 -v:q` 及相同命令的 `-p:PrismVisualQa=false`，均 **PASS，0 errors，1 条既有 WindowsAppSDK SingleFile advisory**；最后恢复普通构建。
- 完整 solution build：`dotnet build 'PRISM Utility.sln' -p:Platform=x64 --no-restore -m:1 -v:q`，**ENVIRONMENT_BLOCKED**：`DngSdkWarpper.vcxproj:36` 缺少 `$(VCTargetsPath)\Microsoft.Cpp.Default.props`。这不推翻 Core/app 单独构建通过，也不构成 solution build 成功。
- QA=true 原生离线 UIA：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage04a-lifecycle-offline-qa.ps1' -AppPath 'D:\ProgrammingStuff\Project PRISM\Host Software\PRISM Utility\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PrismUtility.exe' -EvidenceRoot 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage04a-ready-final-fence' -RequireVisualQaReady`。trace：`stage04a-lifecycle-11a09d10a2224ddebfd32247eb57e749.json`。Scan 3、Log 2、ScanDebug 2，共 **7 visits PASS**；fresh ready UTC 为 `05:24:26.915Z`、`05:24:27.431Z`；close **PASS**、exit 0、无强制结束。这验证正常 Stop 后重新 Start，不是关窗与新请求入场的竞态注入。
- 最终普通 Release 原生离线 UIA：上述脚本不传 `-RequireVisualQaReady`，EvidenceRoot 为 `C:\Users\Mr258876\AppData\Local\Temp\opencode\stage04a-final-fence`；trace `stage04a-lifecycle-442ac35a498d4f1692343cb485158977.json`。**7/7 visits PASS**，close **PASS**，exit 0，无强制结束。
- 证据分层：managed/source contract 是测试证据；build 是编译证据；上述两份 trace 是原生离线 UIA 导航和关闭运行证据。它们均不是硬件、accepted capture 与 unload 重叠或完整视觉验收证据。

## 残余门禁与下一步

- **NOT_RUN**：accepted QA capture 与卸载重叠、注入 QA 故障后真实重试、活跃扫描跨页导航、快速导航重入、Page-owned dialog unload dismiss、重复关闭期间的时序，以及完整焦点/Canvas 绘制释放视觉路径。现有源码和 managed tests 不替代这些 host 场景。
- **NEEDS_DEVICE_VALIDATION**：真实设备扫描、断连、CTS 取消、设备替换、运动/照明及 lease 释放。本轮未连接硬件、未发送硬件命令。
- Oracle 两轮只读复核均为 **PARTIAL**。第一轮的 terminal prompt cancellation、最后成功附着 owner、QA 故障恢复与终止准入已作针对性修复；第二轮发现 timer/请求循环未检查 shutdown 标志，现已加入 guard 并通过新增源码契约及最终全量回归。关窗与 timer 入场的原生竞态尚未注入验证，不能把该源码测试记成运行时 PASS。Page-owned dialog unload dismiss 仍未解决为可验证门禁。
- 最小后续：完成 Page-owned dialog unload dismiss 的修复与测试；为 QA 故障重试、accepted capture unload overlap、活跃扫描导航和重复关闭补充安全 host/fake 场景。需要真实硬件的项目须另行授权验证。
- VM-001/UI-006 仍开放。补齐门禁、复核原 registry 并取得独立批准之前，不关闭 issue、不授权 04B 或提取。
