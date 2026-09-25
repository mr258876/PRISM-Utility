# 阶段 03A：原始信号检查最终实施报告

## 基线、范围与前置条件

- 基线为 `dev`，SHA `4e9b3458c3596f4b1f03a9018bf8d028486566`。未发现适用的 `AGENTS.md`；使用 `git ls-files` 定位了当前 `.sln`、项目和实现文件。工作区原有阶段 01、02 修改仍为脏状态；本报告不归属、不覆盖这些既有改动，也没有清理、切换分支或提交它们。
- 本轮范围仅限 03A：原始单行剖面、列 ROI 统计、直方图及来源关联。没有实施 03B 冻结／A 基线、03C 对焦过程数据或阶段 04 生命周期提取。
- 阶段 02 的来源契约与当前实现支持关联工作流 pass 和采集来源。`IScanImageDecoder.TryGetSample16` 读取 packed RAW 16 位样本；预览 Gamma／白点显示处理不是 RAW 统计来源。此结论来自源码和 managed/fake 测试，不是实机证据。
- 基线解码测试 **PASS 5/5**。基线组合 filter 实际启动但在 240 秒超时，记作 **FAIL（测试调用未完成，非断言失败）**；它不是 PASS，也不用于推断产品回归。

## 旧入口到新入口映射

| 旧入口 | 新入口／保留方式 | 命令或路径 | 状态／数据来源 |
| --- | --- | --- | --- |
| 中央图像预览及缩放工具 | 保留“图像预览”模式，同一选择器增加“线剖面” | `RawSignalModeSelector` 只切换显示，不调用采集命令 | 既有 `PreviewFrame`、Win2D 预览，不作为 RAW 统计来源 |
| 瀑布预览及选项 | 保留于图像预览模式 | 既有瀑布呈现路径 | 预览数据，不由新剖面替代 |
| 现有 ROI 编辑／选择 | 沿用当前选中 ROI，变更后刷新 RAW 结果 | `TryGetSelectedRoiRange`、`RefreshRawRoiIfChanged` | 已解码列坐标，范围端点包含 |
| 设备／单通道采集与工作流 | 保留既有开始、停止及电机停止按钮 | 既有采集命令；RAW 在已发布预览行或最终结果路径关联 | Capture ID、pass、逻辑通道、结果版本及完成行数；多 pass 必须有唯一匹配来源 |
| 新增行号输入 | RAW 行输入框 | `RawSignalRowInput` 属性更新触发分析请求 | 零起始已完成行 |
| 新增 RAW 结果展示 | Win2D profile／直方图画布及来源、统计文本 | Core `ScanRawSignalAnalyzer`；后台计算后经 dispatcher 发布 | profile 为解码行全宽；统计／直方图按选定行及当前列 ROI |

## 实施与变更文件

下列为 03A 功能相关的实际实现与测试路径，不能据此把共享工作树的全部脏文件归为本阶段。

- `PRISM Utility.Core/Services/ScanRawSignalAnalyzer.cs`：RAW 统计及 profile 绘图聚合。
- `PRISM Utility.Core/Models/ScanRawSignalModels.cs`：统计结果、profile 样本和绘图 bucket 模型。
- `PRISM Utility.Core/Models/ScanWorkflowModels.cs`：工作流 pass／行可用数据模型及来源上下文。
- `PRISM Utility.Core/Services/ScanWorkflowService.cs`：完成行增量的稳定副本、pass 来源与 Capture ID 传递。
- `PRISM Utility/ViewModels/ScanDebugViewModel.RawSignal.cs`、`ScanDebugViewModel.RawSignal.Worker.cs`：来源关联、分析请求／发布、过期结果校验及有界 worker。
- `PRISM Utility/ViewModels/ScanDebugViewModel.cs`：采集、ROI 和页面激活状态挂接。
- `PRISM Utility/Views/ScanDebugPage.xaml`、`ScanDebugPage.xaml.cs`：模式选择器、输入和结果绑定、Canvas 绘制及页面事件。
- `PRISM Utility/Strings/en-us/Resources.resw`、`PRISM Utility/Strings/zh-CN/Resources.resw`：中英文标签、来源、统计和不可用状态。
- `DESIGN.md`：原有 Fluent 工作台契约中补充图表及紧凑标题行布局，不改全局主题。
- `PrismUtility.Core.Tests/ScanRawSignalAnalyzerTests.cs`：分析定义与边界测试。
- `PrismUtility.Core.Tests/ScanDebugCalibrationStatusTests.cs`、`ScanDebugRawSignalUiSourceTests.cs`：VM 行为与 UI 源码契约测试。
- `PrismUtility.Core.Tests/FilmProfileSourceContractTests.cs`：保留原预览/停止/ROI 约束，同时将新增图表画布和工作区选择器分别定位；`PrismUtility.Core.Tests.csproj` 显式链接两个新增 VM partial 文件。
- `PrismUtility.Core.Tests/ScanWorkflowServiceTests.cs`：工作流来源及完成行快照测试，包括排队前脱离可变缓冲区、连续增量与 FIFO 顺序。
- `PrismUtility.Core.Tests/Set005SettingsViewModelBehaviorTests.cs`：项目现有 `RecordingUiDispatcher` fake，验证 dispatcher 排队、回滚发布和 teardown 行为；这是测试 dispatcher，不是生产 UI dispatcher。

未增加设备命令、协议／schema、会话 owner 或新的生命周期提取。增加的是公开 RAW 显示输入／结果绑定；页面激活 guard 沿用现有机制。没有改变扫描算法、持久化或硬件绑定语义。

## 数据定义与并发边界

- **来源**：`IScanImageDecoder.TryGetSample16` 从设备 packed 行读取 16 位 RAW 样本；profile 使用 `GetDecodedPixelsPerLine()` 的完整解码宽度。BGRA、Gamma、白点映射不参与统计。来源缺失、不可归属或缓冲区无效时显示不可用，不伪造 RAW 值。
- **坐标／范围**：行号零起始；ROI 的 `Start..EndInclusive` 闭区间。profile 是当前选定的一个已完成行，覆盖完整解码宽度。统计及直方图使用该行在当前列 ROI 中的样本，不宣称为多行统计。
- **指标**：`N` 为选定行 ROI 内有效样本数；min/max 为 RAW 极值，mean 为 `sum(raw)/N` 并使用 `ulong` 累加。直方图固定 256 桶，以 `value >> 8` 分箱；满量程仅计 `raw == 65535`，饱和比例为 `FullScaleCount/N`。不把空间离散度称为时域噪声。
- **绘图**：profile 保留真实列坐标。像素宽不足时 bucket 保留局部极值和原列坐标，只用于绘图，不参与统计。
- **缓冲区与 worker**：工作流回调仅复制新增的已完成行范围，再排队交付；`ScanWorkflowServiceTests` 覆盖缓冲区后续修改不影响快照、连续 delta 和 FIFO。RAW worker 每次只复制所选行，至多一个运行任务和一个最新 pending 请求；最终结果使用现有完成结果的 owner reference，不额外 clone 全帧。结果按 revision、页面 epoch、capture/pass/role、结果版本及 ROI revision 校验并丢弃过期项。dispatcher 上复核后发布；页面激活沿用现有 guard。测试证明 managed 路径，不替代设备缓冲区或长期运行实测。
- **多 pass**：只接受唯一匹配 role 的 RAW 来源；合成结果不能冒充单通道 RAW。多 pass 最终合成图的坐标不能自动解释为原始单通道坐标，界面须保留来源限制说明。

## 需求证据矩阵

状态区分源码／managed、原生 UIA、视觉及设备证据。某一层级 PASS 不替代另一层级。

| 需求 | 最终实现证据 | 状态与边界 |
| --- | --- | --- |
| P3-01 唯一 RAW 来源与坐标 | packed 16 位 decoder、闭区间 ROI、capture/pass/role 匹配；无法唯一归属时拒绝 | **PASS** 源码及 managed 契约证据；设备样本及实机坐标 **NEEDS_DEVICE_VALIDATION** |
| P3-02 全行单行 profile | Core profile 覆盖解码行宽，绘图 bucket 保留局部峰谷，WinUI 有行输入及 profile 绑定 | **PASS** Core、UI 源码及紧凑 UIA 尺寸/切换证据；实际曲线渲染、视觉与对比度仍 **ENVIRONMENT_BLOCKED** 于黑屏截图，含真实数据的曲线 **NEEDS_DEVICE_VALIDATION** |
| P3-03 ROI 统计定义 | `N`、min/max、mean、256 桶、满量程计数／比例按单行闭区间 ROI 计算 | **PASS** Core 测试、165/165 聚焦及最终 1449/1449 全量 managed 测试 |
| P3-04 同来源及过期结果处理 | worker key 关联 capture/pass/role/version/行/ROI/page/revision；dispatcher 发布前复核，过期结果丢弃 | **PASS** 最终 managed 聚焦与全量测试及 Core 源码；实机持续采集行为未验证 |
| P3-05 性能和内存边界 | 单 worker 加单最新 pending；所选行副本；完成结果 owner reference；Core Release test-only benchmark | **PASS** 有界策略、Core 测量及紧凑 UIA 切换；不是 UI FPS、真实缓冲区内存或长期压力测试，后者 **NOT_RUN** |

## 验收测试矩阵

| 测试 | 验收点与证据 | 状态 |
| --- | --- | --- |
| P3-T01 | fixture 的 min/max/mean、直方图与满量程比例；Core analyzer 专项 21/21 | **PASS**，且聚焦测试 165/165、最终全量 1449/1449 |
| P3-T02 | ROI 边界、奇偶宽、单行、满量程、全零、空域及坏 buffer；Core analyzer 专项与最终 managed suite | **PASS** |
| P3-T03 | Gamma／白点显示处理与 RAW 分离；来源契约、VM 测试及处理后来源拒绝 | **PASS** managed/source；实机显示处理前原始数据 **NEEDS_DEVICE_VALIDATION** |
| P3-T04 | 快速切 ROI／通道／图像时丢弃旧结果；最终 VM localization 后聚焦及全量 managed suite | **PASS** managed；没有宣称原生高速交互压力通过 |
| P3-T05 | 未完成行不进入分析且来源 pass 匹配；workflow detached snapshot／pass 元数据测试与最终 managed suite | **PASS** managed；真实 streaming workflow 尚未实机验证 |
| P3-T06 | 绘图聚合保峰，统计仍用完整域；Core 测试及 UI 源码契约 | **PASS** managed/source；Canvas 实际像素绘制不能据黑屏截图判 PASS |
| P3-T07 | 单 worker／单最新 pending 有界；worker 源码、最终 managed tests 及 Core test-only benchmark | **PASS** 源码／managed 边界；长时间 UI 压力及响应 **NOT_RUN** |
| P3-T14 | 主题、键盘、缩放、可访问说明及阶段01主要操作回归；原有源码契约 75/75、最终全量 1449/1449；离线 UIA 5/5 | **PASS** 的范围为源码契约及紧凑模式切换/停止入口/118 DIP 图像视口；真实曲线、完整键盘焦点、English/light/high-contrast 和对比度 **NOT_RUN** 或被黑屏截图 **ENVIRONMENT_BLOCKED**，实机主要操作 **NEEDS_DEVICE_VALIDATION** |

## 最终测试、构建与原生证据

- **PASS** Core analyzer 专项测试：21/21。
- **PASS** 最终 localization 后聚焦测试，命令：

  ```text
  dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 --filter 'FullyQualifiedName~ScanRawSignalAnalyzerTests|FullyQualifiedName~ScanDebugRawSignalUiSourceTests|FullyQualifiedName~ScanDebugCalibrationStatusTests.RawSignal_|FullyQualifiedName~ScanDebugCalibrationStatusTests.CaptureEvidence_|FullyQualifiedName~ScanDebugCalibrationStatusTests.Streaming|FullyQualifiedName~FilmProfileSourceContractTests|FullyQualifiedName~ScanWorkflowServiceTests' -- RunConfiguration.MaxCpuCount=1
  ```

  结果 **165/165 PASS**。此前 UI 源码测试 75/75、4/4 是前一轮证据，不与最终 165/165 混为一次运行。
- **PASS** 完整 managed Core suite：

  ```text
  dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -p:Platform=x64 -- RunConfiguration.MaxCpuCount=1
  ```

  最终布局修改后结果 **1449/1449 PASS**（先前布局修改前为 1448/1448）。执行者在 x64 Release 配置也实际运行了 **1449/1449**；这两个结果与聚焦 165/165 分开记录。
- **PASS** Core x64 build：`dotnet build 'PRISM Utility.Core/PrismUtility.Core.csproj' --no-restore -p:Platform=x64 -v:q`，0 warnings、0 errors。
- **PASS** app x64 Release build：`dotnet build 'PRISM Utility/PrismUtility.csproj' --no-restore -p:Platform=x64 -p:Configuration=Release -m:1 -v:q`，布局调整后再次执行，0 errors、1 条既有 WindowsAppSDK `SingleFile` advisory。x64 Debug 配置也构建通过。
- **ENVIRONMENT_BLOCKED** 完整 solution build：`dotnet build 'PRISM Utility.sln' -p:Platform=x64 --no-restore -m:1 -v:q` 在 `DngSdkWarpper.vcxproj` 第 36 行因缺少 `$(VCTargetsPath)\Microsoft.Cpp.Default.props` 报 MSB4278。独立 Core 与 app build 已通过，不能把 solution build 记作通过。
- **PASS** 最终 Release 原生 UIA：`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage03a-native-qa.ps1' -AppPath 'D:\ProgrammingStuff\Project PRISM\Host Software\PRISM Utility\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PrismUtility.exe' -WorkspaceRoot 'D:\ProgrammingStuff\Project PRISM\Host Software' -EvidenceRoot 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage03a-native-offline-final-layout'`，**5/5**：图像默认模式、紧凑视口不少于 110 DIP、无 RAW 时显示“暂无 RAW 样本”、线模式行／电机停止入口、返回图像模式。`ui-trace.json` 记录子进程退出码 0；这是离线交互，不是实机采集或截图视觉门。
- 实际窗口为 1800×1440 physical pixels、192 DPI，即 900×720 DIP。移动选择器至标题同行后，图像 viewport 从修改前 166 physical pixels（83 DIP）恢复为 **236 physical pixels（118 DIP）**；同条件下阶段01早期约 276 pixels（138 DIP）。紧凑 UIA 尺寸/切换 **PASS**，仍不声称达到旧高度或覆盖 Medium/Wide 布局。
- **ENVIRONMENT_BLOCKED** 最终与预最终的 PrintWindow、CopyFromScreen PNG 均为黑色应用内容；虽然 UIA 可见中文文案及坐标，截图不能用于判定布局外观、实际绘图或对比度。当前环境未配置 XAML、RESW、Markdown LSP；x64 应用构建编译 XAML/资源，双语 RESW 经源码测试 XML 解析，C# 诊断无错误。
- **NEEDS_DEVICE_VALIDATION** 没有真实设备授权验证；capture-bearing 原生交互与真实 RAW/pass 来源尚待授权设备验证。

## Core Release 分析基准

命令：

```text
dotnet run --project 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage03a-bench.csproj' --configuration Release -v:q
```

| 输入 | 每次分析时间 | 每次分析分配 | 说明 |
| --- | ---: | ---: | --- |
| 1 行 × 128 列 | 0.208 ms | 245169 B | Core test-only，零 packed buffer |
| 64 行 × 4096 列 | 5.654 ms | 245176 B | Core test-only，零 packed buffer |
| 512 行 × 全宽 | 63.078 ms | 247592 B | Core test-only，零 packed buffer |

这些不是 UI FPS、设备吞吐、持续预览响应或真实采集缓冲区内存测量。

## 明确待办与边界

- **PASS：最终 compact UIA 回归。** 同一 900×720 DIP、192 DPI 条件实测图像视口 118 DIP（修前 83 DIP），最终隔离实例 5/5。仍比阶段01早期约 138 DIP 小约 20 DIP；Medium/Wide、缩放矩阵、完整焦点顺序及真实图像上的 ROI/统计切换 **NOT_RUN**，不能据源码测试推断通过。
- **ENVIRONMENT_BLOCKED：视觉 QA。** PrintWindow 和 CopyFromScreen 两种截图捕获均黑屏，不能将此负面结果转写为通过。
- **NEEDS_DEVICE_VALIDATION：设备验证。** 未发送设备命令；需另行授权后验证实际 RAW 样本、pass／通道和完成行来源。
- 最小下一步是取得可显示非黑色 WinUI 内容的交互会话以完成主题/对比度/绘图视觉检查；若要验证真实采集，再另行取得明确设备授权。以上均非 03B、03C 或阶段 04 的启动授权。
- 完整 solution build 被本机缺失 C++ targets 阻断；Core 与 WinUI app 分开构建通过。
- 本轮没有开始 03B、03C 或阶段 04。
