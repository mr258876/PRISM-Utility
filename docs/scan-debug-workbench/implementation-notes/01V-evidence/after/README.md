# 01V 原生变更后证据

**16 张默认首屏 PNG 的捕获完整性 PASS；产品视觉判定 REVISE（两位独立 Oracle 均为 REVISE）。** 图像涵盖四项任务、两种窗口大小及两种 QA 来源。它们来自隔离的免安装 `Release-01VQaProbe` 二进制，**不是**生产设备测量。界面为中文/深色，扫描器离线；未改变桌面主题、DPI、语言或硬件。QA driver 使用隔离的 `TEMP`、`TMP`、`LOCALAPPDATA` 及各自输出目录内的绝对 `LocalSettingsOptions__ApplicationDataFolder`。交互限于任务选择、编辑框焦点和未保存草稿、检查栏切换及显示 flyout；没有执行 start/stop、电机、设备参数应用、保存或校准命令。截图捕获有效不代表产品视觉验收通过。

从仓库根目录，在 3840 x 2160、192 DPI 且可将应用置于前景的桌面上，成功执行的捕获命令如下：

```powershell
powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/capture-native.ps1' -Executable 'PRISM Utility/bin/x64/Release-01VQaProbe/PrismUtility.exe' -Source Empty -EvidenceDirectory 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/ready-probe-release'
powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/capture-native.ps1' -Executable 'PRISM Utility/bin/x64/Release-01VQaProbe/PrismUtility.exe' -Source Synthetic -EvidenceDirectory 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/fixture-probe-release-retry1'
```

首次 synthetic 尝试在 `fixture-probe-release` 中设置 `PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER=1`，40 秒内未显示原生窗口；没有合格 PNG 或 manifest。停止其无窗口 QA 子进程，从 driver 移除可选 RAW buffer 注入后，仅重试一次并成功捕获默认矩阵。该重试使用现有 QA **合成 BGRA 预览 fixture**，**不是 RAW 样本，也不是设备数据**；不得把其像素或 ROI 描述为实测 RAW。Empty 运行设置 `PRISM_VISUAL_QA_EMPTY_STATE=1`，每个几何响应均确认无预览帧、无待审查校准候选且 `emptyStateCapture=true`。两组图中设备状态显示离线。`empty-*` 与 `synthetic-run` 下早期尝试是未完成的历史诊断，不是本轮证据。

| 来源与窗口（物理 px） | Root DIP（按实测 scale 2 折算物理 px） | 可见无遮挡预览 DIP（物理 px） | 01V 建议门槛 / 结果 | Before 同窗口 |
| --- | --- | --- | --- | --- |
| Empty / Synthetic，2350 x 1440 | 977 x 604 (1954 x 1208) | 645 x 230 (1290 x 460) | `>=1000 x 600` root 条件不满足（977 < 1000），故条件性 320 DIP 视口目标 **NOT_APPLICABLE**；230 DIP 仅为实测值，视觉上仍局促，审查结论 **REVISE**。 | 977 x 604 root，637 x 79 viewport |
| Empty / Synthetic，3550 x 1850 | 1305 x 809 (2610 x 1618) | 950 x 435 (1900 x 870)，默认检查栏关闭 | `>=1280 x 720` root **PASS**；本次捕获状态下 420 DIP 高度 **PASS**（435 >= 420）。宽度不可与 before 直接比较：before 检查栏打开。 | 1305 x 809 root，693 x 284 viewport，检查栏打开 |

每个窗口大小下四项任务的默认首屏测量相同。每张截图都分别读取进程内 `XamlRoot.RasterizationScale=2` 和 `GetDpiForWindow=192`，而非相互推算。逐图 root ActualWidth/Height、`PreviewScrollViewer` viewport 和裁剪后可见矩形（DIP）、UIA 物理像素边界、进程/时间/来源、PNG SHA-256 与精确窗口物理边界见 [empty manifest](ready-probe-release/native-inprocess-measurements.json) 和 [synthetic manifest](fixture-probe-release-retry1/native-inprocess-measurements.json)。Wide after 首屏拍摄时检查栏关闭（此前两栏操作留下了关闭覆盖状态）；before wide 检查栏打开。因此两个状态只有高度可作谨慎比较，宽度不是同状态对照。后续两栏检查栏 toggle 只改变其明确标注交互截图的布局。

捕获完整性检查：两次成功运行 manifest 所列 36 张 PNG 均具有 PNG `89-50-4E-47-0D-0A-1A-0A` 签名、精确物理尺寸及匹配 SHA-256。16 张默认首屏均已打开并逐张检查：任务选中正确、原生窗口完整可见、设备离线，应用区域无外部遮挡或黑色/未合成区。Empty 预览无图像；Synthetic 显示彩色 QA fixture 与 overlay。左侧可滚动设置会延伸到首屏以下；宽布局 Motion 说明在滚动边界被截断。driver 在 CopyFromScreen 前后确认前景，但仍须人工逐图检查；PNG 有效本身不能证明无遮挡。以上是**捕获完整性 PASS**，不是视觉产品 PASS。

两位独立只读 Oracle 均给出 **REVISE**。主要视觉问题：两栏 Black/White 默认图中白值编辑框只露出 4 个物理像素（2 DIP）；两种窗口下 Motion 的 Move 均位于首屏以下；合成 ROI overlay 标签对比度不足；焦点辅助文字 `0.05 / mm` 或 `(7450 px)` 换行不自然。离线只读任务选择序列在第一次 Motion 选择时、脚本写入草稿前，状态从 saved 变为 unsaved；原因未核实，不据此断言发生持久化变更。总视觉判定仍为 **REVISE**。

可选交互结果：两次运行都捕获了黑/白编辑器焦点和 `513` / `60000` 未保存草稿，各窗口大小均有图；代表性图中可见草稿值。两栏检查栏 toggle **交互证据 PASS**，两栏 auxiliary 后接 display flyout **交互证据 PASS**（flyout 打开时会自然遮住部分预览）。Wide 检查栏自动化 **FAIL [evidence]**：两栏切换后其状态与预期不同，且 rail 本身不在 UIA 控件树中；driver 记为 `optionalInteractions=FAIL` 并保留已完成截图，这不是产品渲染失败的证据。后续 wide 检查栏/flyout **NOT_RUN**。Compact preview/back **NOT_RUN**：初始两个窗口均未进入 compact；补充脚本虽申请了 compact 尺寸，却在前置的 wide 截图阶段失败，未到达 compact 操作。焦点/检查栏/flyout 图不是默认首屏证据，不能替代默认截图。manifest 的 `CAPTURED_NOT_VISUALLY_REVIEWED` 是捕获时状态；本 README 记录后续人工与独立视觉检查，但不会把失败或缺失交互升级为 PASS。

未建立的覆盖：英文、浅色/高对比度、100%/150% DPI、compact preview/back、其他窗口尺寸、真实硬件和实测 RAW 来源均 **NOT_RUN**。本轮只有 200%（192 DPI）缩放证据。真实设备仍需 **NEEDS_DEVICE_VALIDATION**。没有已知 RAW 源。

## 补充 compact / wide 检查栏捕获尝试（2026-09-27）

`capture-native.ps1` 的定向 `-CompactAndWide` 路径复用隔离 QA 启动、UIA 选择及逐图进程内几何/捕获函数，但**不**重跑此前 16 图矩阵。它先请求一张 3550 x 1850 wide 检查栏打开图，之后原计划捕获六张 1840 x 1440 compact 图（BlackWhite 编辑器首屏、`513` 草稿、显式打开预览、返回编辑器、打开检查栏、关闭检查栏）。它前后检查选中项与 UIA 编辑器值，但不把这些作为测量。只接受 no-seed QA 模式。检查栏本身是 UIA 不可见的 `Border`，wide 可见性门控使用其可访问子项 `InspectionEvidenceExpander`。仅在明确隐藏预览时才允许 compact viewport 为零。已有截图集未覆盖。

以下命令确实使用同一预构建 `PRISM Utility/bin/x64/Release-01VQaProbe/PrismUtility.exe` 执行，且输出目录分别隔离（运行前均确认不存在）：

```powershell
powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/capture-native.ps1' -Executable 'PRISM Utility/bin/x64/Release-01VQaProbe/PrismUtility.exe' -Source Empty -CompactAndWide -EvidenceDirectory 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/compact-and-wide'
powershell.exe -NoProfile -File 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/capture-native.ps1' -Executable 'PRISM Utility/bin/x64/Release-01VQaProbe/PrismUtility.exe' -Source Empty -CompactAndWide -EvidenceDirectory 'docs/scan-debug-workbench/implementation-notes/01V-evidence/after/compact-and-wide-retry1'
```

首次运行：**FAIL [evidence]**，报 `Wide inspector not visible after safe toggle`。该次自动化门控问题是 `WorkbenchInspectionRail` 使用 UIA 不可见的 `Border`；后续脚本改为寻找可访问子项。一次有界修正后重试：**FAIL [evidence]**，报 `Capture appears uncomposited or uniform: wide-inspection-open-empty`。捕获工具在保存 PNG 前拒绝了 `CopyFromScreen` 位图。重试取得了[实时 wide 几何响应](compact-and-wide-retry1/isolated-temp/PRISM_Utility_VisualQaCaptureRequests/01v-after-07e690bc87fe43718a7c029a2fd6d9a4.request.result.json)：物理窗口 3550 x 1850、192 DPI、XamlRoot scale 2、root 1305 x 809 DIP、可见预览 701 x 435 DIP、无预览帧/待审查候选、no-seed 为 true。该响应**只有部分几何证据**，不是经视觉核验的 wide 检查栏截图。两次尝试都生成隔离 QA ready 标记，但**两个目录均无 PNG 或完整测量 manifest**。各自启动的进程已关闭，之后没有残留 PrismUtility 进程。因此 wide 检查栏视觉状态、实际 compact root、compact 预览 280 DIP 门槛、视图切换期间任务/草稿保持均 **NOT_RUN / 未验证**。两位独立视觉审查已经完成且均为 **REVISE**，不是 NOT_RUN；无效截图不得提供审查。不要从 compositor 捕获失败推断产品布局失败，也不要从此前两栏测量推断 compact 通过。此前 16 张默认首屏及其 manifest 是独立有效证据；本补充请求在唯一一次允许的重试后停止。没有改生产代码、测试、硬件状态或显示设置。
