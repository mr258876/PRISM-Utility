# 01V 视觉修正实施与证据交接

**结论：代码、构建、托管测试及有限原生截图证据已记录；视觉验收未通过（两位独立只读审查均为 REVISE）。** 本报告仅覆盖 01V，不代表 V1–V6 全部通过。

## 基线、范围与环境

- 基线 HEAD：`f3fdf71721de36b6886c5acb13447f84e04b5eb1`（`dev`）。原用户未跟踪任务文档 [`01V_Visual_Repair.md`](../01V_Visual_Repair.md) 保留未改。
- Windows 主显示器：3840 x 2160 物理像素。截图窗口 `GetDpiForWindow` 为 192 DPI（2 倍）；原生 QA 几何分别实测 `XamlRoot.RasterizationScale=2`，不是由 DPI 推断。
- UI 改动涉及 `ScanDebugPage.xaml`、`.xaml.cs`、中英文 `Resources.resw`、`DESIGN.md` 和相关 Core 测试。页面、字符串、设计说明及测试改动属于 UI 范围。QA 采集辅助类型由 `PRISM_VISUAL_QA` 条件编译，仅用于只读几何/截图证据。业务命令、硬件协议、持久化、取消、会话与生命周期所有权均未改。
- 截图过程中未发送设备、采集、电机、照明、参数应用、保存或校准命令。

## 构建与托管测试

| 项目 | 状态 | 证据说明 |
| --- | --- | --- |
| Production Release x64 应用构建 | **PASS** | 已构建；存在预先已有的 Windows App SDK `PublishSingleFile` advisory。 |
| 隔离 QA Release x64 构建 | **PASS** | 使用 `PrismVisualQa=true`、`OutputPath=bin/x64/Release-01VQaProbe`、`IntermediateOutputPath=obj/x64/Release-01VQaProbe`、`--no-restore`。生成 DLL 含 QA 类型名。当前 DLL SHA-256：`035E9778AB5D7478E26231851D268161EBE24C330EAC3907554750320A022A2F`；EXE SHA-256：`D61B0FA29354DD51B77B4E400AD4F657FD1E5C2996D67AB4C3E005471411E264`。 |
| 聚焦托管测试 | **PASS，134/134** | ScanDebug 相关聚焦测试。 |
| 全量托管测试 | **PASS，1508/1508** | 全量 managed suite。 |

隔离 QA 构建后到截图期间，应用源码保持不变；后续只有文档/QA driver 变化。探测截图清单记录的是操作者提供的预构建二进制和运行标记，本身不证明其构建来源；本节构建收据单独记录 QA 构建及哈希。构建和测试不能替代原生视觉验收或实机验证。

## 原生运行、视口测量与对照

基线是无设备、无帧、无待审查候选的正常空态；after 空态运行以 no-seed QA 标记确认无预览帧、无候选。另一组 after 是 QA 合成 BGRA 预览 fixture，有 pending candidate；它不是 RAW 样本、不是设备测量，也不能用来证明 RAW 信号呈现。

| 窗口物理尺寸 | Root 实测 DIP | Before 可视视口 DIP / 状态 | After 可视视口 DIP / 状态 | 判断 |
| --- | --- | --- | --- | --- |
| 2350 x 1440 | 977 x 604 | 637 x 79，两栏默认空态 | 645 x 230，两栏默认空态 | 视口有实际改善，但图像区仍显局促。root 宽度未达到 `>=1000 x 600` 条件，因此条件性 320 DIP 目标为 **NOT_APPLICABLE**，不可把 230 DIP 记成阈值 FAIL 或 PASS。 |
| 3550 x 1850 | 1305 x 809 | 693 x 284，宽布局且检查栏打开 | 950 x 435，宽布局默认且检查栏关闭 | 前后检查栏状态不同，宽度不可直接比较。仅高度可比：284 到 435 DIP。After 默认关闭检查栏的 root 达到 `>=1280 x 720`，视口高度达到 420 DIP，故该已捕获状态的尺寸门槛 **PASS**。 |

另一次补充 wide inspector-open 几何响应测得视口 701 x 435 DIP（root 1305 x 809，scale 2，无帧、无候选），但截图采集未通过，**没有有效 PNG**；该状态只能作为局部几何记录，视觉状态 **NOT_RUN**。不能将它与 before inspector-open 的 693 x 284 当作完整视觉前后对照。

有效 PNG 示例：before [两栏空态](01V-evidence/before/zh-CN-two-column-empty-qa.png)、[宽布局空态](01V-evidence/before/zh-CN-wide-empty-qa.png)；after [两栏采样空态](01V-evidence/after/ready-probe-release/two-column-Sampling-default-empty.png)、[宽布局采样空态](01V-evidence/after/ready-probe-release/wide-Sampling-default-empty.png)。逐截图记录及来源标记见 [before README](01V-evidence/before/README.md)、[before 几何清单](01V-evidence/before/native-inprocess-measurements.json)、[after README](01V-evidence/after/README.md)、[after 空态清单](01V-evidence/after/ready-probe-release/native-inprocess-measurements.json) 和 [after 合成 fixture 清单](01V-evidence/after/fixture-probe-release-retry1/native-inprocess-measurements.json)。仅链接成功、有效的截图；失败尝试不作为图片证据。

## V1–V6 实现与证据映射

| 要求 | 实现位置 | 证据与状态 |
| --- | --- | --- |
| V1：压缩顶部并归还主观察区高度 | 页面 XAML 顶栏、运行区、预览区；code-behind 布局更新 | **部分。** 两栏 79→230 DIP 有提升但仍局促；由于 root 未满足条件，320 DIP 门槛为 **NOT_APPLICABLE**。宽布局仅高度可比较；检查栏关闭时 435 DIP 达到 420 DIP。检查栏打开仅有 701 x 435 几何，无有效截图。 |
| V2：任务切换具有持续选中态 | 原生 task `SelectorBar`、窄宽回退 `ComboBox` 及同步逻辑，见 `ScanDebugPage.xaml` / `.xaml.cs` | **部分。** 16 张默认首屏涵盖四任务；compact 预览/返回以及跨切换草稿保持 **NOT_RUN**。离线只读的任务选择序列中，首次选择 Motion、脚本写入草稿之前，状态由 saved 变为 unsaved；原因未核实，不断言为切换导致持久化变更。 |
| V3：默认任务内容优先 | 任务区域重排、披露区和编辑区布局；任务选择/导航 code-behind | **部分，REVISE。** 四任务均有两种窗口默认首屏。Motion 的 Move 在两种窗口首屏下方；黑白手动白值编辑框仅露出 4 物理像素（2 DIP）。 |
| V4：原生 Fluent 层级与窄栏表单 | 页面 XAML、WinUI 原生控件、本地化字符串、`DESIGN.md` | **部分，REVISE。** 有效截图为原生 WinUI，但两栏黑白场中白值框严重裁切。合成 ROI 覆盖文字对比度不足；焦点辅助文字 `0.05 / mm` 或 `(7450 px)` 换行观感不佳。英文、浅色、高对比度未验证。 |
| V5：信号检查作为读数区 | XAML 检查区/RAW 展示及从结构化结果投影的 code-behind | **未完成视觉验收。** 合成 BGRA fixture 不是 RAW；没有已知 RAW 源，RAW 呈现 **NOT_RUN**。不可据此声称真实数据下 V5 通过。 |
| V6：按实测尺寸和任务连续性验收 | 现有 `ScanWorkbenchPreviewLayout` 与 code-behind 响应布局 | **部分，REVISE。** root、DPI、XamlRoot 和默认视口有实测。两栏尺寸不满足 320 DIP 条件的适用 root；宽默认关闭检查栏时 435 DIP 达标。Compact 状态、检查栏打开视觉、preview/back 和切换后草稿保持 **NOT_RUN**。 |

已捕获并由两位审查员观察到的交互证据包括两栏手动黑/白草稿 `513` / `60000`、两栏检查栏切换和辅助/显示 flyout；这些安全 UI 操作没有触发硬件命令。不能用交互可达或截图完整性推导所有视觉要求通过。

## 独立视觉审查与最终判定

已完成两次独立只读 Oracle 视觉审查，**两者均为 REVISE**；因此 01V 的视觉 QA gate **NOT SATISFIED**，没有整体视觉 PASS。审查检查了全部 16 张默认首屏图像。确认的具体问题：

- 两栏黑白场首屏白值编辑框仅显示 4 物理像素（2 DIP），基本不可见。
- Motion 的 Move 操作在两种窗口大小下都落在首屏折叠区之外。
- 合成预览的 ROI 覆盖标签对比度不足；对焦焦点信息 `0.05 / mm` 或 `(7450 px)` 换行不自然。
- 离线只读任务选择序列在第一次 Motion 选择时、脚本写入草稿前，UI 状态从 saved 变为 unsaved。原因尚未确认；这里只报告观察，不推断为持久化被修改。

保留的正向证据：16 张默认图均捕获完整、通过 PNG 签名/尺寸/hash 与遮挡检查并逐一查看；默认任务选中状态可见；两栏手动草稿、检查栏 toggle 与辅助/显示 flyout 已安全演示。Capture-integrity PASS 不是 product visual PASS。

## 未验证项、硬件与停止点

| 项目 | 状态 |
| --- | --- |
| Compact / 检查栏打开的有效原生截图及视觉结论 | **NOT_RUN**；补充 wide inspector-open 只有 701 x 435 DIP 几何响应，无有效 PNG；compact 捕获重试因 compositor 检查失败而停止。 |
| 英文、浅色、高对比度 | **NOT_RUN** |
| 100% / 150% 缩放 | **NOT_RUN**；本轮仅测试 200%（192 DPI）。 |
| 已知来源 RAW 样本下的 V5 | **NOT_RUN**；暂无已知 RAW 源。 |
| 硬件命令、真实设备 | **NOT_RUN**；真实设备状态为 **NEEDS_DEVICE_VALIDATION**。 |

完成本轮有界 01V 修正及一次补充截图重试后停止。依用户限定，不继续截图/修正循环，不进入阶段 04，不改应用源码、QA 脚本或任务书，不执行提交、reset、clean 或 push。以上 REVISE 项及 NOT_RUN 项如需继续，须由用户明确授权后另开范围。
