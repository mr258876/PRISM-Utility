# 开发待办

更新日期：2026-10-28。本文记录待开发功能、验证缺口、已完成范围与设计差异。任务完成时应更新状态，并注明实现及验证范围。

模块职责与问题编号见[架构索引](architecture/README.md)和[问题登记](architecture/issues-and-remediation.md)，产品规则见 [DESIGN](../DESIGN.md)。

## 状态口径

- **已实现并验证**：仅关闭已测试的软件范围；WinUI、原生 DLL 和扫描仪分别验收。
- **实现存在、回归未通过**：当前有代码，但相关测试失败；先查明契约、fixture 和实现的差异。
- **待验证**：实现已有，缺少当前版本相应层级的运行证据。
- **待实现或决策**：源码仍有缺口，或需要明确行为契约。可选增强不作为已确认缺陷。
- **与现状冲突**：旧描述不再准确，或产品规范和当前实现不一致。不能直接把代码改回旧要求。

## 最近测试记录

2026-10-28 在当前工作区先运行完整 x64 构建，再运行全量托管测试：

```powershell
.\tools\build.ps1
dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-build --no-restore -p:PlatformTarget=x64 --blame-hang-timeout 5min --logger 'trx;LogFileName=precommit-final.trx' --logger 'console;verbosity=quiet' --results-directory '.artifacts/tests'
```

**2147 项：2147 通过、0 失败、0 跳过，耗时 1 分 41 秒。** 构建脚本完成本机 x64 native bridge 和完整 solution 构建，退出码为 0。相关修复的筛选测试另有 767/767 通过。以上结果属于当前未提交工作区；尚未验证干净检出或 CI 环境，也未验收 WinUI 原生交互、扫描仪采集、电机运动、照明或实际 DNG 写入/重新读取。

构建日志及 TRX 保存在已忽略的 `.artifacts/` 下，仅作本地验证记录，不提交到源码仓库。

### 历史记录：2026-10-20

以下是历史全量结果，已由上面的当前验证记录取代。该次测试基于提交 `652fe37` 和当时工作区的未提交修改，不代表该提交本身的测试结果。当时的复现命令：

```powershell
dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 -p:PlatformTarget=x64 --blame-hang-timeout 5min --logger 'trx;LogFileName=managed-tests.trx' --logger 'console;verbosity=minimal' --results-directory '.artifacts/tests' -- RunConfiguration.MaxCpuCount=1
```

**当时共 2074 项：2035 通过、39 失败、0 跳过。** 当时生产页面元数据探针通过；未覆盖 WinUI 原生交互、扫描仪采集、电机运动和照明验收。

| 历史失败类别 | 数量 | 当时判断 |
| --- | ---: | --- |
| 源码结构或 XAML 布局断言 | 15 | 包含旧字段、方法片段、Grid 行及控件父子关系等期望；需要结合行为核实，不能全部视为误报。 |
| 存储 schema 期望 | 1 | `Set003FilesystemHarness_MigratesLegacyGroupsIdempotentlyAndPreservesDurableStateOnFailures` 仍期望校准 schema 1，当前写出 2。 |
| 行为断言 | 23 | 涉及配置有效性、保存、扫描交接、输入/身份投影及校准引导；必须先核查共同前置状态，不能以更新字符串断言代替修复。 |

历史失败分类描述的是触发断言，不能直接当成根因。相关前置、契约和实现修复及当前回归结果见 T01。

## 优先处理

| ID | 状态 | 工作与验收标准 |
| --- | --- | --- |
| T01 | 已恢复回归 | 2026-10-28 全量 2147/2147 通过，0 失败、0 跳过。保存/检查/扫描交接用例显式建立有效设备映射，缺失角色仍阻止保存；源码/布局断言按当前 partial 文件和控件职责维护，保留生命周期与所有权约束；校准 schema 2 迁移断言、白光兼容回归和测试资源初始化均已验证。 |
| T02 | 已完成历史材料依赖清理 | 测试已解除对未入库运行记录、已退休截图脚本及待办措辞的依赖。生产源码、DI、所有权和功能断言保留，当前全量运行不再出现历史文件缺失错误。干净检出与构建环境验证继续见 T03。 |
| T03 / DNG-002、TEST-001 | 本机构建已通过，干净检出与 CI 待验证 | 2026-10-28 [构建脚本](../tools/build.ps1) 已探测 MSBuild/v143/SDK，完成本机 x64 native bridge 和完整 solution 构建，退出码 0；生产页面元数据探针也通过。仍需验证干净检出和 CI 的依赖安装与完整构建；继续分别报告依赖缺失、文件占用和构建错误。构建通过不关闭 T07 的原生运行验收。 |

T01 的本轮处理：

- [配置/校准行为测试](../PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs)、[扫描交接测试](../PrismUtility.Core.Tests/ScanDebugSavedHandoffTests.cs)及检查/草稿用例使用显式的 IR/Blue/White/Red 设备映射，恢复有效配置的保存、身份、行距和交接前置。[full-v6 fixture](../PrismUtility.Core.Tests/Fixtures/FilmProfile/full-v6.json)及默认设备映射保持原样；新增负例确认默认设备缺少 IR 时保留所选角色和草稿、拒绝保存且不导出文件。已有[设备映射测试](../PrismUtility.Core.Tests/ScanDeviceSettingsProjectionTests.cs)继续覆盖未保存输入、缺失角色与恢复映射。
- 物理 LED 编号跟随当前设备映射，胶片保留所选角色；旧身份测试已更新并保留选择、通知、不写库和不导出的约束，见 X11。
- [胶片源码契约](../PrismUtility.Core.Tests/FilmProfileSourceContractTests.cs)、[ViewModel 编排契约](../PrismUtility.Core.Tests/ScanDebugFilmProfileOrchestrationSourceTests.cs)及生命周期/布局契约按当前文件和结构维护。SET-005 的颜色检查限定为硬编码颜色，不再误中合法的 `Glyph="&#xE8FE;"`，主题约束保留。[存储迁移 harness](../PrismUtility.Core.Tests/Set003LocalSettingsMigrationHarnessTests.cs)已按当前校准 schema 2 验证，旧数据迁移、幂等性与失败时持久状态断言保留。
- `ScanDebugCalibrationStatusTests` 自行初始化 `PRISM.Utility.UseResourceFallbacks`，消除依赖其他测试先设置开关的顺序问题；单独筛选时也不再反复创建失败的 ResourceLoader。
- [白光持久化测试](../PrismUtility.Core.Tests/WhiteChannelPersistenceTests.cs)新增 v5/v6 旧文件回归：旧白光没有显式启用模式时，迁移保留原有自动 Steady 照明语义；现代 schema 7 文件显式保存的 Off 仍保持 Off，不会被迁移规则重新点亮。

上述修复已通过当前全量测试，不依靠跳过失败用例关闭 T01。WinUI、设备及实际 DNG 运行仍分别按 T04–T07 验收。

## 开发与验证

| ID / 关联问题 | 状态 | 剩余工作及完成标准 | 当前依据 |
| --- | --- | --- | --- |
| T04 / VM-001、UI-006、APP-002 | 实现已有，原生运行待验证 | 使用可控 WinUI host 复核 dialog 显示中卸载/关闭、A 页迟到收尾不影响 B 页、Show/dispatcher 故障、快速重入、重复关闭；另覆盖 accepted QA capture 与卸载/关闭重叠、注入故障后重试和有界终结。保留实际 owner、epoch 和 terminal cleanup 契约。 | [Dialog lifetime](../PRISM%20Utility/Views/ScanDebugDialogLifetime.cs)、[Page](../PRISM%20Utility/Views/ScanDebugPage.xaml.cs)、[App](../PRISM%20Utility/App.xaml.cs)、[QA service](../PRISM%20Utility/PrismVisualQaCaptureService.cs) |
| T05 / TEST-002、LOG-004 | 待当前版本原生验收 | 四任务、属性/工程/问题列表、冷启动首次 Focus、键盘/焦点返回、Canvas/ROI/pointer 释放、文件选择取消/成功、语言切换时状态保留。补高对比度与 100%/150%/200% 显示缩放、较大文字和可达到的宽窗口矩阵；已有历史中英文、Dark/Light 和部分尺寸的观察记录。 | [QA runner](../tools/run-workbench-visual-qa.py)、[变更记录](scan-debug-workbench/implementation-notes/20261014-ui-continuity.md) |
| T06 / USB-001、CAL-001、生命周期硬件边界 | 待设备验证 | 实际扫描/停止、断连重连/换设备、CTS 取消、USB lease 释放、电机停止与照明恢复；验证手动参考值 Apply/Revert/Clear/采样填入、候选审查、参数应用与真实读回、多通道来源及专用 LED / RGB 合成白光。软件 fake 通过不能关闭这些验收。 | [会话管理](../PRISM%20Utility.Core/Services/ScannerDeviceSessionManager.cs)、[工作流](../PRISM%20Utility.Core/Services/ScanWorkflowService.cs)、[白光](../PRISM%20Utility/ViewModels/ScanDebugViewModel.WhiteChannel.cs) |
| T07 / DNG-001、DNG-003 的负路径 | 待原生验证与边界决策 | 对直接 native 调用补最小 packed stride、blackLevelPlaneCount 与 samplesPerPixel 一致性检查或明确契约；验证实际 ABI、DNG 写入/重新读取，以及 JXL 不支持时的真实负路径。托管 36 项通过不能代替 C++/SDK 行为。 | [原生 writer](../DngSdkWarpper/PrismDngWriter.cpp)、[托管 writer](../PRISM%20Utility.Core/Services/DngWriterService.cs)、[JXL stubs](../DngSdkWarpper/PrismDngJxlStubs.cpp) |
| T08 / APP-004、LOG-003 | 部分处理，仍有缺口 | 定义正常终止期间可丢弃的 UI 投递与异常 dispatcher 拒绝；为 Scan、Log、系统主题回调补 `TryEnqueue=false` 的诊断/收尾测试。Language/Settings 已有处理，不重复实现。 | [Scan VM](../PRISM%20Utility/ViewModels/ScanViewModel.cs)、[Log VM](../PRISM%20Utility/ViewModels/LogViewModel.cs)、[MainWindow](../PRISM%20Utility/MainWindow.xaml.cs) |
| T09 / UI-004 | 待修复及验证 | Header behavior 的静态 `_current` 与实例 owner 仍需收敛；覆盖 attach/detach、Shell 替换及 detach 后属性变化。 | [Header behavior](../PRISM%20Utility/Behaviors/NavigationViewHeaderBehavior.cs) |
| T10 / UI-005、LOG-006、USB-002、APP-002 诊断 | 待明确诊断策略 | unset route 已有可断言诊断；剩余是导航、关闭失败、USB catalog 刷新失败哪些应进入持久日志/用户提示。保留高频 timing 可仅 Debug 的选择，补失败原因可追溯测试。 | [Navigation logger](../PRISM%20Utility/Helpers/NavigationTimingLogger.cs)、[USB catalog](../PRISM%20Utility.Core/Services/UsbDeviceCatalog.cs)、[App](../PRISM%20Utility/App.xaml.cs) |
| T11 / SCAN-003、SET-007 | 待实现或明确异常传播契约 | 普通 status/progress/diagnostic 回调与 workspace `SnapshotChanged` 仍有直接调用。确定隔离策略并测试抛错订阅者；不能回滚已经发布的状态或掩盖主失败。cleanup diagnostic 已安全隔离，见 C05。 | [Execution runner](../PRISM%20Utility.Core/Services/ScanExecutionRunner.cs)、[Workspace](../PRISM%20Utility.Core/Services/ScanFilmProfileWorkspace.cs) |
| T12 / SESSION-002 | 部分实现，需明确快照 | 显式断开先清 `_session`，若后续 cleanup 抛错，可能跳过 disconnected snapshot。覆盖 teardown 失败后真实状态、主异常保留、lease 释放及重新连接。 | [Session manager](../PRISM%20Utility.Core/Services/ScannerDeviceSessionManager.cs) |
| T13 / USB-003、SCAN-004 | 待验证与诊断补足 | 单次同步 `_reader.Read` 不能仅靠进入前检查 token 实现即时取消；验证 timeout 上界与真实驱动表现。ACK 未知异常当前返回 false，应保留原因并区分取消、超时与 I/O/协议失败。 | [Bulk session](../PRISM%20Utility.Core/Services/UsbBulkDuplexSession.cs)、[ACK channel](../PRISM%20Utility.Core/Services/ScanAckChannel.cs) |
| T14 / APP-003 | 测试缺口 | 无设备动作地解析当前 pages、ViewModels 与关键 services，增加完整应用容器组合验证。注册模块拆分是可选重构，不是本项完成前提。 | [App 注册](../PRISM%20Utility/App.xaml.cs) |
| T15 / SET-004、SET-006 | 通用策略待决策；迁移用例已通过 | 通用 LocalSettings 字典仍没有统一 envelope，评估剩余 keys 是否需要版本迁移。胶片 v5/v6/v7、校准 v1/v2 与 legacy/future 路径已有实现和用例；校准 schema 2 的 filesystem harness 及旧白光兼容回归已通过，继续维护版本与兼容行为。 | [Local settings](../PRISM%20Utility/Services/LocalSettingsService.cs)、[校准 repository](../PRISM%20Utility.Core/Services/ScanCalibrationProfileRepository.cs)、[白光持久化测试](../PrismUtility.Core.Tests/WhiteChannelPersistenceTests.cs) |
| T16 / CAL-003 | 待明确边界及补测试 | `TryApplyReferenceLevels` 有 expected snapshot 保护，但 `SetCurrentDraft` 仍不接受 expected snapshot；审计其他写入交错并加回归。对有导出补丁但无选中通道的调用，验证 `PatchIncluded=false` 和提示契约。尚无据此确认的数据丢失。 | [Workspace](../PRISM%20Utility.Core/Services/ScanFilmProfileWorkspace.cs)、[Workspace tests](../PrismUtility.Core.Tests/ScanFilmProfileWorkspaceTests.cs) |
| T17 / IMG-002、IMG-003 | 待测量 | 代表性分辨率下测普通/瀑布预览、RAW 连续更新与快速 ROI、对齐/色彩转换的延迟、分配和峰值内存。冻结帧单份 64 MiB 限制不等于“实时帧 + 冻结 + 基线 + 统计”的总预算；补失败/超限保持旧状态的验证，再选择优化。 | [Decoder](../PRISM%20Utility.Core/Services/ScanImageDecoder.cs)、[Preview](../PRISM%20Utility/Services/ScanPreviewPresenter.cs)、[Snapshot](../PRISM%20Utility.Core/Models/ScanObservationSnapshot.cs) |
| T18 / SESSION-003 | 狭窄测试缺口 | 外部 RawUsb lease 阻塞规则已实现；在已有 ScanWorkflow 用例之外补 ScanDebug 模式变体，继续区分“功能不可用”和“设备被占用”。 | [Access coordinator](../PRISM%20Utility.Core/Services/ScannerAccessCoordinator.cs)、[Access tests](../PrismUtility.Core.Tests/ScannerAccessCoordinatorTests.cs) |
| T19 / UI-007、VM-002 | 重构待决策 | Page/VM 已拆成多个 partial 文件，职责仍属于相同对象，不算 presenter/workspace 所有权提取完成。先完成 T04/T05 的运行证据，再选择单一职责的小范围提取，并验证资源所有权和生命周期。 | [Page](../PRISM%20Utility/Views/ScanDebugPage.xaml.cs)、[ViewModel](../PRISM%20Utility/ViewModels/ScanDebugViewModel.cs) |

## 可选增强

| ID / 范围 | 当前实现 | 尚需决定或实施 |
| --- | --- | --- |
| F01 / 基线比较 | 已有冻结、恢复、单槽 A、切换/清除、独立显示副本和已计算统计。 | A/当前同域数值差、曝光/增益/照明差异列表、不可比条件及重新计算策略尚未完成。当前保存的是显示与统计快照，不是完整可重算 RAW 数据集。若保留该需求，先明确语义和内存预算。 |
| F02 / 对焦轨迹 | 自动对焦算法与状态/图像回调已存在。 | 服务尚无结构化探测序列进度模型，UI 无实测完整轨迹。若实施，应暴露真实采样点、最佳点和终止原因。依据：[接口](../PRISM%20Utility.Core/Contracts/Services/IScanAutoFocusService.cs)、[实现](../PRISM%20Utility.Core/Services/ScanAutoFocusService.cs)。 |
| F03 / CAL-002 | Brenner 指标目前刻意保持未归一化，已有用例锁定。 | 亮度归一化是算法变更决策，先比较 fixtures 与设备结果；不能仅因方法名含 `Normalized` 就直接改公式。 |
| F04 / DNG-003、采集溯源增强 | JXL 明确不支持；采集来源只填已知提交值，可选的设备身份/读回字段为空时 UI 明确显示非读回。 | 启用 JXL、补完整 per-pass 设备身份和真实读回属于独立需求，需要先确认数据来源与设备交互成本。依据：[Capture provenance](../PRISM%20Utility.Core/Models/ScanWorkflowModels.cs)、[Workflow](../PRISM%20Utility.Core/Services/ScanWorkflowService.cs)。 |

## 已实现的软件范围

以下结果来自 2026-10-28 的 2147 项全量测试，各过滤范围可能重叠。

| ID | 已确认完成的范围 | 实现与验证 | 剩余边界 |
| --- | --- | --- | --- |
| C01 / RAW 分析 | 原始样本统计、保峰聚合、单 worker/latest-pending、旧 epoch/pass/ROI 结果拒绝 | [RAW analyzer](../PRISM%20Utility.Core/Services/ScanRawSignalAnalyzer.cs)、[Worker](../PRISM%20Utility/ViewModels/ScanDebugViewModel.RawSignal.Worker.cs)；`ScanRawSignalAnalyzerTests` 21/21。 | 原生连续刷新性能、主题与设备验证。 |
| C02 / 冻结与基线 | 冻结显示、单槽 A、深拷贝与单帧上限 | [Observation](../PRISM%20Utility/ViewModels/ScanDebugViewModel.Observation.cs)、[Snapshot](../PRISM%20Utility.Core/Models/ScanObservationSnapshot.cs)；`ScanObservationSnapshotTests` 9/9。 | F01 的完整同域比较。 |
| C03 / VM-001、UI-006 软件修复 | ScanPage Loaded/Unloaded guard、实际 VM terminal cleanup、重入 owner/epoch 保护 | [ScanPage](../PRISM%20Utility/Views/ScanPage.xaml.cs)、[App](../PRISM%20Utility/App.xaml.cs)；`ScanPageLifecycleVm001CharacterizationTests` 7/7、`AppCloseLifecycleSourceContractTests` 9/9、`ScanDebugPageReentryUi006SourceContractTests` 14/14。 | T04 原生竞态和 T19 架构提取仍未关闭。 |
| C04 / 对话框生命周期 | Dialog owner/lease retirement、旧确认与迟到 Hide 隔离 | [Dialog lifetime](../PRISM%20Utility/Views/ScanDebugDialogLifetime.cs)；`ScanDebugDialogLifetimeTests` 8/8。 | 原生模态导航与关闭。 |
| C05 / SCAN-005 | cleanup diagnostic 订阅者抛错不会遮蔽 cleanup/主失败 | [Workflow](../PRISM%20Utility.Core/Services/ScanWorkflowService.cs) 的 `ReportCleanupDiagnostic`；`ScanWorkflowServiceTests` 37/37。 | 普通回调异常策略仍见 T11。 |
| C06 / 参考值与 ROI | 手动参考值显式应用与输入保护、采集来源和编辑状态分离、Focus 与 ADC ROI 独立 | [Calibration tests](../PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs) 中 `ManualReference_` 26/26、`CaptureEvidence_` 6/6、`R01_` 20/20；完整配置保存、交接与身份行为回归已随 T01 恢复。 | T04–T06 的原生交互、实际参数读回与设备校准仍待验证。 |
| C07 / 格式和白光持久化 | 独立 `.prismdraft` 格式读写、白光双来源持久化及校准文档迁移 | `ScanFilmProfileDraftFileServiceTests` 30/30、`WhiteChannelPersistenceTests` 15/15、`Set003CalibrationProfileRepositoryTests` 6/6、filesystem migration harness 1/1；覆盖旧白光自动 Steady 与现代显式 Off 的区别。 | 原生文件选择交互及真实照明行为仍见 T05/T06；托管保存链与迁移回归已通过。 |
| C08 / DNG-001 托管子范围 | 请求校验、托管 ABI 布局与 native 状态映射 seam | [DNG tests](../PrismUtility.Core.Tests/DngWriterServiceValidationTests.cs)等 `DngWriterService*` 用例 36/36。 | T07 原生直调、SDK 文件与 JXL 负路径仍待验证。 |

四任务/运行栏、工作流引导、文件审阅/反馈和“保存并用于扫描”的相关托管与源码契约回归已通过；这一软件范围为**已实现并验证**。原生 UI 交互和布局验收仍由 T05 跟踪。

已关闭问题的回归与新增验证缺口分别进入 T01–T07。LOG-005 的主题启动职责、SCAN-009 的公开协议边界属于架构约束。

## 兼容性与设计差异

| ID | 范围 | 2026-10-28 状态与处理 |
| --- | --- | --- |
| X01 | 观察比较与对焦轨迹 | 冻结/A 基线已实现，完整比较仍见 F01；结构化对焦轨迹仍见 F02，现有状态文本尚不提供完整采样序列。 |
| X02 | 页面清理与重入 | C03/C04 已包含 Loaded guard、实际 VM 清理、owner/epoch 和对话框卸载保护。剩余 T04 原生验证与 T19 重构决策。 |
| X03 | 存储格式版本 | 胶片当前写 schema 7、读取 v5/v6/v7；校准设置写 schema 2、兼容 v1；编辑草稿使用独立 v1 格式。迁移和测试期望应分别维护。依据：[文档服务](../PRISM%20Utility.Core/Services/ScanFilmProfileDocumentService.cs)、[校准 repository](../PRISM%20Utility.Core/Services/ScanCalibrationProfileRepository.cs)、[草稿服务](../PRISM%20Utility.Core/Services/ScanFilmProfileDraftFileService.cs)。 |
| X04 | 白光来源 | 当前有 DedicatedLed / RgbComposite 双来源，校验、持久化与 UI 投影均区分两种模式。v5/v6 迁移保留旧白光的自动 Steady 语义，schema 7 显式 Off 保持关闭，回归已通过。依据：[WhiteChannelSettings](../PRISM%20Utility.Core/Models/WhiteChannelSettings.cs)、[持久化测试](../PrismUtility.Core.Tests/WhiteChannelPersistenceTests.cs)。 |
| X05 | 检查列表停靠门槛 | 当前 `ReviewDockThreshold` 为 720 DIP，较窄全宽，否则给审阅区 320+12 DIP；早期记录中的 1280 DIP 不再是当前门槛。依据：[布局计算](../PRISM%20Utility.Core/Models/ScanWorkbenchPreviewLayout.cs)、[边界测试](../PrismUtility.Core.Tests/ScanWorkbenchPreviewLayoutTests.cs)。 |
| X06 | DESIGN 的空预览参数区为 45%、最小 360 DIP；无图属性/工程默认占满。 | 当前布局常量为 38%、最小 300 DIP，预览最小 480 DIP；部分详情保留预览。**产品规范与实现尚未统一**：需决定更新规范或调整实现，并同步测试。依据：[DESIGN](../DESIGN.md)、[布局计算](../PRISM%20Utility.Core/Models/ScanWorkbenchPreviewLayout.cs)。 |
| X07 | 设计要求返回任务保留各自滚动位置。 | 当前黑白场任务/详情切换重置至顶部，并有源码契约。需要明确哪些路由应记忆滚动，哪些应复位。依据：[工作台设计](scan-debug-workbench/workbench-design.md)、[Page](../PRISM%20Utility/Views/ScanDebugPage.xaml.cs)、[布局契约](../PrismUtility.Core.Tests/ScanDebugTaskRailLayoutSourceTests.cs)。 |
| X08 | 测试与环境状态 | 当前全量 2147/2147 通过，0 失败、0 跳过；本机 x64 native/solution 构建退出码 0。Status 测试自行初始化资源 fallback，独立筛选通过，不再依赖其他测试的执行顺序。干净检出与 CI 仍见 T03，原生 UI/设备/DNG 运行仍见 T04–T07。 |
| X09 | UI 验收材料 | 开发截图和运行日志不入库。UI 检查应约束可见入口、数据语义和行为；控件名称或容器结构变化时，测试仍需验证原有功能意图。 |
| X10 | 回调、迁移与职责拆分 | SCAN-005 的 cleanup observer 隔离已由 C05 关闭；校准 schema 2 的旧 harness 期望已更新且迁移回归通过；partial 文件拆分只改变文件组织，职责提取仍为 T19。普通回调异常边界仍见 T11。 |
| X11 | 物理 LED 映射归属 | 物理 LED 映射由设备设置拥有，胶片保留所选角色。`BwIdentity_WhenDeviceMappingChanges_UpdatesBoundLedAndPreservesSelectedRole` 已验证 Red 从 LED1 映射到 LED3 时选择不变、发出绑定通知且不写库或导出；缺失角色拒绝保存、未保存输入和映射恢复也保留回归。依据：[身份与保存行为](../PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs)、[设备映射契约](../PrismUtility.Core.Tests/ScanDeviceSettingsProjectionTests.cs)。 |

X06/X07 需要设计决定。其余条目用于维护版本兼容性、测试预期和验证边界。
