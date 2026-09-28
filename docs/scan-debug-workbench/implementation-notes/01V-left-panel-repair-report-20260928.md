# 01V 左栏修复实施报告

本轮由 [01V-left-panel-repair.md](../01V-left-panel-repair.md) 接管旧 01V、01V-3 和 `0ca4635` 复核稿中冲突的局部视觉要求。实施基线为 `dev` 的 `0ca463526f50d89d5d7eab9824f0c981989651c9`；开始时仅任务书是用户已有的未跟踪文件，未覆盖它。仓库及上层未发现适用的 `AGENTS.md`。未执行阶段 4，未提交或推送。

## 结果与文件

- **黑白场编辑/状态：** `ScanDebugPage.xaml`、`.xaml.cs` 将现有左栏改为通道身份头、互斥的编辑/问题主体、稳定的 ROI/验证底栏。三个自动命令显式排成完整校准一行、单项动作一行；缺档案时显示短原因与真实参数入口，本地未应用输入独立显示。进入空配置黑白场时使用模型首个可用通道，经原受保护选择流程建立真实当前通道；切通道前保护未应用输入，不自动应用、建档或保存。
- **问题详情/定位：** 复用原问题投影和导航请求；点击“查看问题”直接显示消息、来源、严重性和可导航动作。返回保留输入及编辑滚动；定位退出详情后展开真实字段，旧请求在返回、任务/通道变化或页面卸载时失效。`ScanDebugViewModel.cs` 只增加呈现投影及选择/请求失效保护，原解析、命令和设备会话边界未改。
- **运动：** 三轴均按方向、距离、速度、完整估算摘要、移动/当前轴停止、其他两轴停止、高级设置排序。摘要从格式源去掉重复电机身份，保留 `0.5 mm`、`0.1 s`、原始方向及“物理方向未知”；三条停止命令和全电机停止仍在原路径。`Strings/en-us` 与 `Strings/zh-CN` 同步，`DESIGN.md` 只更新被本任务替代的局部规则。
- **验证文件：** 更新 `FilmProfileSourceContractTests.cs`、`FilmProfileLocalizationAccessibilityContractTests.cs`、`ScanDebugCalibrationStatusTests.cs`、`ScanDebugPageReentryUi006SourceContractTests.cs`、`ScanDebugViewModelVm002CharacterizationTests.cs`；现有条件 QA 投影 `PrismVisualQaCaptureService.cs` 增加 Root/预览在 Page 内的只读矩形及所选通道状态事实；`01V-evidence/capture-root-baseline.ps1` 保留原宽版门槛，增设原尺寸两栏独立运行方式并观察真实点击，不再要求旧通道库展开或预滚动到问题。

## 原生证据

此前四张 [默认黑白场](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-root-default-empty.png)、[未应用输入](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-unapplied-draft-root.png)、[旧问题展开](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-view-issues-expanded-root.png)、[默认运动](01V-evidence/01V-3-final-20260928/two-column-Motion-root-default-empty.png) 均已核对，原失败证据未改。最终同轮 QA 构建的 [回执](01V-evidence/01V-left-panel-two-column-20260928-k/root-baseline.json) 与 [黑白场默认](01V-evidence/01V-left-panel-two-column-20260928-k/two-column-BlackWhite-default-empty-root.png)、[入口键盘焦点](01V-evidence/01V-left-panel-two-column-20260928-k/two-column-BlackWhite-view-issues-focus-root.png)、[未应用输入](01V-evidence/01V-left-panel-two-column-20260928-k/two-column-BlackWhite-unapplied-draft-root.png)、[直接问题详情](01V-evidence/01V-left-panel-two-column-20260928-k/two-column-BlackWhite-view-issues-detail-root.png)、[默认运动](01V-evidence/01V-left-panel-two-column-20260928-k/two-column-Motion-default-empty-root.png) 已逐张打开，单人只读视觉复核对这些截图范围给出 PASS。

受测窗口为 `2174 × 1440` 物理像素、192 DPI，Root **977 × 604 DIP**、预览可见区 **645 × 396 DIP**；默认、草稿、详情和运动切换前后均未减小，PNG 为 **1954 × 1208** 像素的 Root 裁图，**不是包含 Shell 的全窗口截图**。原始宽版基线为 Root `1305 × 809`、预览 `950 × 601 DIP`，本轮未声称已复测。所见为中文深色、离线空预览；有效文字缩放未知。受测 DLL SHA-256 为 `03CF6CF946264C77B26E304F335035B9B58EEB526E7E8863CF074`，由本轮隔离 QA 构建提供；回执哈希并不构成独立源码到二进制认证。

| ID | 状态 | 证据与边界 |
| --- | --- | --- |
| B1 | PASS | 默认未滚动时输入、应用/撤销、三个自动动作、ROI/问题入口完整；独立焦点图证实“查看问题”焦点框四边未裁切。 |
| B2 | PASS | UIA 写入 `65536` 后仍为本地输入；短状态及底栏完整，草稿哈希和既有配置问题不变；详情返回仍保留输入。并未调用输入验证或应用。 |
| B3 | PASS | 单次 UIA 点击直接显示实际参数错误、字段路径与定位动作；条目及焦点在可见视口内，捕获前后未由脚本补滚动/补焦点。 |
| B4 | PASS | UIA 返回恢复输入、原滚动位置及可见入口焦点；随后定位同一可导航问题，曝光参数字段可见并聚焦，`65536` 保留。 |
| B5 | NOT_RUN | 0 条、被动、多条和超长问题的完整原生矩阵未构造；源码模板/投影测试不能替代滚动可达性验收。 |
| B6 | NOT_RUN | 有延迟加载取消与页面请求失效的托管/源码回归，但未原生执行快速切通道、离页和反复开关矩阵。 |
| B7 | PASS | UIA 点击配置入口定位到当前通道 `ExposureMicrosecondsTextBox` 且获得可见焦点；`65536` 和草稿哈希不变。未据此宣称已创建或保存档案。 |
| M1 | PASS | 原生两栏图中参数、摘要、移动/当前轴停止、另两轴停止完整且顺序正确；无重复 `Motor1`，单位及未知物理方向未丢。未实际转动电机。 |
| M2 | NOT_RUN | 轴选择不调用命令及摘要投影受托管/源码测试约束；多轴/方向/单位变化的完整原生操作与设备命令观察未执行。 |
| L1 | NOT_RUN | 两栏部分的 Root/预览几何及详情切换已通过；当前 DPI-aware 主屏 `2560 × 1600` 物理像素，容不下宽版所需 `3374 × 1850`，宽版截图和缩放/ROI 保持未复测。 |

**构建和回归：** `dotnet build 'PRISM Utility/PrismUtility.csproj' -c Release -p:Platform=x64 -p:PrismVisualQa=false --no-restore` 通过（0 错误、1 条既有 WindowsAppSDK `PublishSingleFile` 建议）；隔离 `PrismVisualQa=true` 构建通过；`dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' -c Release -p:Platform=x64 --no-restore` 最终 **1530/1530 通过**。变更 C# 文件 LSP 无诊断，PowerShell 解析和 `git diff --check` 通过；XAML/RESW/PS1 无已配置 LSP，以原生构建和脚本解析补验。一次早期全量运行中 UI003 探针失败，单独复测及最终全量均通过；不将这次波动隐去。

**仍未验证：** 宽版和较小窗口/放大文字的完整可达性、英文与其他主题/DPI、B5/B6/M2 的原生矩阵、真实 RAW 和实机运动/停止。运行过程中产生的独立失败诊断目录仍保留，未把失败改写为通过；没有发送任何物理运动命令，也没有重写 QA 平台。
