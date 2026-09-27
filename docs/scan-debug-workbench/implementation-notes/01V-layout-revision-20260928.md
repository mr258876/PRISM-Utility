# 01V 原生布局修订与验收交接

**结论：REVISE。** 已完成本轮限定的两次页面实现后视觉检查，不再扩大到阶段 4 或第三轮视觉改动。两种约定 Root 尺寸下预览视口高度均达标，四任务的主要操作组比基线完整；但两栏黑白场的 ROI 摘要及验证问题提示仍落在首屏以下，不能报告整体验收 PASS。

## 基线与变更边界

- 基线：`dev` 的 `d6ffd6399f8eaf28fe386f2ba7066715afec8baf`；开始时只有用户提供的 `PRISM_ScanDebug_Screenshot_Layout_Revision.md` 未跟踪。适用路径未发现 `AGENTS.md`。
- 用户本轮明确要求四任务默认首屏，因此在修订稿默认仅黑白场的基础上，额外重排采样、对焦和运动的**呈现**；没有实施文档第 6 节的检查区重构，也未进入阶段 4。
- 页面变更：`ScanDebugPage.xaml`、必要的 `.xaml.cs` 呈现/选择/焦点处理、两种语言资源、`DESIGN.md` 的页面局部规则；Shell 只对 ScanDebug 使用 `12,24,12,0` 的内容宿主边距，其他页仍使用 `56,24,56,0`。现有原生捕获脚本仅增加可配置物理窗口尺寸和安全 UIA 验证。
- 原有采集、停止采集、停止全部电机、三个自动校准、每轴停止、手动应用/撤销、ROI/缩放、候选审查和配置入口继续使用原命令及绑定。未修改 ViewModel/Core 设备命令、协议、校准算法、持久化/输入提交时机、取消语义或页面会话/生命周期所有权。未发送真实硬件命令。

## 原生截图与几何

以下为当前 HEAD 在**布局修改前**构建的条件 QA 无种子原生 WinUI 截图，以及改动后重新构建同一 QA 配置拍摄的原生截图。每个路径均是整窗 PNG，不是设计稿或拼图。两档尺寸的默认检查区均关闭，语言中文、深色主题、设备离线、没有预览帧。逐图 Page/Root/裁剪后可见视口、截图物理边界、时间、DPI、XamlRoot 比例和哈希见 [前测清单](01V-evidence/01V-revision-before-20260927/native-inprocess-measurements.json) 与 [终测清单](01V-evidence/01V-revision-after-round2-20260927/native-inprocess-measurements.json)。

| 任务 | 两栏前 / 后 | 宽版前 / 后 |
| --- | --- | --- |
| 采样 | [前](01V-evidence/01V-revision-before-20260927/two-column-Sampling-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/two-column-Sampling-default-empty.png) | [前](01V-evidence/01V-revision-before-20260927/wide-Sampling-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/wide-Sampling-default-empty.png) |
| 黑白场 | [前](01V-evidence/01V-revision-before-20260927/two-column-BlackWhite-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/two-column-BlackWhite-default-empty.png) | [前](01V-evidence/01V-revision-before-20260927/wide-BlackWhite-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/wide-BlackWhite-default-empty.png) |
| 对焦 | [前](01V-evidence/01V-revision-before-20260927/two-column-Focus-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/two-column-Focus-default-empty.png) | [前](01V-evidence/01V-revision-before-20260927/wide-Focus-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/wide-Focus-default-empty.png) |
| 运动 | [前](01V-evidence/01V-revision-before-20260927/two-column-Motion-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/two-column-Motion-default-empty.png) | [前](01V-evidence/01V-revision-before-20260927/wide-Motion-default-empty.png) / [后](01V-evidence/01V-revision-after-round2-20260927/wide-Motion-default-empty.png) |

| 实际窗口（前 → 后，物理 px） | 实测 DPI / XamlRoot 比例 | Page / Root（前后相同，DIP） | 图像可见视口前 → 后（DIP） | 高度目标 |
| --- | --- | --- | --- | --- |
| 2350×1440 → 2174×1440 | 192 / 2 | 1001×640 / 977×604 | 645×230 → 645×396 | 396 ≥ 380 |
| 3550×1850 → 3374×1850 | 192 / 2 | 1329×845 / 1305×809 | 950×435 → 950×601 | 601 ≥ 580 |

调整物理窗口宽度是为了抵消仅本页 Shell 边距的变化，保持**相同实际 Root**，不是扩大 Root 凑指标。可见视口在 Page 坐标中的起点由两档基线的约 `y=326` 降到 `y=173` DIP；Root 高度减去视口高度由 374 降到 208 DIP。终测默认 PNG 已逐张打开检查，没有整窗遮挡、黑色未合成区或主操作控件底边裁切；清单中的 `CAPTURED_NOT_VISUALLY_REVIEWED` 是捕获时状态，不是产品视觉结论。条件 QA 构建的空态经进程内标记核对为 `emptyStateCapture=true`、`previewFramePresent=false`、`pendingCalibrationPresent=false`。构建后 QA DLL SHA-256 为 `98F9E00C06360D9082128BA9487343193C1589E2BFE14822A89954A0BE81BED2`；捕获清单单独记录运行进程和逐图哈希，但其通用 `sourceBuild` 字段本身不认证构建来源。

## 视觉与交互结果

- **PASS（已观察的范围）**：顶部行减少纵向堆叠，任务单选在宽版以 SelectorBar、两栏以同步 ComboBox 持续可见；四任务首屏均使用原生控件。两栏黑白两个输入、应用/撤销及三项自动动作完整可见，不再出现原先白值仅露出 2 DIP 的情况。[两栏白值焦点图](01V-evidence/01V-revision-interactions-retry2-20260927/two-column-ManualWhiteLevelTextBox-focus-empty.png)显示输入下边缘和活动焦点框均完整。两栏及宽版运动的方向、距离/速度、有效移动摘要与 Move 同屏，所有三轴 Stop 入口仍可见；对焦限位说明在点动控件下方横跨完整宽度。采样的低频会话照明测试留在详情中。
- **REVISE（未达到的本轮项）**：两栏黑白场的 ROI 摘要和真实验证问题提示仍在参数区首屏以下；详情入口在底部边界处被部分裁切。宽版能显示这些提示。两位独立只读审查均判 REVISE；中文专项复核另指出两种尺寸的运动有效移动摘要在“电机”“对焦”“移动距离”等语义词中间换行，宽版黑白场/对焦的 ROI 读数将 `(7450 px)` 的数值与单位拆到两行，宽版缺档提示也有词中断行。故整体验收不是 PASS。按照文档两轮上限到此停止，不为此继续第三轮页面改动。
- **PASS（限定的原生安全操作）**：[交互清单](01V-evidence/01V-revision-interactions-retry2-20260927/native-inprocess-measurements.json) 记录两尺寸黑/白输入 `513` / `60000` 的未应用草稿跨采样/黑白场视图切换仍在输入框中、两栏/宽版检查入口和[两栏直接显示浮层](01V-evidence/01V-revision-interactions-retry2-20260927/two-column-preview-flyout-empty.png)/[宽版直接显示浮层](01V-evidence/01V-revision-interactions-retry2-20260927/wide-preview-flyout-empty.png)可打开、Log → ScanDebug 导航往返后 Root 几何恢复。检查区的两栏替换参数区行为是既有机制，本轮未实施文档第 6 节的覆盖式侧栏。
- **未由该 QA 构建证明**：条件空态钩子每次页面加载都会强制选择采样，故不能用上述导航往返宣称真实任务记忆通过。离线没有通道档案，应用命令被原条件禁用；仅确认按钮与草稿/禁用原因可见，没有执行 Apply/Save、硬件自动校准或电机/采集命令。未执行非法输入的原生校验路径、通道切换草稿、真实帧缩放/ROI 手势。另有 [合成 BGRA 帧截图](01V-evidence/01V-revision-fixture-20260927/two-column-BlackWhite-default-synthetic.png)，只供现有预览/覆盖呈现检查；它不是 RAW、真实设备读数或本轮空态验收图。该图的彩色覆盖标签在局部背景上对比度偏低，真实帧/主题仍需另行验证。

## 构建、测试与未覆盖项

- `dotnet build 'PRISM Utility/PrismUtility.csproj' -c Release -p:Platform=x64 -p:PrismVisualQa=true -p:OutputPath=bin/x64/Release-01VQaProbe/ -p:IntermediateOutputPath=obj/x64/Release-01VQaProbe/ --no-restore -v:q`：**PASS**（修改前与最终版分别构建）；正常 Release x64 `-p:PrismVisualQa=false --no-restore`：**PASS**。均仅有已有的 Windows App SDK `PublishSingleFile` 建议警告。
- ScanDebug 页面、原生信号 UI、几何、生命周期/本地化等聚焦源码契约测试：**PASS 150/150**；`dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-build --no-restore -v:q`：**PASS 1509/1509**。仅更新因本轮替换的旧行列位置而失效的断言，并增加共享头部/直接显示入口约束，保留停止命令、草稿绑定、生命周期激活守卫等保护；这些托管结果不代替上面的 REVISE 原生判定。
- 英文、浅色/高对比度、100%/150% DPI、极矮窗口与文字放大：**NOT_RUN**；真实扫描器、RAW 信号、在线命令和机械停止：**NEEDS_DEVICE_VALIDATION**。`.xaml`、`.resw`、`.ps1` 在当前环境没有 LSP，已由 WinUI 构建、原生运行和现有 PowerShell 驱动实际执行补足；C# 诊断无错误。

本轮未提交、未推送；用户提供的未跟踪修订文档保留原状。以上未达项需独立授权再修，不能把本次 REVISE 改记为 PASS 或顺手扩展为架构重构。
