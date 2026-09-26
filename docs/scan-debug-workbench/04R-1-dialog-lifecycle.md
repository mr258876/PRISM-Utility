# PRISM ScanDebug：阶段 04 复核与 04R-1 对话框生命周期收尾

> 建议放入：`docs/scan-debug-workbench/04R-1-dialog-lifecycle.md`。
> 本文是一个新的、有边界的修复任务，不是重新执行整个阶段 04，也不授权 04B 职责拆分。
> 复核日期：2026-09-26。参考代码：`2416b11`（`life cycle repair`），父提交 `36e2695e2638230ecba5a137c7ba779292f3c1c5`。[S1]
> 执行者应先读取适用的 `AGENTS.md`、确认当前 HEAD 和用户已有修改；HEAD 已变化时逐项复核本文发现，不覆盖已经正确完成的实现。
> 本文来自源码与仓库报告复核。文档作者未运行 Windows 程序、未重跑测试、未连接设备；文中的既有运行结果属于仓库报告，不是本次独立复测。

## 一、为什么不能继续用“把阶段 4 全部做完”下任务

原始阶段 04 中，04A 是评估，可以用 BLOCKED 结论完成；04B 是需独立授权的提取切片。仓库已经提交了 04A 评估和后续修复报告。评估完成、局部修复完成、WinUI 场景通过、允许提取，是不同状态，不应共用一个“阶段 4 是否全绿”的判定。[S2][S3]

本轮只修复对话框请求从创建到退出的生命周期。完成本轮不表示 VM-001/UI-006 全部关闭，更不要求提取 Page、ViewModel 或预览资源所有权。剩余运行验证与其他缺口登记为后续任务，不自动扩大当前实施范围。

## 二、当前证据与边界

### 2.1 已有工作应保留

最近修复已经接入实际 ViewModel 的应用终止清理，并补充 Page 激活归属和终止取消相关保护；不是“只有 Cleanup 方法而没有任何调用者”。不要为了本任务重写这一套机制。[S1][S4]

仓库修复报告记载 managed suite 1477/1477、Core/app 独立构建和普通/QA 离线导航关闭通过；报告同时明确保留对话框、活动任务、QA 并发等验证门槛。完整 solution 的 C++ props 缺失被单独标为环境问题。[S3]

历史 04A 报告有自己的旧基线，不能将其改写成新代码已经通过。需要更新的是当前状态说明与新修复记录，而不是篡改历史证据。[S2]

### 2.2 本轮针对的源码缺口

| 编号 | 复核位置 | 观察与需要验证的行为 |
|---|---|---|
| D1 | `ScanDebugPage.xaml.cs`：三个对话框事件处理器、`OnUnloaded` | 对话框是局部变量，卸载没有对应的显式关闭/请求终结机制。解除事件订阅不能终止已经进入 `ShowAsync` 的处理器。[S5] |
| D2 | `ScanDebugViewModel.cs`：两个 profile confirmation helper | `RequestFilmProfileDiscardConfirmationAsync` 和 `RequestFilmProfileImportConfirmationAsync` 直接返回 completion Task；不同于已有 prompt/notice 的 terminal-aware 等待。[S6] |
| D3 | `NewFilmProfile` 及上述 confirmation 的调用方 | 需要增加确定性回归：等待确认时清理，然后注入迟到的肯定答复，不能重置或替换配置。当前 `NewFilmProfile` 在确认后继续修改 workspace，需验证并补足失效检查。[S6] |

D1/D2 是静态可见的缺口；D3 是由控制流推导出的风险和待测试序列，不是已经在实机复现的数据丢失事故。

### 2.3 不属于本轮的事项

QA `StopAsync` 等待已接受的 processing task；相关调用涉及页面卸载和关闭。若任务无法结束，存在等待不结束的条件性风险，当前报告也未覆盖 accepted capture/unload overlap。本轮只登记该风险，不顺手重写 QA 捕获系统。[S3][S7]

`issues-and-remediation.md` 中 VM-001 的部分“当前实现”描述落后于最近代码。允许在本轮结果中补充当前修复与未验证项，但不因说明更新就把 issue 关闭。[S4][S8]

## 三、任务目标与允许改动范围

**目标：对每一个对话框请求，都能解释谁拥有它、何时失效、谁完成等待、真实窗口如何关闭，以及迟到结果为什么无效。**

主要实现位置：

- `PRISM Utility/Views/ScanDebugPage.xaml.cs`。
- `PRISM Utility/ViewModels/ScanDebugViewModel.cs` 中对话框请求、直接调用方和终止信号的必要局部修改。
- 请求模型的现有定义文件：先定位 `ScanCalibrationPromptRequest`、`ScanFilmProfileDiscardConfirmationRequest`、`ScanNoticeRequest`；仅在传递取消/归属确有需要时扩展。
- 相应测试、可重复运行的测试脚本，以及 `implementation-notes/04R-1-dialog-lifecycle.md`。

允许一个小型、局部的对话框登记/请求生命周期辅助类型以便测试，但不得借此建立新的通用 UI 框架或改变工作台会话 owner。

禁止：拆分整个 ViewModel、抽取 Canvas/ROI ownership、改变 DI 生命周期、升级 WinUI/.NET 依赖、改协议或持久化格式、在 Page 中新增硬件调用、为了过测试删除生命周期保护、以导航离开替代全局扫描停止、顺手实现 03B/03C 或 04B。

确有跨文件必需改动时，说明与当前对话框契约的直接关系；对无关问题只登记后续项。

## 四、实现要求

### R1-01：请求属于明确的 Page 激活周期

每个 Page 展示请求需要关联当次激活标识、具体请求、具体 dialog 实例与终结状态。优先利用现有 activation epoch / Page owner，而不是维护一套与它们竞争的新状态机。

ShowAsync 前核对当前 Page 是否仍允许接收请求。失效请求按类型拒绝或结束，不弹出窗口。终止清理开始后的新请求不得先触发事件、再试图用取消等待补救。

同一窗口不能同时展示时，应采用明确的拒绝或串行策略；不得以永久挂起作为冲突处理。保留现有主/关闭按钮语义和本地化。

### R1-02：卸载先使本周期请求失效，再结束实际对话框

Page 卸载时，及时终结属于本激活周期的请求，并在 UI 线程请求关闭其实际 ContentDialog。不要把这一步放到可能长期等待的 QA drain 之后。

“取消逻辑等待”和“关闭实际对话框”都要处理。不能只在 ViewModel 加 `WaitAsync(token)`，也不能只 Hide 而没有定义请求最终结果。

旧请求的收尾必须只处理旧 dialog。Page 已重新激活时，旧 Hide、finally 或迟到的肯定结果，不能关闭、完成或覆盖新周期的 dialog。

**注意现有 dispatcher guard 的含义：** 如果先推进 epoch，再把旧 dialog 的必要清理投递到只接受当前激活的 `EnqueueForCurrentActivation`，清理可能被 guard 丢弃。清理应捕获具体旧请求/旧实例，并使用适用于“退休实例清理”的归属检查；不能通过禁用全部 epoch 保护解决。

### R1-03：请求终结具有幂等性

明确区分肯定、否定、宿主失效/取消、显示失败。一个请求只产生一个最终结果，重复卸载、Cleanup 和迟到回调都安全。

确认类请求的宿主失效不得被解释为同意。可选择安全的 false 或已被调用方处理的取消；保留现有 prompt/notice 的可观察契约，确需统一语义时说明并覆盖调用方。

对于原始 CompletionSource 及其包装等待都要交代最终状态。请求退出后解除取消注册/事件持有，不长期保留 Page、dialog 或 ViewModel 引用。

如果 UI dispatcher 不再接受任务，仍需安全终结逻辑请求并记录宿主关闭事实；不能假装已经成功执行 Hide。

### R1-04：补齐 profile confirmation 的终止取消

至少覆盖：

- `RequestFilmProfileDiscardConfirmationAsync`。
- `RequestFilmProfileImportConfirmationAsync`，包括其丢弃/替换包装方法。
- 已有 `RequestCalibrationPromptAsync` 和 `RequestNoticeAsync` 的回归。

审查所有直接调用方。对预期的生命周期取消进行正常收尾，不能让新增 OperationCanceledException 逃出未处理的 UI 命令，也不能把取消当成成功导入/保存。

不要把 terminal CTS 用成每次导航都永久取消：ScanDebugViewModel 的现有持有方式与页面可再次激活的契约必须保持。

### R1-05：失效确认不得提交 workspace 修改

在可能修改配置/候选结果的 await 边界后，验证请求、操作和所确认的上下文仍有效。验证应围绕已有归属机制实现，避免无意义地给所有方法增加第二套版本计数。

最低保证：确认等待期间发生卸载/终止后，迟到 true 不能 ResetToDefaultDraft、接受暂存导入、保存候选或发布本次成功状态。应释放所属命令的操作占用并结束 Busy 状态；旧命令不得重置新命令的状态。

用户正常确认、正常取消、无订阅者，以及无修改时直接新建等原有路径应继续可用。

### R1-06：不要改变导航与硬件操作契约

“关闭当前 Page 的确认窗口”不等于“停止整个扫描仪”。如果某工作流正在等待该确认，应按已有流程的拒绝/取消分支结束；不要在 Page 卸载中新增全局电机停止、USB lease 释放或全局会话 Dispose。

不要把 `DeactivateAsync` 和 terminal `CleanupAsync` 合并成同一行为。保持共享设备资源由现有 session manager 管理。

### R1-07：测试必须覆盖真正的状态变化

源码结构断言可以保留并更新，但新增行为不能只用 `Assert.Contains` 证明。

优先使用已有 fake/managed harness 构造“进入确认等待”的同步点，人工控制旧 completion。不要依靠长时间 Sleep 和概率性循环证明竞态安全。可以使用有上限的等待作为测试失败保护，但超时绝不是成功。

### R1-08：不以消灭所有 BLOCKED 作为本轮结束条件

本轮结束时分别报告代码修复、managed 行为证据、native dialog 验证、整体提取资格。不能将其他任务的硬件/QA门槛吸收进来，也不能通过降低标准宣布这些门槛已通过。

## 五、验收矩阵

| ID | 场景 | 必须观察的结果 | 证据类型 |
|---|---|---|---|
| R1-T01 | 无对话框订阅者 | 确认立即安全拒绝；notice 不悬挂；不生成新的持有关系 | Managed |
| R1-T02 | 未保存配置 → 新建 → 等待丢弃确认 → Cleanup | 原命令可结束，workspace 不重置，Busy/操作占用正确释放 | Managed，真实命令路径 |
| R1-T03 | T02 后手工注入旧 true | 迟到结果无效，不发布成功，不修改清理后/新上下文 | Managed |
| R1-T04 | 暂存导入的丢弃/替换确认等待中 Cleanup | 操作安全结束；迟到 true 不提交导入或丢弃新上下文 | Managed，两种语义 |
| R1-T05 | Page A 弹窗 → 卸载 → Page B/新激活弹窗 → A 迟到收尾 | A 逻辑请求终结，旧窗口关闭；B 不被旧收尾误关闭或误完成 | Managed ownership + native host |
| R1-T06 | 校准 prompt/notice 显示中卸载或终止 | 既有取消回归通过，实际 dialog 退出，无候选误发布 | Managed + native host |
| R1-T07 | ShowAsync 故障/宿主丢失/重复卸载 | 请求无永久等待；无未处理异常；后续合法激活仍可展示新 dialog | Managed +可运行的 native 子集 |
| R1-T08 | terminal 已发生后再请求确认 | 不触发新展示，不写 workspace，结果明确 | Managed |
| R1-T09 | 正常确认/否定与原工作流 | 新建/导入/校准语义未变；输入、参数与持久化不发生额外写入 | Managed 回归 |
| R1-T10 | dialog 显示中关闭窗口 | 关闭路径结束；无残留对话框等待；重复关闭不会多次终结同一请求 | Native host |

Native host 的卸载场景应通过测试宿主/程序化导航构造；不要假设用户可以点击被模态对话框遮挡的 Shell 导航。普通 Release 为首要验证对象；具备 QA 构建时再验证对应路径，但不得把“无正在执行捕获时关闭成功”写成“捕获与关闭重叠通过”。

本轮不需真实硬件。能使用 fake 的路径不以缺设备作为跳过理由；实际电机/断连/lease 效果保持另行授权验证。

## 六、构建与执行记录

先确认当前仓库脚本和项目路径。下列命令来自上一轮报告，是入口参考，不是已经运行的结果；新工作区缺少恢复产物时应先正常 restore，而不是机械使用 `--no-restore`。

```powershell
dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 -- RunConfiguration.MaxCpuCount=1

dotnet build 'PRISM Utility.Core/PrismUtility.Core.csproj' --no-restore -p:Platform=x64 -v:q

dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -p:PrismVisualQa=false -m:1 -v:q

dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -p:PrismVisualQa=true -m:1 -v:q
```

新增测试实际名称与聚焦命令在报告中列出。全套数量按本轮输出记录，不抄旧数字。保留测试失败、环境失败与未执行的区别。

若 solution 因 C++/MSBuild toolchain 不可用失败，单独记录所缺组件/实际命令；不要删除 native 项目、升级无关依赖，或把 app 单独构建通过表述为 solution 通过。

新增 native 验证脚本尽量纳入仓库并参数化 AppPath/EvidenceRoot。证据记录实际 HEAD、构建开关、场景、预期与实测结果、退出码和日志位置；不能只写执行者本机的 Temp 绝对路径。

执行环境缺 WinUI 时：完成可做的代码与 managed 测试，给出可运行的 native 验证入口，将相应项标记 NOT_RUN/ENVIRONMENT_BLOCKED，结束本轮交付。不得伪造通过，也不得自动回到“重新分析整个阶段 4”的循环。

## 七、交付与判定格式

新增 `docs/scan-debug-workbench/implementation-notes/04R-1-dialog-lifecycle.md`，包含：

1. 起止 SHA、改动文件、逐条 R1 要求映射。
2. 旧缺口、最终策略、Page/请求/VM 的责任与 UI 线程边界。
3. 每项测试的实际结果与证据，不可运行项的准确原因。
4. 保留的 owner、导航、硬件和持久化语义。
5. 本轮之外的剩余事项清单，不追加实施。

状态必须分开，例如：

```text
04A_ASSESSMENT: COMPLETE_WITH_BLOCKERS（引用既有评估，不重做）
04R1_CODE: IMPLEMENTED / PARTIAL（按实际）
04R1_MANAGED_TESTS: PASS / FAIL / ENVIRONMENT_BLOCKED
04R1_NATIVE_DIALOG_TESTS: PASS / FAIL / NOT_RUN
04B_EXTRACTION: NOT_AUTHORIZED
GLOBAL_LIFECYCLE_GATE: 保留当前判定，仅列本轮改变的证据
```

只有代码、必要行为测试和 native dialog 子集都满足时，才把本轮对话框行为记为已验证。缺 native 环境时可以结束一次代码交付，但不得写成 native 验收完成。这个区别是明确结束边界，不是降低验证要求。

允许同步当前 registry 的事实描述，如“已有 Cleanup 调用者，仍待哪些场景验证”；保留历史测试记录和 issue 状态，不将过期描述当成永远不可修复的条件，也不擅自关闭全部阻塞。

## 八、04R-1 之后的有限后续

完成本轮后停止。以下只登记，不自动执行：

- **04R-2：Windows host/fake 并发验证。** accepted QA capture 与卸载/关闭重叠、注入失败重试、活动任务导航、快速重入、重复关闭。
- **QA 等待问题的条件性修复。** 若复现 processing task 不能结束，先设计可取消/可终结的捕获及明确 teardown 契约；不要简单超时后仍释放正在使用的 Page/bitmap。不能安全终结时应保留资源并报告/中止相应关闭，不能冒充成功。
- **真实设备验证。** 独立授权，不以离线导航替代。
- **04B：一个明确提取切片。** 只有相关门槛满足且用户再次授权才启动；保留当前生产结构本身是合法结果。

## 九、可直接发送给 Codex 的启动指令

```text
请执行 docs/scan-debug-workbench/04R-1-dialog-lifecycle.md。

这是阶段 04A 后续的定界修复，不是重新评估全部阶段 4，也不授权 04B。
先读取 AGENTS.md、总说明、既有 04A 评估与修复报告，并检查当前 HEAD 和用户已有修改。

本轮只完成 Page-owned ContentDialog 的生命周期收尾、两类 film profile confirmation 的终止取消和迟到结果保护，以及对应行为测试。保留现有 Page owner/epoch、终止清理、DI、设备会话和命令契约。

先用可控 fake 构造“确认等待 → 卸载/Cleanup → 迟到 true”的失败序列，再修复。
不能只增加源码字符串断言，不能只取消等待而不处理实际 dialog，也不能让旧周期 Hide 关闭新 dialog。
不扩大为 QA/Canvas/设备会话/整个 ViewModel 重构，不自动执行 04R-2 或 04B。

运行可执行的测试和构建；原生环境不足时交付 native 验证入口并准确标记未运行，结束本轮，不继续循环补全所有全局门槛。
最终分别报告代码、managed 测试、native dialog 测试和仍然阻塞的提取资格。
```

## 十、复核来源（固定参考提交）

以下链接是便于复核的源码入口，实施时应以执行环境的当前 HEAD 重新确认。

- [S1] 提交与改动清单：<https://github.com/mr258876/PRISM-Utility/commit/2416b11>
- [S2] 既有 04A 评估：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/docs/scan-debug-workbench/implementation-notes/04A-lifecycle-and-extraction.md>
- [S3] 后续修复记录与验证边界：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/docs/scan-debug-workbench/implementation-notes/04A-lifecycle-repair.md>
- [S4] 实际应用关闭与 ViewModel cleanup：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PRISM%20Utility/App.xaml.cs>
- [S5] Page 对话框和卸载：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PRISM%20Utility/Views/ScanDebugPage.xaml.cs>
- [S6] ViewModel 请求和 profile 命令：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PRISM%20Utility/ViewModels/ScanDebugViewModel.cs>
- [S7] QA capture 的条件编译与等待：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PRISM%20Utility/PrismVisualQaCaptureService.cs>
- [S8] 当前架构问题登记：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/docs/architecture/issues-and-remediation.md>
- [S9] 已有真实 managed 取消测试：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs>
- [S10] Page 源码契约测试：<https://raw.githubusercontent.com/mr258876/PRISM-Utility/2416b11/PrismUtility.Core.Tests/ScanDebugPageReentryUi006SourceContractTests.cs>

