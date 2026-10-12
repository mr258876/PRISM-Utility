# Scan Debug 工作台改版

基线：dev `37689d698721cb2eee512c0c063dd31c22ad50ca`。当前用户授权按本次设计调整原生页面，包括观察冻结与单槽基线。未发现 AGENTS.md；工作区初始为空、克隆后无本地修改。

## 修改前迁移清单

| 原入口 | 新入口 | 命令与真实状态来源 |
| --- | --- | --- |
| 顶部配置身份、配置文件操作 | 保留 | FilmProfile workspace／文件协调服务 |
| 固定运行栏三操作 | 保留 | StartScanCommand／StopScanCommand／StopAllMotorsCommand |
| 四任务与次要折叠字段 | 保留 | 原绑定、运行 gate 和硬件会话 |
| 黑白场细节中的通道保存 | 左栏固定底部 | SaveChannelProfileCommand；保留本地校准库语义 |
| 候选摘要与靠后审查卡 | 审查卡前置、摘要跳转 | Accept／AcceptAndSave／Restore／Cancel 原四命令 |
| 右侧检查中的采样来源、比较 | 观察区常显摘要、详情仍可展开 | 原 CaptureEvidence；显示对象自身的采样来源 |
| RAW 状态、统计 | 观察区直接读数 | ScanRawSignalResult，冻结时保存其独立副本 |
| 图像／瀑布、缩放、像素检查、显示工具 | 保留，冻结限制取值 | 原 Win2D 与 decoder 路径 |
| 无冻结与基线 | 冻结显示、设置／查看／清除基线 A | 纯显示副本；不发送硬件、保存或采集命令 |

## 实现与边界

| 设计要求 | 实现 | 证据 |
| --- | --- | --- |
| 观察对象与编辑对象分开 | ObservationFrame／ObservationRawSignalResult、来源摘要、通道不匹配与参数写入记录变化提示 | 编译后成员审查、RAW UI 契约 |
| 不混淆保存对象 | 黑白场固定底部使用唯一 SaveChannelProfileCommand；保留顶部 JSON 操作，补充作用域文案 | 完整命令与控件清单契约 |
| 四种候选处理保持独立 | 原审查卡前置，原四命令不变 | 页面清单及校准源契约 |
| 冻结／单槽基线 | ScanObservationSnapshot 复制像素、profile、histogram；保留 capture evidence、行号与范围；切换 A／当前，清除 A | 9 个副本测试；2 个新增 ViewModel 行为测试已编译，未在 Linux 执行 |
| 冻结与采集分开 | 设备／采集逻辑继续使用 PreviewFrame；Page 读取 ObservationFrame；冻结命令不触发设备操作 | 原生 VM 编译、源审查 |
| 测量范围与算法范围分开 | 独立观察输入与单通道图像拖选；RAW 分析使用观察范围；显式填入 ADC／Focus 的 owner numeric draft，由原 Apply 提交 | 观察范围行为测试已编译；ROI 拖动与 owner 草稿契约 |
| 不从显示像素算 RAW | 继续使用 packed row 和 ScanRawSignalAnalyzer；反相仅在 Win2D DrawImage 使用 InvertEffect | RAW 绘图契约、Win2D 元数据核对 |
| 清晰的快照状态 | 冻结/基线固定行号、来源、统计与底部来源；禁止从快照向实时缓冲取像素/列均值；隐藏实时列读数；Gamma/白点保留实时处理范围 | VM 编译、绑定审查、源审查 |

原会话、仲裁、自动校准/对焦算法、配置格式、DNG 格式和硬件协议没有重写。新增状态在页面 Deactivate 时释放，Cleanup 仍复用原 Deactivate 链路。没有升级项目 NuGet 或目标框架。

为了避免同时复制两张超大预览造成额外内存峰值，每个显示副本最多 64 MiB；超限会提供原因，采集继续。基线通过 A/当前切换比较，没有新增历史库、自动参数推荐或对焦示意曲线。图像复合坐标不直接作为 RAW 观察拖选坐标；复合图仍可用 RAW 数字范围测量，并标明 RAW 通道和坐标。

## 实际验证

Linux x64；临时工具链 .NET 8.0.415 与 .NET 9.0.306。工具链装在任务临时目录，未改变仓库依赖。

| 检查 | 状态 | 结果与限制 |
| --- | --- | --- |
| Core 构建 | PASS | MSBuild Core `-restore -t:Build -p:EnableWindowsTargeting=true -p:Platform=x64` |
| 托管测试源码与 ViewModel 编译 | PASS | .NET 9 MSBuild 测试项目 `-t:Compile -p:EnableWindowsTargeting=true -p:UseWinUI=false -p:Platform=x64`，生成真实 source-generator 命令与属性；不等于 WinUI XAML 构建 |
| 页面 ViewModel 绑定 | PASS | 367 个成员全部可在上述编译元数据中解析；Win2D 1.4.0 存在 InvertEffect |
| XML／双语资源／diff | PASS | XAML、resw 均可解析；无重复资源键；新增观察资源占位符成对；`git diff --check` 无问题 |
| 可移植断言检查 | PASS / 已有缺失 | 将相关既有 xUnit 方法和新增 snapshot tests 编入临时 net8.0 程序，按 Fact/Theory 数据逐项执行真实 xUnit 断言。当前 211 通过、7 失败；这 7 个都读取仓库未提供的历史验收文件，基线也失败。不是全量 Windows xUnit 运行 |
| 完整测试项目 Build | ENVIRONMENT_BLOCKED | Linux 无法执行 Windows `makepri.exe`；源码已生成并编译为测试 DLL |
| 原生应用 Build/Compile | ENVIRONMENT_BLOCKED | 常规构建首先缺预编译 DngSdkWarpper.dll；只作诊断、临时禁用原生打包要求后，Windows `mt.exe` 在 Linux 仍不能执行。没有把诊断参数写回 csproj |
| WinUI 运行、主题、缩放、键盘、实机 | NEEDS_DEVICE_VALIDATION / NOT_RUN | 没有 Windows 桌面或扫描仪；未生成或声称原生截图和实机成功 |

源码契约的更新仅反映新观察绑定、候选卡位置、增加的 footer 行以及新增入口；保留了原功能清单、owner ROI、释放指针、生命周期与命令独立性约束。没有删除或跳过既有失败用例。

可移植程序集纳入 ScanObservationSnapshotTests、ScanWorkbenchPreviewLayoutTests、ScanDebugRawSignalUiSourceTests、ScanDebugTaskRailLayoutSourceTests、FilmProfileSourceContractTests、ScanDebugSampleActionLabelTests、ScanDebugGeometryUi007CharacterizationTests 与 ScanDebugFilmProfileOrchestrationSourceTests。完整检查日志随补丁包提供。

## Windows 最小验收

1. 用现有 WinUI 工具链构建应用与测试；运行新增 Observation 行为测试和相关 RAW／ROI／布局测试。
2. 在无设备状态检查四任务、文件入口、工程工具、禁用原因；核对中英文、浅深色、窄窗口和低高度。
3. 连接扫描仪后采样并核对来源；连续采集时冻结，确认进度继续；设 A，恢复，重新采样，切换 A/当前，核对各自行号与来源。
4. 修改/应用 ADC 后旧样本提示重新采样；切换校准通道后提示数据通道不同。
5. 观察拖选只改变 RAW 测量；填入算法目标输入后，原配置范围保持不变，只有原 Apply 才提交。
6. 检查候选四入口、停止采集、全部电机停止、手动对焦松开/捕获丢失、导出及原生 DNG；返回页面后旧快照已释放。

## 后续修复：Gamma 控件启用条件

用户在 Windows 构建时报告 ScanDebugPage.xaml 的 StackPanel.IsEnabled 不存在。此前 XML 和 ViewModel 绑定检查没有校验 WinUI 控件属性，漏掉了这个错误。

移除无效的外层 StackPanel，将 IsObservationLive 直接绑定到 Gamma ToggleSwitch；Gamma TextBox 改用 CanEditObservationGamma，即实时显示且 Gamma 已开启。该属性在 Gamma 开关变化、冻结/恢复、基线切换/清除时发出通知，包括尚无有效扫描缓冲区的情况。

修复后托管 ViewModel 与测试源码编译通过；368 个 ViewModel 绑定均可解析。基于实际 Microsoft.WinUI.dll 的属性和基类元数据核对页面 44 处 IsEnabled，均有效；用修复前的页面运行同一核对，准确检出 StackPanel.IsEnabled。

新增页面回归检查通过；可移植断言结果为 212 通过、7 个与基线相同的历史验收文件缺失失败。新增 Gamma 状态/通知行为测试已编译，需在 Windows 执行；完整 WinUI XAML 构建和 UI 运行仍未在 Linux 验证。补丁包附带从上一版 ab6c89c 到本次修复的增量补丁，已应用上一版时仅需应用该增量。
