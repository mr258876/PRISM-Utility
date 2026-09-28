# PRISM ScanDebug 01V-3 第三轮结项报告

## 基线与范围

- 分支：`dev`。实施前 HEAD：`a5186c10f9e76b0d6da0061738d91611ded6fc02`。
- 初始用户未跟踪文件：[01V-3-closeout.md](01V-3-closeout.md)，本报告按其中第三轮授权分别记录 BW1、TXT1、QA1。保留该任务书原状；本轮新增捕获辅助脚本和隔离证据目录。
- 报告仅覆盖本轮授权，不扩展阶段 01 至 04，不包含设备命令。当前本地构建产物与源代码、HEAD 的逐项关联没有独立认证。

## 结果摘要

| 要求 | 结果 | 边界 |
| --- | --- | --- |
| BW1 默认两栏首屏 | REVISE | 六组内容均出现，但两栏“查看问题”按钮下缘略被参数视口裁切；展开后实际问题条目仍不可见，整体不通过。 |
| TXT1 数值排版 | PASS | 仅限本轮实测的中文、深色主题、192 DPI 两种尺寸与指定 Motion/ROI 字段。 |
| QA1 任务记忆 | 部分观察，未完全通过 | QA 专用每次加载强制选任务已可关闭；同一构建返回 ScanDebug 后任务仍是黑白场，但未应用的编辑值和状态清空。跨 Shell 草稿保留不属于已明示契约，不能据此称产品缺陷或完全通过。 |

## BW1：首屏 REVISE，查看问题条目可见性 FAIL

最终默认两栏黑白场 Root 截图显示六组内容：当前逻辑通道与 LED 映射、黑值和白值两个 ADU 输入、Apply/Revert、三个自动动作、带活动区及屏蔽区角色和宽度的结构化 ROI 摘要及编辑入口，以及一个阻塞问题摘要和“查看问题”入口。自动动作和 Apply 在离线状态禁用，Revert 可用。独立视觉复核发现两栏入口按钮的文字可读，但按钮下缘贴着参数视口边界被轻微裁切，未达到“完整可见”；宽版无此问题。不能把默认两栏首屏写成完整 PASS。

最终六张 Root PNG 集中包含展开“查看问题”后的状态。调用入口后展开器已打开，但图像只显示“当前配置验证”标题，没有实际问题条目；UIA 找到的问题按钮仍为 offscreen。UIA 调用时曾报告该按钮获键盘焦点，但截图后焦点已转移至其他按钮，不能证明条目可见或保持可见焦点。因此问题条目可见性明确 **FAIL**，不能把六图集称为全面视觉 PASS。

已按授权做过一次产品滚动修正，并停止在规定范围内。两栏 977 x 604 DIP 下，最小剩余改动是让外层滚动器定位到第一个实际问题条目而不只停在验证卡标题，并重新验证可见性与焦点；默认参数区还需在不缩字号、不损失预览的前提下收回按钮下缘所需的少量空间。本报告不实施这些改动，也不自动开启下一轮。

测试输入 `65536` 只作为未应用文本草稿，不是已验证非法值：未观察到解析结果或新的输入错误，Apply 离线禁用，不能声称输入校验通过或失败。在两栏 977 x 604 DIP 的此状态下，`ManualReferenceStatusTextBlock` 将“草稿”拆成“草 / 稿。”；剩余局部改动是缩短这条状态说明或按语义分行，而非缩小全局字号。草稿多占一行后 ROI/验证自然滚出首屏，按任务书的非默认密集状态规则允许滚动，但问题入口经点击后仍须真正显示问题条目。

证据：[最终报告与六图清单](01V-evidence/01V-3-final-20260928/README.md)、[最终 manifest](01V-evidence/01V-3-final-20260928/root-baseline.json)。六图：[两栏黑白场默认](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-root-default-empty.png)、[两栏 Motion 默认](01V-evidence/01V-3-final-20260928/two-column-Motion-root-default-empty.png)、[宽版黑白场默认](01V-evidence/01V-3-final-20260928/wide-BlackWhite-root-default-empty.png)、[宽版 Motion 默认](01V-evidence/01V-3-final-20260928/wide-Motion-root-default-empty.png)、[两栏未应用草稿](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-unapplied-draft-root.png)、[两栏查看问题展开状态](01V-evidence/01V-3-final-20260928/two-column-BlackWhite-view-issues-expanded-root.png)。

## TXT1：指定中文深色样本 PASS

中文深色界面中，Motion 的 `0.5 mm`、`0.1 s`、`Z+` 及步数选择器保持完整；黑白场 ROI 的 `7450 px`、`90 px` 数值与单位未拆行。窗口 DPI 为 192，RasterizationScale 为 2。两栏 Root 为 977 x 604 DIP，预览区为 645 x 396 DIP；宽版 Root 为 1305 x 809 DIP，预览区为 950 x 601 DIP。预览尺寸在渲染前后未变，没有通过放大窗口或预览区取得结果。

证据见[最终六图与测量记录](01V-evidence/01V-3-final-20260928/README.md)和[最终 manifest](01V-evidence/01V-3-final-20260928/root-baseline.json)。图像为 `ScanDebugRootGrid` 的 Root-only RenderTargetBitmap，不是 Shell/全窗口截图。已知外部全窗口 `CopyFromScreen`/`PrintWindow` 黑图问题不适合作为有效证据；本轮不以其判断产品界面，也不用 HTML 或更早截图充当当前基线。另见[新鲜修改前基线](01V-evidence/01V-3-inprocess-20260928-head-a5186c1-final/README.md)，该基线同样是 Root-only，并记录了待修排版。

`Motor1: Motor1:` 重复文案仍存在，属于本轮范围外观察，未改。英语、浅色、高对比度、100 至 150 DPI、文字缩放与其他内容密度未覆盖。

## QA1：测试钩子隔离已验证，跨 Shell 草稿不作产品结论

QA 专用构建路径增加了 `PRISM_VISUAL_QA_PRESERVE_TASK_SELECTION=1`，在每次页面加载时选择性绕过“强制选中采样”的测试钩子。该开关只用于隔离 QA 构建和运行，正常默认行为不变。最终同构建 UIA 场景在黑白场输入黑值 `513`、白值 `60000` 后，经 Shell“日志”离开再返回：当前任务仍为“黑白场”；编辑框恢复为空，“仅更改了本地输入，尚未应用到通道草稿”状态消失；原有缺少可编辑档案的禁用原因仍在，Apply 仍禁用，Revert 仍可用。

该观察说明这些未应用输入未跨 Shell 往返保留，不表示其已被应用或持久化。现有明确契约只要求同一 Page 内配置返回，不承诺跨 Shell 草稿保留。因此这是探索性结果，不据此追加产品修复，也不把 QA1 描述为完全 PASS。交互只使用 UIA 选任务、编辑文本和 Shell 导航；没有 Apply、Save、扫描或设备命令。

QA1 最终回执：[run-after-issuefix-retry1/receipt.json](01V-evidence/01V-3-qa1-20260928/run-after-issuefix-retry1/receipt.json)。该轮使用环境变量 `preserveTaskSelection=1`；脚本状态为 `OBSERVED_CROSS_SHELL_MISMATCH` 且退出非零，不能作为草稿保持的绿灯。首次同构建尝试因 ready marker 未及时出现而未完成 UIA 检查；随后重试观察到上述结果。早期 `run-first` 的 ready marker 超时不作为行为结果。

## 构建、测试与原生证据

- `dotnet build 'PRISM Utility/PrismUtility.csproj' -c Release -p:Platform=x64 -p:PrismVisualQa=false --no-restore -v:q`：PASS。
- 同项目 `-p:PrismVisualQa=true -p:OutputPath=bin/x64/Release-01V3Closeout/ -p:IntermediateOutputPath=obj/x64/Release-01V3Closeout/ --no-restore -v:q`：PASS。两个构建各报告一条 WindowsAppSDK `PublishSingleFile` advisory；不影响构建退出成功。
- 修正后聚焦测试：220/220 PASS。
- 隔离 QA handler 修正前，手工参考场景：21/21 PASS。该数值仅描述当时手工场景，不替代修正后的 220 项测试或完整套件。
- 更早一次宽泛过滤测试运行因超时中止；完整测试套件本轮 **NOT_RUN**。历史报告中的 1509/1509 是历史结果，不是本轮复测结果。
- 最终原生 UIA/Root 捕获检查：指定数值与单位局部通过；两栏默认按钮下缘轻微裁切、草稿提示孤字、查看问题条目可见性 FAIL。QA1 首次同构建运行 ready marker 等待超时后重试，回执已记录。
- C# LSP：干净。XAML、RESW 与 PowerShell 无可用 LSP 诊断，不能表述为已通过 LSP。
- 全窗口外部截图不合格，未用于结论。没有真实 RAW、设备或电机动作。

## 要求、实现位置与证据索引

| ID | 实现位置 | 验证证据 |
| --- | --- | --- |
| BW1 | `PRISM Utility/Views/ScanDebugPage.xaml`；`PRISM Utility/Views/ScanDebugPage.xaml.cs`；`PRISM Utility/ViewModels/ScanDebugViewModel.cs`；中英文资源 `PRISM Utility/Strings/zh-CN/Resources.resw`、`PRISM Utility/Strings/en-us/Resources.resw` | [最终六图及 manifest](01V-evidence/01V-3-final-20260928/README.md)；两栏首屏 REVISE，问题条目可见性 FAIL。 |
| TXT1 | `PRISM Utility/Views/ScanDebugPage.xaml`、`PRISM Utility/Strings/{zh-CN,en-us}/Resources.resw` | [最终 Root 截图及测量](01V-evidence/01V-3-final-20260928/README.md)；中文深色、指定数值与两种布局 PASS；草稿状态文本仍有孤字。 |
| QA1 | `PRISM Utility/PrismVisualQaPendingCalibrationHook.cs`；`PrismUtility.Core.Tests/ScanDebugPageReentryUi006SourceContractTests.cs`；`01V-evidence/01V-3-qa1-20260928/qa1-task-memory-uia.ps1` | [最终 UIA 回执](01V-evidence/01V-3-qa1-20260928/run-after-issuefix-retry1/receipt.json)；任务保留，未应用草稿未保留，跨 Shell 草稿保留不属于明确契约。 |

命令语义、硬件协议、校准算法与持久化契约未被此报告授权改变。没有设备命令。未验证项还包括英语、浅色、高对比度、100 至 150 DPI、文字缩放、真实 RAW、硬件行为以及全窗口合成截图。

## 范围外建议

- `Motor1: Motor1:` 重复文案仅记录，不在本轮修改范围。

## 未验证环境

- 英文、浅色及高对比度、100/150 DPI、文字缩放、其他窗口尺寸和真实 RAW：NOT_RUN。
- 全窗口原生截图：外部合成器返回黑图，ENVIRONMENT_BLOCKED；本轮仅用当前构建的 Root-only PNG 判断局部布局。
- 实机采集、校准及电机动作：NEEDS_DEVICE_VALIDATION；本轮未发送设备命令。

## 停止点

- 本轮已尝试一次外层滚动器定位到验证卡，仍未定位到条目；不继续改动或启动第四阶段/下一轮。
- 不提交、不推送。
