# 04R-1 对话框生命周期实施与证据

## 基线与判定

- 起始／结束 HEAD：`2416b11842402071d69c3734f3987a74ac47d4a8`。未提交。
- 预检：仓库内及工作区上级未找到适用的 `AGENTS.md`；开始时工作树仅有用户未跟踪的 04R-1 任务说明，未覆盖该文件。
- 本报告只记录 04R-1，不重做 04A，不授权 04B。任务说明 `docs/scan-debug-workbench/04R-1-dialog-lifecycle.md` 是既有未跟踪输入，未修改。
- `04A_ASSESSMENT: COMPLETE_WITH_BLOCKERS`，见既有评估及修复报告；这些报告里的测试数和 UIA 结果是历史记录，不是本轮证据。
- `04R1_CODE: IMPLEMENTED`；`04R1_MANAGED_TESTS: PASS`；`04R1_NATIVE_DIALOG_TESTS: NOT_RUN`；`04B_EXTRACTION: NOT_AUTHORIZED`。
- `GLOBAL_LIFECYCLE_GATE: BLOCKED`。本轮没有关闭 `VM-001`、`UI-006`、`UI-007`，也没有解除整体提取门槛。

## 改动文件

本轮实现涉及以下既有代码和新增文件。另有本报告及 native 证据入口脚本。下列代码与测试文件均为工作树变更，未提交：

- `PRISM Utility/Views/ScanDebugPage.xaml.cs`
- `PRISM Utility/Views/ScanDebugDialogLifetime.cs`（新增）
- `PRISM Utility/ViewModels/ScanDebugViewModel.cs`
- `PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj`
- `PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs`
- `PrismUtility.Core.Tests/ScanDebugDialogLifetimeTests.cs`（新增）
- `PrismUtility.Core.Tests/ScanDebugFilmProfileOrchestrationSourceTests.cs`
- `PrismUtility.Core.Tests/ScanDebugGeometryUi007CharacterizationTests.cs`
- `PrismUtility.Core.Tests/ScanDebugPageReentryUi006SourceContractTests.cs`
- `PrismUtility.Core.Tests/ScanDebugViewModelVm002CharacterizationTests.cs`
- `docs/scan-debug-workbench/implementation-notes/run-04R-1-native-dialog.ps1`（新增）
- `docs/scan-debug-workbench/implementation-notes/04R-1-dialog-lifecycle.md`（本报告）

工作树还包含既有未跟踪任务说明，不属于本轮新增。没有改架构 registry、DI 生命周期、命令／持久化格式、设备协议、Canvas/QA 捕获实现，也没有拆分 Page 或 ViewModel。

## 责任划分与实现策略

- **旧缺口：** 三个 Page handler 只在局部变量中持有 dialog，卸载不能显式关闭已进入 `ShowAsync` 的窗口；两个 profile confirmation helper 等待裸 completion，且新建命令在确认之后未检查失效上下文。
- **Page / dialog lease：** `ScanDebugPage` 使用现有激活 epoch 与 Page owner 检查请求，卸载时先推进 epoch、使 owner 失效，再按旧 epoch 退休该 Page 的对话框请求。`ScanDebugDialogLifetime` 为当前 UI 线程的 Page 共用一个活动 lease，拒绝并发窗口；lease 对退休、完成和故障只结算一次。它捕获具体 dialog 实例，旧请求不能隐藏新请求的窗口。
- **请求 / VM：** Page 负责创建并显示 `ContentDialog`，将 primary、close、宿主退休或显示故障映射到各自 completion。VM 的 terminal cleanup token 让 profile confirmation、calibration prompt 和 notice 能结束等待；无订阅者时确认拒绝、notice 直接完成。`NewFilmProfile` 与导入路径在 await 后复查 cleanup 状态、Page owner 和 workspace snapshot，失效的迟到同意不能提交 workspace。操作 finally 释放 claim 与 Busy 状态。
- **UI dispatcher 边界：** 请求退休可在取消回调线程同步终结逻辑 TCS；真实 `ContentDialog.Hide()` 只通过捕获的 `DispatcherQueue` 投递，并只针对该 dialog 实例。该退休清理不走要求“仍是当前激活”的 `EnqueueForCurrentActivation`，避免推进 epoch 后丢弃旧窗口清理。dispatcher 拒绝时只报告未能 Hide，不伪称窗口已关闭。具体真实 WinUI 行为仍须 host 验证。
- **保留边界：** Page 卸载 / Deactivate 不等于 terminal `CleanupAsync`，也不停止扫描、不释放共享 session、USB lease 或硬件资源；共享设备生命周期仍由原 session manager 管理。没有改变 QA 捕获、Canvas/ROI 所有权或持久化语义。

## R1-01..R1-08 实现与证据映射

| 要求 | 实施与证据 | 状态／边界 |
|---|---|---|
| R1-01 激活周期归属与冲突处理 | Page epoch、owner 和 host token 入场复核；线程级 lease 绑定 Page、epoch，活动窗口冲突时拒绝并完成请求。`ScanDebugDialogLifetimeTests` 覆盖同线程共享登记、冲突、失效激活拒绝展示。 | managed PASS；未在真实 Frame / WinUI host 验证。 |
| R1-02 卸载先退休，再关闭实际 dialog | `OnUnloaded` 先失活、推进 epoch、invalidate owner，再退休旧 epoch lease；退休回调将具体 dialog 的 Hide 排入 dispatcher。测试覆盖先完成请求、再执行排队 Hide，以及旧 Hide 不影响新 dialog。 | managed ownership PASS；原生导航与窗口关闭 NOT_RUN。 |
| R1-03 终结幂等及 dispatcher 边界 | lease settled guard 与 release 保证单次结算；退休结果为拒绝，显示异常 fault 到本请求；取消注册在 Show 生命周期结束时释放。dispatcher 拒绝测试确认请求结束但 Hide 未发生；迟到结果不覆盖已终结请求。 | managed PASS；dispatcher/ShowAsync 的 WinUI 故障运行未验证。 |
| R1-04 confirmation / prompt / notice terminal cancellation | film profile confirmation 接入 terminal token，并覆盖 discard、import discard 和 replacement 包装路径；prompt、notice 延续其原有终止契约。无订阅者时拒绝或完成，不启动悬挂等待。 | 有真实 VM/fake 的 managed 测试 PASS；非 UI host。 |
| R1-05 失效确认不写 workspace | `NewFilmProfile` 和导入确认前后检查 cleanup、Page owner、snapshot；导入结果 await 后也复查。测试覆盖 Cleanup 后注入迟到 true、Page reentry 前后迟到 true、导入迟到结果、正常肯定/否定、显示故障及 claim/Busy 释放。 | managed PASS；未证明 native 窗口交错。 |
| R1-06 保持导航与硬件契约 | 生命周期改动只退休 Page dialog；未在卸载中新增 StopScan、lease 释放或 session Dispose；terminal cleanup 与 Deactivate 仍为不同路径。 | 源码及项目回归证据；真实设备行为本轮未测试，且不是本任务要求。 |
| R1-07 可控行为测试 | 使用可控 completion、假 dialog、排队 Hide 和 VM workspace harness，不靠长时间 Sleep；终结测试设有有限等待上限。 | managed PASS；代码测试不能替代真实 WinUI host。 |
| R1-08 分层结案 | 本报告分列代码、managed、native 与提取状态；不把应用构建或 managed fake 测试当成 native PASS。 | 已按要求分层；全局门槛保持 BLOCKED。 |

## R1-T01..R1-T10 状态

| 场景 | 当前证据 | 状态 |
|---|---|---|
| R1-T01 无订阅者 | VM 对 prompt/confirmation 安全拒绝、notice 立即完成的 managed 用例。 | PASS，managed |
| R1-T02 Dirty draft 新建，等待确认后 Cleanup | 真实 VM 命令路径的 terminal cleanup 行为测试；包含等待、迟到 true、workspace 不变。 | PASS，managed |
| R1-T03 T02 后注入旧 true | 与 T02 同一可控请求测试，确认迟到确认不能重置 workspace 或发布成功。 | PASS，managed |
| R1-T04 staged import 丢弃／替换等待中 Cleanup | New 命令的 staged discard 和 Load 的 replacement 两条路径均有可控 terminal cleanup 测试。 | PASS，managed |
| R1-T05 A 卸载、B 新 dialog、A 迟到收尾 | lease / fake dialog 测试覆盖旧请求、旧 Hide 与新 dialog 的隔离，但没有程序化 Frame host 导航运行。 | managed ownership PASS；native NOT_RUN |
| R1-T06 prompt/notice 显示中卸载或终止 | VM terminal cancellation 与 fake lease 测试分别覆盖请求结算和旧 dialog 退休；没有 WinUI host 实际导航关闭窗口。 | managed PASS；native NOT_RUN |
| R1-T07 Show 故障、宿主丢失、重复卸载 | managed lease 覆盖 Show 抛错、inactive admission、重复 retire、取消和 dispatcher rejection；VM confirmation Show 故障覆盖 workspace 不变与操作释放。 | managed PASS；native 子集 NOT_RUN |
| R1-T08 terminal 后再请求 | Cleanup 后重新执行新建命令的 managed 用例确认无事件派发、workspace 不变、Busy 不残留。 | PASS，managed |
| R1-T09 正常肯定／否定与原工作流 | Dirty draft 确认两种结果、无订阅者和相关导入/命令回归纳入测试。 | PASS，managed；非持久化／设备实机验收 |
| R1-T10 dialog 显示中关闭窗口 | 仓库没有可程序化模态 Frame 导航／注入的 native host；本轮没有实机 host 跑窗口关闭。 | NOT_RUN，native |

## 本轮实际验证

以下结果属于本轮执行，不是 04A 历史报告的旧数字：

- **修复前 RED：** 三个 `FilmProfileConfirmation_TerminalCleanup*` 测试在 Cleanup 后等待超时，3/3 RED；父级直接运行一次，代理也运行 3/3。新增的 show failure 路径也实际失败，异常为 `InvalidOperationException: show rejected`。这些是定位缺口的失败证据，不是当前结果。
- **Managed：** 修复后聚焦集曾为 127/127 PASS（最终额外断言加入前）。新增的 reentry/显示失败 targeted 集 5/5 PASS；terminal 后新建单例 1/1 PASS。完整 suite 的最终命令 `dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 -- RunConfiguration.MaxCpuCount=1` 为 **1501/1501 PASS**。不得把历史 04A 的 1477/1477 当成本轮结果。
- **Core build：** `dotnet build 'PRISM Utility.Core/PrismUtility.Core.csproj' --no-restore -p:Platform=x64 -v:q`，PASS，0 warnings、0 errors。
- **App build：** 以下 Release 命令的普通与 QA 两种开关均 PASS，且 QA=true 后再以 false 构建也 PASS：
  - `dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -p:PrismVisualQa=false -m:1 -v:q`
  - `dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -p:PrismVisualQa=true -m:1 -v:q`
  两种构建均 0 errors，各有 1 条既有 PublishSingleFile advisory。这是编译证据，不是 app/native dialog 运行证据。
- **Solution build：** `dotnet build 'PRISM Utility.sln' --no-restore -p:Platform=x64 -m:1 -v:q` FAIL，环境／项目平台限制：`DngSdkWarpper.vcxproj:36` 找不到 `Cpp.Default.props`（MSB4278），并有测试项目 MSIL/AMD64 reference mismatch（MSB3270）。不得将独立 app build 写成 solution build 通过。
- **Native dialog：** `R1-T05/T06/T07/T10` 均 NOT_RUN。Windows 环境存在；原因是仓库没有可执行的程序化 modal Frame 导航／注入 host，本轮也未连接真实硬件。脚本 `implementation-notes/run-04R-1-native-dialog.ps1` 仅生成含 AppPath、HEAD、build 开关、场景、日志与退出码字段的 evidence manifest，并拒绝无日志的 PASS；它不会启动 app 或驱动 UI。其 PowerShell parser 检查为 0 errors，不是 native 验证。

Native 证据入口（仅初始化 `NOT_RUN` 清单，需另有可程序化导航的 host 才能填写实际场景结果）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'docs/scan-debug-workbench/implementation-notes/run-04R-1-native-dialog.ps1' -AppPath 'PRISM Utility/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe' -EvidenceRoot 'evidence/04R-1-release' -PrismVisualQa false
```

## 有限后续

- 为 T05/T06/T07/T10 建立或提供 native host，程序化触发模态 dialog、在 UI dispatcher 导航／关闭并保存 trace；再按普通 Release 与适用的 QA 开关分别记录实测，不用 Shell 点击替代被模态遮挡的导航。
- 其他 04R-2、QA accepted capture 与卸载重叠、快速重入、故障注入和真实设备 / lease 行为，只在独立授权后处理。本报告不自动实施或声称这些已验证。
- native dialog 子集和全局生命周期门槛未通过前，继续保持 `04B_EXTRACTION: NOT_AUTHORIZED`，且 VM-001/UI-006/UI-007 不关闭。
