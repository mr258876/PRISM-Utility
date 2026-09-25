# PRISM ScanDebug：WinUI 3 工作台改造总说明

> 面向 Codex 的实施任务书。目标仓库：`mr258876/PRISM-Utility`，目标工作基线：`dev`。
> 编写日期：2026-09-24。状态：**待实施**，不是完成报告。
> 默认仅执行阶段 01。读取本文件不代表获得阶段 02～04 的执行授权。

## 1. 目标与交付边界

将 ScanDebug 从分区较多的配置／调试页面，重组为以采样、观察、校准为中心的原生 WinUI 3 工作台。保留已有硬件操作、校准、配置导入导出、预览与工程工具，不重写扫描框架。

最终布局强调：顶部任务控制始终可达；左侧调整当前任务；中央观察图像或原始信号；右侧查看统计与数据来源；配置编辑与工程工具仍有清楚入口。

**这里定义的是产品要求，不是要求逐像素复刻网页。** 此任务书可以独立使用，不依赖聊天历史、此前 HTML 原型或截图。不得因为拿不到原型就自行补造模拟界面作为交付。

### 必须保持

- 使用现有 WinUI 3 应用、原生控件、主题资源和 Win2D 路径；保留 Shell、导航与标题栏。
- 复用现有命令、Core 服务、会话协调和设备访问仲裁；UI 不直接发送 USB／电机／照明命令。
- 保留原有单次、连续、输片／多通道等实际支持的采集方式，以及加载、导出、校准候选审查和 ROI 功能。
- 支持中文与英文、浅色与深色、高对比度、键盘操作及有效像素下的自适应布局。
- 文件保存、通道档案更新、设备参数应用、采集数据快照使用不同语义。
- 无硬件、无采样或缺少数据来源时明确显示未知／未采样，不能造数字或成功状态。

### 非目标

本任务不引入 WebView2、WebUSB、Electron、新前端工程、浏览器运行时、全局主题重做、第三方图表库或新的设备通信协议；不升级 SDK／NuGet／目标框架；不改固件；不改扫描、自动校准、对焦、DNG 的核心算法或输出格式。确需超出边界时，先提交具体阻塞证据与独立任务建议，不夹带实施。

## 2. 文档结构与执行顺序

| 文件 | 作用 | 执行条件 |
| --- | --- | --- |
| [01-native-workbench.md](01-native-workbench.md) | 原生布局、命令入口、响应式、主题、本地化和功能迁移 | 默认实施范围 |
| [02-state-and-manual-levels.md](02-state-and-manual-levels.md) | 状态可信度、采集来源、手动黑白参考值及并发／保存边界 | 01 已审查；用户明确指定 02 |
| [03-signal-inspection.md](03-signal-inspection.md) | RAW 线剖面／统计、冻结、基线、对焦过程数据 | 02 的数据来源契约可用；按子阶段执行 |
| [04-lifecycle-and-extraction.md](04-lifecycle-and-extraction.md) | 生命周期证据、条件性组件拆分 | **可选且默认不执行**；必须先满足拆分门槛 |

使用独立变更集推进：不要把四个阶段合并成一次大改。阶段 01 完成即可形成可用的第一版，不依赖阶段 03 的新图表。阶段 04 不是界面改造交付的前置条件。

本文件是共同约束的唯一来源；阶段文件补充局部要求。发生冲突时：仓库内适用的 `AGENTS.md`／用户当前指令优先；在这些约束下，本文件的硬件安全、数据语义和生命周期限制优先于阶段的外观便利。

## 3. 开始前必须核对的仓库事实

本任务书通过公开 `dev` 文件核对入口，但**未固定到一个已验证的 commit SHA，也未在 Windows 上构建或运行该仓库**。不得把文档中的历史测试数或此前聊天结论当作当前通过证据。

开始实施时必须：

1. 读取适用的 `AGENTS.md`，记录 `git status --short`、`git branch --show-current` 和 `git rev-parse HEAD`。保留用户已有修改，不 reset／clean，不擅自切换分支或推送。
2. 用 `git ls-files` 定位真实文件。部分架构文档含旧的 `Host Software/` 前缀；以当前文件树为准，不创建重复目录来迎合文档。
3. 确认目标功能是否已存在。已经满足的条目验证后标记“已存在”，不要重复实现；分支已有新修复时保持新行为。
4. 读取相关实现与测试，先做一份“旧入口 → 新入口 → 命令 → 状态／数据源”的迁移清单，再进行当前阶段的代码修改。预检不是仅输出计划后结束的理由。
5. 确认 `.sln`、`.csproj`、现有脚本和工具链；测试命令以当前仓库为准，不根据本任务书猜测项目名或目标框架。

### 关键入口

| 用途 | 已核对的入口／查找方式 |
| --- | --- |
| 页面布局 | `PRISM Utility/Views/ScanDebugPage.xaml` |
| 页面交互及生命周期 | `PRISM Utility/Views/ScanDebugPage.xaml.cs` |
| 调试状态与命令 | `PRISM Utility/ViewModels/ScanDebugViewModel.cs` 及其当前 partial 文件 |
| 布局计算 | 搜索 `ScanWorkbenchPreviewLayout` 及调用者，路径以当前文件树为准 |
| 原始数据读取 | `PRISM Utility.Core/Services/ScanImageDecoder.cs`；搜索 `TryGetSample16` |
| 预览呈现 | 搜索 `ScanPreviewPresenter`、`ScanPreviewFrame`、`PreviewFrame` |
| 胶片工作区 | 搜索 `IScanFilmProfileWorkspace`、`IScanFilmProfileFileCoordinator` |
| 通道黑白参考值 | 搜索 `SaveColumnSampleAsBlackLevel`、`SaveColumnSampleAsWhiteLevel`、`SaveCalibrationLevelsAsync`、`ScanChannelCalibrationProfile` |
| 设备命令与互斥 | 搜索 `IScanDebugSessionCoordinator`、`ScanDebugRuntimeOperationClaims`、`CanExecuteRuntimeCommand` |
| 本地化与主题 | 定位现有 `Strings/**/Resources.resw`、主题字典；沿用原有结构 |
| 测试 | 定位当前测试项目，查找 ScanDebug、FilmProfile、ROI、Lifecycle、ImageDecoder 相关测试 |

推荐先读架构文档：`ui-mvvm.md`、`issues-and-remediation.md`、`viewmodel-lifecycle.md`、`testing-and-build.md`。阶段 02 再读 workspace／session／calibration，阶段 03 再读 image decoding／preview。不要为阶段 01 扫描和重构整个仓库。

### 已核对但仍需在工作基线复查的事项

- 页面已有主题卡片、分区选择与窄版选择入口；不是从零建设响应式。[S1]
- ViewModel 已有 start／stop／apply 和黑白列采样保存路径；不是从零建设硬件命令或通道档案。[S2]
- 架构文档仍记录 `VM-001`、`UI-006`、`UI-007`、`VM-002` 的拆分阻塞链。不能仅因布局好看就假设允许提取 Page／ViewModel 生命周期所有权。[S3]
- 原始样本与显示图像不同；现有预览存在缓冲区复用。后续测量不能从显示像素倒算，冻结不能仅保留一个仍会被修改的数组引用。[S4]

以上为入口说明，不是授权改写这些模块。

## 4. 最终信息架构

```text
沿用现有应用 Shell / NavigationView / 标题栏

当前胶片配置 · 文件保存状态                    保存配置   配置操作 ▾

当前任务／阶段／进度     采样模式  开始采样  停止采集  全部电机停止
必要的警告或错误摘要（普通状态不弹出大型 InfoBar）

┌ 左：任务参数 ──────┬ 中：观察区 ──────────────┬ 右：检查区 ─────┐
│ 采样 / 黑白场      │ 原有图像与瀑布显示        │ 当前 ROI        │
│ 对焦 / 运动       │ 原有缩放、ROI、像素检查    │ 数据来源        │
│                 │ 后续：线剖面、对焦曲线     │ 后续：统计、基线 │
│ 独立滚动          │ 优先分配剩余宽高          │ 可收起          │
└─────────────────┴─────────────────────────┴────────────────┘

当前操作摘要                                      查看事件记录
```

“配置操作”仍提供基本信息、采集计划、通道档案、导入／审查／验证等入口。设备级设置、底层寄存器等进入高级区域，但不得因重分类删除。

阶段 01 的检查区只显示已存在、可信的数据；没有测量结果时不放示意直方图、假均值或无效按钮。

### 全局交互不变量

- 切换任务分区只是换视图，不得隐式启动／取消扫描、保存文件或重新连接设备。
- 保留 `StopScanCommand` 的现有语义时，文案为“停止采集”，不能改名为可以取消所有任务的“停止当前任务”。
- 全电机停止入口始终存在，但其启用条件仍服从真实命令契约；不等同于物理急停，不声称一定关灯。
- 文件保存成功不代表设备应用成功；参数写入成功不代表采集已重新执行；冻结显示不代表采集已停止。
- 参数应用范围可以不同：通道参数、设备全局时钟、照明、运动映射分别按原有命令处理，不增加一个无条件“全部应用”。
- 不默认第四路为 IR，不固定 ADC1 对应偶列，不将估算位移标为实测位置；映射和有效性来自设备设置／校准事实。
- 灰掉的主要操作必须有可见禁用原因；错误／停止等关键信息不能只依赖颜色。

### 硬件命名与单位约束

当前公开 PRISM102 说明将 LED level 描述为二值开关，同步照明由本地 PIO 定时脉冲实现。[S13] 因此界面不能把非零 level 包装成连续亮度百分比。正式实施仍须核对用户工作基线和实际固件，不把此说明变成不可变的未来硬件假设。

曝光／行时序、LED 脉宽、运动步进间隔使用各自真实单位和时钟域；复用仓库时序换算，不硬编码 125 MHz 等原型示例，不用主控时钟替代外设 PIO 时钟。缺少时钟依据时保留原始单位并解释未知。现有输入已经使用 μs／MHz 时保持它们，不为复刻原型反改为仅显示 ticks。

## 5. WinUI 风格约束

优先原生 `Grid`、`SelectorBar`、`ComboBox`、`Button`、`DropDownButton`、`MenuFlyout`、`CommandBar`、`Expander`、`InfoBar`。控件是否可用以当前依赖为准，不为获得某个控件升级 SDK。

沿用主题资源和控件默认状态，不写一整套网页皮肤。开始操作可用强调按钮，停止采用语义化警示资源但保留焦点／禁用状态。普通说明使用次级文字资源，不能硬编码深色背景与固定白字。Mica 沿用 Shell 现状，不要求修改窗口材质。

任务选择条不能靠裁剪适配；空间不足时换单个选择控件。预览工具允许进入“更多”；停止入口不得进入自动溢出菜单。以上控件行为参考微软资料。[S6][S7]

改变绑定时检查模式与作用域；提取控件会改变 `x:Bind` 查找根，动态显示不能误用一次性绑定。主题／高对比度以当前资源系统处理。[S8][S9]

## 6. 通用验证与证据规则

在修改前记录相关基线结果。优先运行既有聚焦测试与受影响模块构建；工具链可用时完成应用构建和相关回归。不可删除、跳过或弱化现有测试来使结果变绿；布局契约确需更新时，保留其行为约束并说明原因。

证据状态只使用：

| 状态 | 含义 |
| --- | --- |
| PASS | 实际运行且通过；附命令／环境／结果 |
| FAIL | 实际运行但失败；区分已有失败与新回归 |
| NOT_RUN | 未执行；说明原因 |
| ENVIRONMENT_BLOCKED | 尝试或检测后确认缺 Windows、工具链、SDK 等环境条件 |
| NEEDS_DEVICE_VALIDATION | 需要真实设备验证，当前证据不足 |

源代码字符串测试、Core 单测、WinUI 应用构建、WinUI 运行检查、实机操作是不同层级。一个通过不能替代另一个。仓库现有构建文档也区分原生依赖与硬件验证边界。[S5]

没有 Windows 时仍应完成可安全实施的代码和可运行测试，并准确列出剩余验证；不伪造 WinUI 截图或宣称视觉验收通过。没有真实设备授权时，不发送真实运动／照明命令来“自动测试”。

### 每个变更集的报告

沿用仓库已有任务报告机制；若没有，在本目录 `implementation-notes/` 下创建当前阶段报告。至少包含：

- 基线 commit、阶段／子阶段、改动文件、已有能力与新增能力。
- 要求 ID → 实现位置 → 测试／手工证据的映射。
- 实际执行命令与结果；没有执行的检查和具体原因。
- 是否改变了命令、持久化、会话、公开绑定或生命周期；未改变也应说明。
- 遗留阻塞与最小下一步；不自动开始下一阶段。

不得自动更新本任务书中的“待实施”来假装验收完成；可添加报告链接。未经用户要求，不提交远端、不合并、不改历史。

## 7. 交给 Codex 的启动指令

把这五份文件放入仓库 `docs/scan-debug-workbench/`；目录名称是建议，不覆盖已有 `AGENTS.md` 或项目说明。

### 第一次：只执行阶段 01

```text
请实现 PRISM ScanDebug 的第一阶段 WinUI 3 工作台改造。

先读取当前仓库适用的 AGENTS.md，再读取：
- docs/scan-debug-workbench/README.md
- docs/scan-debug-workbench/01-native-workbench.md

只执行阶段 01。保留原有功能、设备命令和生命周期所有权，
不要实现阶段 02～04，不要引入 WebView2 或升级依赖。
先核对实际源码、现有修改和测试，再完成代码修改及可运行验证，
不要只输出计划。按要求 ID 报告实现、证据及未验证项。
缺少 Windows 或硬件时如实分类，但继续完成可安全实施的部分。
```

### 后续阶段通用指令

```text
读取 docs/scan-debug-workbench/README.md、上一阶段实施报告，以及我本次指定的阶段文件。
只实现本次指定范围；复查前置条件，不重复已完成能力，不自动进入后续阶段。
保留现有用户修改，完成代码、聚焦回归和阶段报告。
未满足前置条件的部分给出具体证据；仍完成不受其影响的当前范围工作。
```

阶段 03 应明确指定 `03A`、`03B` 或 `03C`。阶段 04 首次只指定 `04A` 生命周期评估；不能把“可选提取”理解为默认同意架构重写。

## 8. 可追溯来源

来源核对日期：2026-09-24。源码与架构文档可能随分支变化；工作基线源码、当前测试和用户指令优先。以下链接用于查找实现和官方 API，不要求照抄来源的全部内容。

- [S1：ScanDebugPage.xaml](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/PRISM%20Utility/Views/ScanDebugPage.xaml)
- [S2：ScanDebugViewModel.cs](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/PRISM%20Utility/ViewModels/ScanDebugViewModel.cs)
- [S3：UI 与 MVVM 架构](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/ui-mvvm.md)，以及 [问题登记](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/issues-and-remediation.md)
- [S4：图像解码与预览](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/image-decoding-preview.md)
- [S5：测试和构建](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/testing-and-build.md)
- [S6：Microsoft SelectorBar](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/selector-bar)
- [S7：Microsoft CommandBar](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/command-bar)
- [S8：Microsoft x:Bind](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/x-bind-markup-extension)
- [S9：Microsoft XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources)
- [S10：ScanDebugPage.xaml.cs](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/PRISM%20Utility/Views/ScanDebugPage.xaml.cs)
- [S11：校准、对焦与档案](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/calibration-autofocus-film-profiles.md)
- [S12：会话与访问协调](https://raw.githubusercontent.com/mr258876/PRISM-Utility/dev/docs/architecture/scanner-session-and-access.md)
- [S13：PRISM102 外设固件说明](https://raw.githubusercontent.com/mr258876/PRISM-Film-Scanner/main/102_PeriControl_Firmware/README.md)

阶段文件内 `[Sx]` 均指本节来源。
