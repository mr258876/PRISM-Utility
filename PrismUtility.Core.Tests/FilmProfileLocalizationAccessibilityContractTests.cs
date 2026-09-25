using static PrismUtility.Core.Tests.FilmProfileContractSource;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileLocalizationAccessibilityContractTests
{
    private const string WordJoiner = "\u2060";

    private static readonly string[] VisibleRenameResourceKeys =
    [
        "Shell_ScanDebug.Content",
        "Main_ScanDebugCardTitle.Text",
        "Main_ScanDebugCardText.Text",
        "Main_OpenScanDebugButton.Content",
        "ScanDebug_WorkstationTitle.Text",
        "ScanDebug_WorkstationTitle.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip",
        "ScanDebug_PageRoleDescription.Text",
        "ScanDebug_WorkstationDetailsIconButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
        "ScanDebug_WorkstationDetailsIconButton.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip"
    ];

    private static readonly (string Key, string English, string Chinese)[] RequiredTerminologyResources =
    [
        ("ScanDebug_FilmProfileWorkbenchLifecycleNewButton.Content", "New film profile", "新建胶片配置"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleOpenJsonButton.Content", "Open profile JSON", "打开配置 JSON"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleValidateButton.Content", "Validate profile", "验证配置"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton.Content", "Export configuration JSON", "导出配置 JSON"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleSaveCalibrationLibraryButton.Content", "Save to calibration library", "保存到校准库"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleRemoveCalibrationLibraryButton.Content", "Remove from calibration library", "从校准库移除"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleApplyStagedButton.Content", "Apply staged profile", "应用暂存配置"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleDiscardStagedButton.Content", "Discard staged profile", "丢弃暂存配置"),
        ("ScanDebug_FilmProfileWorkbenchStagedImportTitle.Text", "Staged import review", "暂存导入审查"),
        ("ScanDebug_FilmProfileWorkbenchStagedImportHelpText.Text", "Review the loaded JSON before applying it. Apply replaces the current workspace profile; Discard leaves current settings unchanged.", "应用前请审查已加载的 JSON。应用会替换当前工作区配置；丢弃会保留当前设置不变。"),
        ("ScanDebug_FilmProfileWorkbenchImportError", "Import error", "导入错误"),
        ("ScanDebug_FilmProfileWorkbenchImportResultNoChannels", "No staged profile", "没有暂存配置"),
        ("ScanDebug_Runtime_FilmProfileImportDiscardConfirmationTitle", "Discard pending import result", "丢弃待处理导入结果"),
        ("ScanDebug_Runtime_FilmProfileImportDiscardConfirmationMessage", "The current import result, including any staged profile or import errors, will be discarded before creating a new film profile. Current workspace settings are unchanged until you confirm.", "新建配置会丢弃暂存配置或导入错误。取消则保留当前设置。"),
        ("ScanDebug_FilmProfileWorkbenchImportDiscardConfirmationTitle.Text", "Discard pending import result", "丢弃待处理导入结果"),
        ("ScanDebug_FilmProfileWorkbenchImportDiscardConfirmationMessage.Text", "The current import result, including any staged profile or import errors, will be discarded before creating a new film profile. Current workspace settings are unchanged until you confirm.", "新建配置会丢弃暂存配置或导入错误。取消则保留当前设置。"),
        ("ScanDebug_Runtime_FilmProfileImportReplacementConfirmationTitle", "Replace pending import result", "替换待处理导入结果"),
        ("ScanDebug_Runtime_FilmProfileImportReplacementConfirmationMessage", "Choosing another file will replace the current import result, including any staged profile or import errors. Canceling the file picker keeps the current import result.", "选择其他文件会替换暂存配置或导入错误。取消则保留当前结果。"),
        ("ScanDebug_Runtime_FilmProfileImportReplacementConfirmationOpenButton", "Open profile JSON", "打开配置 JSON"),
        ("ScanDebug_FilmProfileWorkbenchImportReplacementConfirmationTitle.Text", "Replace pending import result", "替换待处理导入结果"),
        ("ScanDebug_FilmProfileWorkbenchImportReplacementConfirmationMessage.Text", "Choosing another file will replace the current import result, including any staged profile or import errors. Canceling the file picker keeps the current import result.", "选择其他文件会替换暂存配置或导入错误。取消则保留当前结果。"),
        ("ScanDebug_FilmProfileWorkbenchSectionMetadata.Text", "1. Profile metadata", "1. 配置元数据"),
        ("ScanDebug_FilmProfileWorkbenchSectionAcquisition.Text", "2. Acquisition setup", "2. 采集设置"),
        ("ScanDebug_FilmProfileWorkbenchSectionRecipe.Text", "3. Scan recipe", "3. 扫描配方"),
        ("ScanDebug_FilmProfileWorkbenchSectionChannelCalibration.Text", "4. Channel calibration", "4. 通道校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionReviewStatus.Text", "5. Review and status", "5. 审查与状态"),
        ("ScanDebug_FilmProfileWorkbenchSectionBasicInfo.Text", "1. Basic info", "1. 基本信息"),
        ("ScanDebug_FilmProfileWorkbenchSectionAcquisitionPlan.Text", "2. Acquisition plan", "2. 采集计划"),
        ("ScanDebug_FilmProfileWorkbenchSectionChannelCalibrationSelector.Text", "3. Channel calibration", "3. 通道校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionLiveCalibration.Text", "4. Live calibration", "4. 实时校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionDeviceSettings.Text", "5. Device settings", "5. 设备设置"),
        ("ScanDebug_FilmProfileWorkbenchSectionEngineeringTools.Text", "6. Engineering tools", "6. 工程工具"),
        ("Scan_AlternateDirectionToggleSwitch.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Alternate motor direction each pass", "每轮交替电机方向"),
        ("ScanDebug_BasicInfoSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Basic info section", "基本信息区域"),
        ("ScanDebug_AcquisitionPlanSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Acquisition plan section", "采集计划区域"),
        ("ScanDebug_ChannelCalibrationSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Channel calibration section", "通道校准区域"),
        ("ScanDebug_LiveCalibrationSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Live calibration section", "实时校准区域"),
        ("ScanDebug_DeviceSettingsSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Device settings section", "设备设置区域"),
        ("ScanDebug_EngineeringToolsSection.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Engineering tools section", "工程工具区域"),
        ("ScanDebug_FilmProfileWorkbenchLiveRuntimeDescription.Text", "Export an existing capture as DNG. This does not save profile JSON or apply parameters to the device.", "将已有采集数据导出为 DNG；不会保存配置 JSON 或向设备应用参数。"),
        ("ScanDebug_FilmProfileWorkbenchLiveProgressLabel.Text", "Progress appears here while a scan is running.", "扫描运行时会在这里显示进度。"),
        ("ScanDebug_FilmProfileWorkbenchCurrentValidationTitle.Title", "Current profile validation", "当前配置验证"),
        ("ScanDebug_FilmProfileWorkbenchCurrentValidationTextTitle.Text", "Current profile validation", "当前配置验证"),
        ("ScanDebug_FilmProfileWorkbenchStagedValidationTitle.Text", "Staged import validation", "暂存导入验证"),
        ("ScanDebug_FilmProfileWorkbenchValidationNotRun", "Not validated", "尚未验证"),
        ("ScanDebug_FilmProfileWorkbenchValidationValid", "Valid", "有效"),
        ("ScanDebug_FilmProfileWorkbenchValidationIssues", "{0} validation issue(s)", "{0} 个验证问题"),
        ("ScanDebug_FilmProfileWorkbenchOperationStatusTitle.Text", "Operation status", "操作状态"),
        ("ScanDebug_FilmProfileWorkbenchDeviceStatusTitle.Text", "Device status", "设备状态"),
        ("ScanDebug_FilmProfileWorkbenchOperationReady", "Ready", "就绪"),
        ("ScanDebug_FilmProfileWorkbenchOperationBusy", "Working...", "正在处理..."),
        ("ScanDebug_FilmProfileWorkbenchOperationSucceeded", "Operation completed", "操作已完成"),
        ("ScanDebug_FilmProfileWorkbenchOperationFailed", "Operation failed: {0}", "操作失败：{0}"),
        ("ScanDebug_FilmProfileWorkbenchOperationCanceled", "Operation canceled.", "操作已取消。"),
        ("ScanDebug_FilmProfileWorkbenchDeviceDisconnected", "Scanner disconnected", "扫描仪未连接"),
        ("ScanDebug_FilmProfileWorkbenchDeviceConnected", "Scanner connected", "扫描仪已连接"),
        ("ScanDebug_FilmProfileWorkbenchPersistenceScopeWorkspace", "Current workspace", "当前工作区"),
        ("ScanDebug_FilmProfileWorkbenchPersistenceScopeJson", "Profile JSON file", "配置 JSON 文件"),
        ("ScanDebug_FilmProfileWorkbenchPersistenceScopeCalibrationLibrary", "Calibration library", "校准库"),
        ("ScanDebug_FilmProfileWorkbenchPersistenceScopeUnsaved", "Unsaved changes", "未保存更改"),
        ("ScanDebug_FilmProfileWorkbenchChannelStatusReady", "{0}: ready", "{0}：就绪"),
        ("ScanDebug_FilmProfileWorkbenchChannelStatusMissingCalibration", "{0}: calibration missing", "{0}：缺少校准"),
        ("ScanDebug_FilmProfileWorkbenchChannelStatusEmpty", "{0}: empty", "{0}：为空"),
        ("ScanDebug_FilmProfileWorkbenchChannelStatusSavedAt", "{0}: saved at {1}", "{0}：保存于 {1}"),
        ("ScanDebug_FilmProfileWorkbenchPreviewEmptyTitle.Text", "No calibration preview", "暂无校准预览"),
        ("ScanDebug_FilmProfileWorkbenchPreviewEmptyDescription.Text", "Open or capture a profile to preview calibration data.", "打开或捕获配置后预览校准数据。"),
        ("ScanDebug_FilmProfileWorkbenchPreviewExpandCollapsed", "Show preview details", "显示预览详情"),
        ("ScanDebug_FilmProfileWorkbenchPreviewExpandExpanded", "Hide preview details", "隐藏预览详情"),
        ("ScanDebug_OpenPreviewButton.Content", "Open preview", "打开预览"),
        ("ScanDebug_OpenPreviewButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Open preview", "打开预览"),
        ("ScanDebug_BackToEditorButton.Content", "Back to editor", "返回编辑器"),
        ("ScanDebug_BackToEditorButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Back to editor", "返回编辑器"),
        ("ScanDebug_PreviewSplitter.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Resize preview", "调整预览大小"),
        ("ScanDebug_PreviewSplitter.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", "Drag or use Left and Right arrow keys to resize the editor and preview panes.", "拖动或使用左右方向键调整编辑器和预览面板大小。"),
        ("ScanDebug_PreviewGammaToggleSwitch.Header", "Preview gamma", "预览伽马"),
        ("ScanDebug_PreviewGammaToggleSwitch.OnContent", "On", "开"),
        ("ScanDebug_PreviewGammaToggleSwitch.OffContent", "Off", "关"),
        ("ScanDebug_PreviewGammaTextBox.Header", "Preview gamma value", "预览伽马值"),
        ("ScanDebug_PreviewGammaTextBox.PlaceholderText", "e.g. 2.2", "例如 2.2"),
        ("ScanDebug_Runtime_StatusFilmProfileImportInvalid", "The selected film profile could not be staged. Review the file and try again.", "所选胶片配置无法暂存。请检查文件后重试。"),
        ("ScanDebug_Runtime_FilmProfileDirtyConfirmationTitle", "Unsaved film profile changes", "胶片配置有未保存更改"),
        ("ScanDebug_Runtime_FilmProfileDirtyConfirmationMessage", "Save or discard changes before opening another profile.", "打开其他配置前请保存或丢弃更改。"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationTitle.Text", "Unsaved film profile changes", "胶片配置有未保存更改"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationMessage.Text", "Save or discard changes before opening another profile.", "打开其他配置前请保存或丢弃更改。"),
        ("ScanDebug_Runtime_FilmProfileDirtyConfirmationStayButton", "Keep editing", "继续编辑"),
        ("ScanDebug_Runtime_FilmProfileDirtyConfirmationDiscardButton", "Discard changes", "丢弃更改"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationStayButton.Content", "Keep editing", "继续编辑"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationDiscardButton.Content", "Discard changes", "丢弃更改"),
        ("ScanDebug_FilmProfileWorkbenchJsonSavePrecise.Content", "Export configuration JSON", "导出配置 JSON"),
        ("ScanDebug_FilmProfileWorkbenchCalibrationLibrarySavePrecise.Content", "Save selected channel calibration to library", "将所选通道校准保存到校准库"),
        ("ScanDebug_FilmProfileWorkbenchCalibrationLibraryRemovePrecise.Content", "Remove selected channel calibration from library", "从校准库移除所选通道校准"),
        ("ScanDebug_CalibrationCopySourceComboBox.Header", "Copy from channel", "复制来源通道"),
        ("ScanDebug_CalibrationCopySourceComboBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Copy calibration source", "复制校准来源"),
        ("ScanDebug_CalibrationCopySourceComboBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", "Select a saved channel to stage its calibration values on the selected channel.", "选择已保存的通道，将其校准值暂存到所选通道。"),
        ("ScanDebug_CopyCalibrationProfileButton.Content", "Copy to selected channel", "复制到所选通道"),
        ("ScanDebug_CopyCalibrationProfileButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Copy calibration to selected channel", "复制校准到所选通道"),
        ("ScanDebug_CopyCalibrationProfileButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", "Copying stages values only. Use Save channel calibration to persist them.", "复制只会暂存数值。保存通道校准后才会写入。"),
        ("ScanDebug_CalibrationCopyHelperText.Text", "Copying stages values for the selected channel only. Use Save channel calibration to persist them.", "复制只会暂存所选通道的数值。保存通道校准后才会写入。"),
        ("ScanDebug_PendingCalibrationCandidateReviewTitle.Text", "Pending calibration candidate", "待审查校准候选"),
        ("ScanDebug_PendingCalibrationCandidateReviewHelperText.Text", "Original device parameters are active during review. Accept applies the candidate to the device only; Accept and Save persists only the captured selected channel.", "审查时设备仍用原始参数。接受仅应用到设备；接受并保存仅保存捕获时所选通道。"),
        ("ScanDebug_PendingCalibrationSelectedChannelLabel.Text", "Selected channel", "所选通道"),
        ("ScanDebug_PendingCalibrationValidationLabel.Text", "Validation", "验证"),
        ("ScanDebug_PendingCalibrationAdcDifferenceLabel.Text", "ADC difference", "ADC 差异"),
        ("ScanDebug_PendingCalibrationBlackDeviationLabel.Text", "Black deviation", "黑电平偏差"),
        ("ScanDebug_PendingCalibrationSaturationLabel.Text", "Saturated pixels", "饱和像素"),
        ("ScanDebug_PendingCalibrationNoiseLabel.Text", "Noise", "噪声"),
        ("ScanDebug_PendingCalibrationAcceptDeviceOnlyButton.Content", "Accept (device only)", "接受（仅设备）"),
        ("ScanDebug_PendingCalibrationAcceptDeviceOnlyButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Accept pending calibration on device only", "仅在设备上接受待处理校准"),
        ("ScanDebug_PendingCalibrationAcceptAndSaveButton.Content", "Accept and Save", "接受并保存"),
        ("ScanDebug_PendingCalibrationAcceptAndSaveButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Accept pending calibration and save selected channel", "接受待处理校准并保存所选通道"),
        ("ScanDebug_PendingCalibrationRestoreOriginalButton.Content", "Restore Original", "恢复原始参数"),
        ("ScanDebug_PendingCalibrationRestoreOriginalButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Restore original calibration parameters", "恢复原始校准参数"),
        ("ScanDebug_PendingCalibrationCancelButton.Content", "Cancel", "取消"),
        ("ScanDebug_PendingCalibrationCancelButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Cancel pending calibration review", "取消待处理校准审查"),
        ("ScanDebug_AdvancedAutofocusTitle.Text", "Advanced Autofocus", "高级自动对焦"),
        ("ScanDebug_AdvancedAutofocusExpander.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Advanced autofocus settings", "高级自动对焦设置"),
        ("ScanDebug_AdvancedAutofocusExpander.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", "Expand to edit autofocus presets, custom algorithm inputs, and bounds.", "展开后编辑自动对焦预设、自定义算法输入和边界。"),
        ("ScanDebug_AutofocusPresetComboBox.Header", "Autofocus preset", "自动对焦预设"),
        ("ScanDebug_AutofocusNote.Text", "Host-side scan-debug autofocus uses the persisted focus motor mapping. Choose a preset for deterministic bounds, or switch to Custom for direct algorithm inputs.", "主机端扫描调试自动对焦会使用已保存的对焦电机映射。选择预设可获得确定性边界，或切换到自定义直接输入算法参数。"),
        ("ScanDebug_AutofocusMaxTiltIterationsTextBox.Header", "Max tilt iterations", "最大倾斜迭代次数"),
        ("ScanDebug_AutofocusPresetComboBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", "Quick, Standard, and Fine fill deterministic autofocus distances and iteration limits; Custom enables manual values.", "快速、标准和精细会填入确定性的自动对焦距离与迭代限制；自定义允许手动输入。"),
        ("ScanDebug_AutofocusMaxZIterationsTextBox.Header", "Max Z iterations", "最大 Z 迭代次数"),
        ("ScanDebug_FocusMappingTitle.Text", "Device focus motor mapping", "设备对焦电机映射"),
        ("ScanDebug_FocusMappingLeftMotorComboBox.Header", "Left focus motor", "左侧对焦电机"),
        ("ScanDebug_FocusMappingRightMotorComboBox.Header", "Right focus motor", "右侧对焦电机"),
        ("ScanDebug_SaveFocusMappingButton.Content", "Save focus mapping", "保存对焦映射"),
        ("ScanDebug_TestLeftFocusMappingButton.Content", "Test left motor", "测试左电机"),
        ("ScanDebug_TestRightFocusMappingButton.Content", "Test right motor", "测试右电机"),
        ("ScanDebug_QuickFocusButton.Content", "Quick focus", "快速对焦"),
        ("ScanDebug_FineFocusButton.Content", "Fine focus", "精细对焦"),
        ("ScanDebug_StopAllFocusButton.Content", "Stop all focus", "停止全部对焦"),
        ("ScanDebug_StopAllMotorsButton.Content", "Stop all motors", "停止全部电机"),
        ("ScanDebug_StopAllMotorsButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Stop all scanner motors", "停止全部扫描仪电机"),
        ("ScanDebug_MotionReadRequiredTextBlock.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Motion state read required", "需要读取运动状态"),
        ("ScanDebug_MotionReadRequiredRefreshButton.Content", "Read motion state", "读取运动状态"),
        ("ScanDebug_Runtime_MotionStateReadRequired", "Motion state is unknown. Read device motion state before starting another motion command.", "运动状态未知。启动其他运动命令前请读取设备运动状态。"),
        ("ScanDebug_Runtime_MotorRoleLeftFocus", "left focus motor", "左侧对焦电机"),
        ("ScanDebug_Runtime_MotorRoleRightFocus", "right focus motor", "右侧对焦电机"),
        ("ScanDebug_Runtime_MotorRoleScanTransport", "film transport motor", "胶片传送电机"),
        ("ScanDebug_Runtime_MotorRoleNone", "no configured semantic role", "没有已配置的语义角色"),
        ("ScanDebug_Runtime_MotorRoleSummary", "{0}: {1}", "{0}：{1}"),
        ("ScanDebug_Runtime_MotorMoveSummary", "{0}: {1}; distance {2} mm; estimated steps {3}; estimated duration {4} s; logical direction {5}; raw direction {6}; physical direction unknown.", $"{{0}}：{{1}}；移动距离 {{2}} mm；预{WordJoiner}计{WordJoiner}步{WordJoiner}数 {{3}}；预计耗时 {{4}} s；逻辑方向 {{5}}；原始方向 {{6}}；物理方向未知。"),
        ("ScanDebug_Runtime_MotorMoveSummaryInvalid", "{0}: {1}; requested move is invalid: {2}", "{0}：{1}；请求移动无效：{2}"),
        ("ScanDebug_Runtime_MotorLogicalDirectionUnmapped", "unmapped", "未映射"),
        ("ScanDebug_Runtime_MotorLogicalDirectionFocusZPositive", "focus Z+", "对焦 Z+"),
        ("ScanDebug_Runtime_MotorLogicalDirectionFocusZNegative", "focus Z-", "对焦 Z-"),
        ("ScanDebug_Runtime_MotorLogicalDirectionTransport", "film transport {0}", "胶片传送 {0}"),
        ("ScanDebug_Runtime_MotorApplyConfigEffect", "Applies the device motor configuration to {0}, then reads motion state. Editor speed, distance, and direction fields are not sent by this command.", "将设备电机配置应用到 {0}，然后读取运动状态。此命令不会发送编辑器中的速度、距离和方向字段。"),
        ("ScanDebug_Runtime_StatusStopAllMotorsSucceeded", "All motor stop commands were dispatched for Motor1, Motor2, and Motor3. Hardware motion state was not verified; read motion state before resuming motion.", "已向 Motor1、Motor2 和 Motor3 派发全部电机停止命令。硬件运动状态尚未验证；恢复运动前请读取运动状态。"),
        ("ScanDebug_Runtime_StatusStopAllMotorsFailed", "Stop all motors failed: {0}", "停止全部电机失败：{0}"),
        ("ScanDebug_Runtime_StatusStopAllMotorsPartial", "Stop all motors completed with failures: {0}. Hardware motion state was not verified; read motion state before resuming motion.", "停止全部电机已完成但存在失败：{0}。硬件运动状态尚未验证；恢复运动前请读取运动状态。"),
        ("ScanDebug_Runtime_StatusStopAllMotorsQuarantined", "Global motor stop outcome is unknown. The command transaction remains quarantined. Unattempted motors: {0}. Hardware motion state was not verified; read motion state before resuming motion.", "全局电机停止结果未知。当前命令事务仍处于隔离状态。未尝试的电机：{0}。硬件运动状态尚未验证；恢复运动前请读取运动状态。"),
        ("ScanDebug_Runtime_AutofocusPresetQuick", "Quick", "快速"),
        ("ScanDebug_Runtime_AutofocusPresetStandard", "Standard", "标准"),
        ("ScanDebug_Runtime_AutofocusPresetFine", "Fine", "精细"),
        ("ScanDebug_Runtime_AutofocusPresetCustom", "Custom", "自定义"),
        ("ScanDebug_Runtime_ErrorAutofocusMappedMechanicsMismatch", "Mapped focus motors must use identical mechanics before autofocus can run.", "映射的对焦电机必须使用相同机械参数，才能运行自动对焦。"),
        ("ScanDebug_Runtime_FocusMappingDefaultStatus", "Default mapping: left Motor1, right Motor3.", "默认映射：左侧 Motor1，右侧 Motor3。"),
        ("ScanDebug_Runtime_FocusMappingSavedStatus", "Focus mapping saved.", "对焦映射已保存。"),
        ("ScanDebug_Runtime_ErrorFocusMappingDuplicateMotors", "Left and right focus motors must be different motors.", "左侧和右侧对焦电机必须不同。"),
        ("ScanDebug_Runtime_PendingCalibrationSelectedChannel", "Captured selected channel: {0}", "捕获时所选通道：{0}"),
        ("ScanDebug_Runtime_PendingCalibrationMetric", "Before {0} -> after {1}", "之前 {0} -> 之后 {1}"),
        ("ScanDebug_Runtime_PendingCalibrationValidationValid", "Valid", "有效"),
        ("ScanDebug_Runtime_PendingCalibrationValidationInvalid", "Invalid", "无效"),
        ("ScanDebug_Runtime_PendingCalibrationValidationWarning", "Valid with warning(s)", "有效但有警告"),
        ("ScanDebug_Runtime_PendingCalibrationValidationIssues", "Issues: {0}", "问题：{0}"),
        ("ScanDebug_Runtime_PendingCalibrationValidationNoIssues", "No validation issues.", "没有验证问题。"),
        ("ScanDebug_Runtime_ChannelStatusSaved", "Saved", "已保存"),
        ("ScanDebug_Runtime_ChannelStatusModified", "Modified", "已修改"),
        ("ScanDebug_Runtime_ChannelStatusInvalid", "Invalid", "无效"),
        ("ScanDebug_Runtime_ChannelStatusMissing", "Missing", "缺少"),
        ("ScanDebug_Runtime_ChannelStatusCopiedUnverified", "Copied-Unverified", "已复制待验证"),
        ("ScanDebug_Runtime_ChannelStatusUnconfigured", "Missing", "缺少"),
        ("ScanDebug_FilmProfileWorkbenchBasicMetadataTitle.Text", "Profile identity", "配置身份"),
        ("ScanDebug_FilmProfileWorkbenchBasicMetadataDescription.Text", "Edit the film-level name and document output choices. Changes stay in the workspace until you save or apply.", "编辑胶片级名称和文档输出选项。更改会保留在工作区中，直到保存或应用。"),
        ("ScanDebug_FilmProfileWorkbenchBasicRecipeTitle.Text", "Color and scan recipe", "色彩与扫描配方"),
        ("ScanDebug_FilmProfileWorkbenchBasicRecipeDescription.Text", "These values come from the active profile snapshot and update the draft immediately, even while the scanner is offline.", "这些值来自活动配置快照，并会立即更新草稿，即使扫描仪离线也是如此。"),
        ("ScanDebug_FilmProfileWorkbenchBasicValidationTitle.Text", "Current save and validation state", "当前保存与验证状态"),
        ("ScanDebug_FilmProfileWorkbenchBasicValidationDescription.Text", "Validation lists exact field paths with actionable messages before Save or Apply can proceed.", "验证会列出准确字段路径和可操作消息，之后才能继续保存或应用。"),
        ("ScanDebug_FilmProfileWorkbenchBasicProfileNameTextBox.Header", "Profile name", "配置名称"),
        ("ScanDebug_FilmProfileWorkbenchBasicProfileNameTextBox.PlaceholderText", "e.g. 5207", "例如 5207"),
        ("ScanDebug_FilmProfileWorkbenchBasicColorManagementToggleSwitch.Header", "Use profile color management", "使用配置色彩管理"),
        ("ScanDebug_FilmProfileWorkbenchBasicColorManagementToggleSwitch.OnContent", "On", "开"),
        ("ScanDebug_FilmProfileWorkbenchBasicColorManagementToggleSwitch.OffContent", "Off", "关"),
        ("ScanDebug_FilmProfileWorkbenchBasicRedWavelengthTextBox.Header", "Red wavelength (nm)", "红色波长 (nm)"),
        ("ScanDebug_FilmProfileWorkbenchBasicGreenWavelengthTextBox.Header", "Green wavelength (nm)", "绿色波长 (nm)"),
        ("ScanDebug_FilmProfileWorkbenchBasicBlueWavelengthTextBox.Header", "Blue wavelength (nm)", "蓝色波长 (nm)"),
        ("ScanDebug_FilmProfileWorkbenchBasicOutputGammaTextBox.Header", "Output gamma", "输出伽马"),
        ("ScanDebug_FilmProfileWorkbenchBasicTargetWhitePointComboBox.Header", "Target white point", "目标白点"),
        ("ScanDebug_FilmProfileWorkbenchBasicTargetWhitePointD65ComboBoxItem.Content", "D65", "D65"),
        ("ScanDebug_FilmProfileWorkbenchBasicTargetWhitePointD50ComboBoxItem.Content", "D50", "D50"),
        ("ScanDebug_FilmProfileWorkbenchBasicTargetWhitePointManualComboBoxItem.Content", "Manual color temperature", "手动色温"),
        ("ScanDebug_FilmProfileWorkbenchBasicManualWhitePointColorTemperatureTextBox.Header", "Manual color temperature (K)", "手动色温 (K)"),
        ("ScanDebug_FilmProfileWorkbenchBasicProfileNameTextBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "Profile name", "配置名称"),
        ("Scan_Runtime_ProfileDngExportModeLinearRaw4", "4-channel raw DNG", "4 通道原始 DNG"),
        ("Scan_Runtime_ProfileDngExportModeLinearRgbIrw", "RGB + IR/W DNG", "RGB + IR/W DNG"),
        ("FilmProfile_Validation_NumberInvalid", "Enter a valid number for {0}.", "请为 {0} 输入有效数字。")
    ];

    [Fact]
    public void ResourceFiles_AreValidXmlAndHaveNoDuplicateDataNames()
    {
        foreach (var culture in new[] { "en-us", "zh-CN" })
        {
            var document = ReadResourceDocument(culture);
            var duplicates = document
                .Descendants("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.Empty(duplicates);
        }
    }

    [Fact]
    public void ScanDebugDisplayRenameBaseline_CurrentRouteResourceNamesArePaired()
    {
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");
        var shell = ReadAppText("Views", "ShellPage.xaml");
        var main = ReadAppText("Views", "MainPage.xaml");
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");
        var requiredKeys = VisibleRenameResourceKeys.Concat(new[]
        {
            "ScanDebug_SaveJsonButton.Content",
            "ScanDebug_LoadJsonButton.Content",
            "ScanDebug_FilmProfileWorkspaceSummaryTitle.Text",
            "ScanDebug_FilmProfileWorkspaceSummaryHelpText.Text",
            "ScanDebug_FilmProfileWorkspaceNameLabel.Text",
            "ScanDebug_FilmProfileWorkspaceDirtyLabel.Text",
            "ScanDebug_FilmProfileWorkspaceValidationLabel.Text"
        }).ToArray();

        Assert.Contains("helpers:NavigationHelper.NavigateTo=\"ScanDebug\"", shell, StringComparison.Ordinal);
        Assert.Contains("Main_ScanDebugCardTitle", main, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_WorkstationTitle", xaml, StringComparison.Ordinal);
        foreach (var key in requiredKeys)
        {
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }
    }

    [Fact]
    public void FilmProfileAndCalibrationVisibleTerminology_ReplacesScanDebuggingOnlyInShellMainAndPageCopy()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Film Profile and Calibration", english["Shell_ScanDebug.Content"]);
        Assert.Equal("胶片配置与校准", chinese["Shell_ScanDebug.Content"]);
        Assert.Equal("Film Profile and Calibration", english["Main_ScanDebugCardTitle.Text"]);
        Assert.Equal("胶片配置与校准", chinese["Main_ScanDebugCardTitle.Text"]);
        Assert.Equal("Open Film Profile and Calibration", english["Main_OpenScanDebugButton.Content"]);
        Assert.Equal("进入胶片配置与校准", chinese["Main_OpenScanDebugButton.Content"]);
        Assert.Equal("Film Profile and Calibration", english["ScanDebug_WorkstationTitle.Text"]);
        Assert.Equal("胶片配置与校准", chinese["ScanDebug_WorkstationTitle.Text"]);

        foreach (var key in VisibleRenameResourceKeys)
        {
            Assert.DoesNotContain("Scan Debug", english[key], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("扫描调试", chinese[key], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FilmProfileAndCalibrationTerminologyFoundation_HasRequiredPairedKeysAndPlaceholders()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        foreach (var (key, expectedEnglish, expectedChinese) in RequiredTerminologyResources)
        {
            Assert.True(english.TryGetValue(key, out var englishValue), $"Missing en-us resource: {key}");
            Assert.True(chinese.TryGetValue(key, out var chineseValue), $"Missing zh-CN resource: {key}");
            Assert.Equal(expectedEnglish, englishValue);
            Assert.Equal(expectedChinese, chineseValue);
            Assert.Equal(GetPlaceholderIndexes(englishValue), GetPlaceholderIndexes(chineseValue));
        }
    }

    [Fact]
    public void ScanDebugRuntimeLimitChineseCopy_UsesCompactSemanticCurrentPhrase()
    {
        var chinese = ReadResources("zh-CN");

        Assert.Equal("{0}：{1} ~ {2}", chinese["ScanDebug_Runtime_LimitBounded"]);
        Assert.Equal("{0}：{1} ~ {2}；需为整数", chinese["ScanDebug_Runtime_LimitBoundedInvalidInteger"]);
        Assert.Equal("{0}：{1} ~ {2}；{3} 超范围", chinese["ScanDebug_Runtime_LimitBoundedCurrentOutOfRange"]);
        Assert.Equal("{0}：{1} ~ {2}；{3}", chinese["ScanDebug_Runtime_LimitBoundedCurrent"]);
        Assert.DoesNotContain("当前", chinese["ScanDebug_Runtime_LimitBoundedCurrent"], StringComparison.Ordinal);
        Assert.DoesNotContain("当前", chinese["ScanDebug_Runtime_LimitBoundedCurrentOutOfRange"], StringComparison.Ordinal);
        Assert.DoesNotContain("现值", chinese["ScanDebug_Runtime_LimitBoundedCurrent"], StringComparison.Ordinal);
        Assert.DoesNotContain("现值", chinese["ScanDebug_Runtime_LimitBoundedCurrentOutOfRange"], StringComparison.Ordinal);
        Assert.DoesNotContain("值：", chinese["ScanDebug_Runtime_LimitBoundedCurrent"], StringComparison.Ordinal);
        Assert.DoesNotContain("值：", chinese["ScanDebug_Runtime_LimitBoundedCurrentOutOfRange"], StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugMotorMoveChineseCopy_KeepsStepPhrasesTogetherForConstrainedVisuals()
    {
        var chinese = ReadResources("zh-CN");

        Assert.Contains($"预{WordJoiner}计{WordJoiner}步{WordJoiner}数", chinese["ScanDebug_Runtime_MotorMoveSummary"], StringComparison.Ordinal);
        Assert.Contains($"步{WordJoiner}数必须", chinese["ScanDebug_Runtime_ErrorMotorStepsPositive"], StringComparison.Ordinal);
        Assert.DoesNotContain("预计步数", chinese["ScanDebug_Runtime_MotorMoveSummary"], StringComparison.Ordinal);
        Assert.DoesNotContain("步数必须", chinese["ScanDebug_Runtime_ErrorMotorStepsPositive"], StringComparison.Ordinal);
    }

    [Fact]
    public void Todo2_FilmProfileDiscardConfirmation_UsesExistingLocalizedResourcesInThePageDialog()
    {
        var page = ReadAppText("Views", "ScanDebugPage.xaml.cs");
        var viewModel = ReadAppText("ViewModels", "ScanDebugViewModel.cs");

        foreach (var key in new[]
        {
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationTitle",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationMessage",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationDiscardButton",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationStayButton"
        })
        {
            Assert.Contains(key, viewModel, StringComparison.Ordinal);
        }

        Assert.Contains("private async void OnFilmProfileDiscardConfirmationRequested", page, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", page, StringComparison.Ordinal);
        Assert.Contains("Title = e.TitleResourceKey.GetLocalized()", page, StringComparison.Ordinal);
        Assert.Contains("Content = e.MessageResourceKey.GetLocalized()", page, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonText = e.PrimaryButtonResourceKey.GetLocalized()", page, StringComparison.Ordinal);
        Assert.Contains("CloseButtonText = e.CloseButtonResourceKey.GetLocalized()", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FilmProfileRuntimeAndWorkspaceKeys_HaveCultureParityAndMatchingPlaceholders()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");
        var englishKeys = GetFilmProfileResourceKeys(english.Keys).ToArray();
        var chineseKeys = GetFilmProfileResourceKeys(chinese.Keys).ToArray();

        Assert.NotEmpty(englishKeys);
        Assert.Empty(englishKeys.Except(chineseKeys, StringComparer.Ordinal));
        Assert.Empty(chineseKeys.Except(englishKeys, StringComparer.Ordinal));

        foreach (var key in englishKeys)
            Assert.Equal(GetPlaceholderIndexes(english[key]), GetPlaceholderIndexes(chinese[key]));
    }

    [Fact]
    public void ScanDebugUids_HaveResourcesInBothCultures()
    {
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");

        foreach (var uid in GetScanDebugXamlUids())
        {
            Assert.Contains(english, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
            Assert.Contains(chinese, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ScanDebugWorkbenchReviewAction_RuntimeAndUidCopyStayPairedInBothLocales()
    {
        foreach (var culture in new[] { "en-us", "zh-CN" })
        {
            var resources = ReadResources(culture);

            Assert.Equal(resources["ScanDebug_WorkbenchReviewButton.Content"], resources["ScanDebug_WorkbenchReviewAction"]);
            Assert.Equal(resources["ScanDebug_WorkbenchPendingReviewButton.Content"], resources["ScanDebug_WorkbenchPendingReviewAction"]);
        }
    }

    [Fact]
    public void ScanDebugStopButton_UsesScanOnlyCopyDistinctFromStopAllMotors()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Stop Scan", english["ScanDebug_StopButton.Content"]);
        Assert.Equal("停止采集", chinese["ScanDebug_StopButton.Content"]);
        Assert.Equal("Stop all motors", english["ScanDebug_StopAllMotorsButton.Content"]);
        Assert.Equal("停止全部电机", chinese["ScanDebug_StopAllMotorsButton.Content"]);
        Assert.NotEqual(english["ScanDebug_StopButton.Content"], english["ScanDebug_StopAllMotorsButton.Content"]);
        Assert.NotEqual(chinese["ScanDebug_StopButton.Content"], chinese["ScanDebug_StopAllMotorsButton.Content"]);
    }

    [Fact]
    public void Stage01ZoomButtons_HavePairedLocalizedTooltipsAndAutomationNames()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        foreach (var (uid, expectedEnglish, expectedChinese) in new[]
        {
            ("ScanDebug_ZoomOutButton", "Zoom out", "缩小预览"),
            ("ScanDebug_ZoomInButton", "Zoom in", "放大预览")
        })
        {
            var tooltipKey = $"{uid}.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip";
            var nameKey = $"{uid}.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name";

            Assert.Equal(expectedEnglish, english[tooltipKey]);
            Assert.Equal(expectedEnglish, english[nameKey]);
            Assert.Equal(expectedChinese, chinese[tooltipKey]);
            Assert.Equal(expectedChinese, chinese[nameKey]);
        }
    }

    [Fact]
    public void ScanDebugStopDisabledReason_HasSpecificNoScanCopyInBothLocales()
    {
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("No scan is running to stop.", english["ScanDebug_DisabledReasonNoActiveScan"]);
        Assert.Equal("当前没有正在进行的采集。", chinese["ScanDebug_DisabledReasonNoActiveScan"]);
        Assert.NotEqual(english["ScanDebug_DisabledReasonUnavailable"], english["ScanDebug_DisabledReasonNoActiveScan"]);
        Assert.NotEqual(chinese["ScanDebug_DisabledReasonUnavailable"], chinese["ScanDebug_DisabledReasonNoActiveScan"]);
    }

    [Fact]
    public void ScanDebugCurrentValidationTitleUids_SeparateTextBlockTextResourcesFromInfoBarTitleResources()
    {
        const string infoBarUid = "ScanDebug_FilmProfileWorkbenchCurrentValidationTitle";
        const string textBlockUid = "ScanDebug_FilmProfileWorkbenchCurrentValidationTextTitle";
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");

        Assert.Equal(2, CountOccurrences(xaml, $"x:Uid=\"{textBlockUid}\""));
        Assert.DoesNotContain($"<TextBlock x:Uid=\"{infoBarUid}\"", xaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(xaml, $"<InfoBar x:Uid=\"{infoBarUid}\""));

        foreach (var (culture, expectedTitle) in new[]
        {
            ("en-us", "Current profile validation"),
            ("zh-CN", "当前配置验证")
        })
        {
            var resources = ReadResources(culture);
            var names = resources.Keys.ToHashSet(StringComparer.Ordinal);

            Assert.True(resources.TryGetValue($"{textBlockUid}.Text", out var textBlockTitle), $"Missing {culture} TextBlock title resource.");
            Assert.Equal(expectedTitle, textBlockTitle);
            Assert.DoesNotContain($"{textBlockUid}.Title", names);

            Assert.True(resources.TryGetValue($"{infoBarUid}.Title", out var infoBarTitle), $"Missing {culture} InfoBar title resource.");
            Assert.Equal(expectedTitle, infoBarTitle);
            Assert.DoesNotContain($"{infoBarUid}.Text", names);
        }
    }

    [Fact]
    public void RuntimeMessageKeys_HaveResourcesInBothCultures()
    {
        var runtimeKeys = ExtractRuntimeMessageKeys(
            ReadAppText("ViewModels", "ScanDebugViewModel.cs"),
            ReadAppText("ViewModels", "ScanViewModel.cs"),
            ReadCoreText("Services", "ScanFilmProfileDocumentService.cs"),
            ReadCoreText("Services", "ScanFilmProfileDocumentNormalization.cs"),
            ReadCoreText("Models", "ScanFilmProfileDraft.cs"));
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");
        var englishResources = ReadResources("en-us");
        var chineseResources = ReadResources("zh-CN");

        foreach (var key in runtimeKeys)
        {
            var resourceKey = key.Replace('.', '_');
            Assert.Contains(resourceKey, english);
            Assert.Contains(resourceKey, chinese);
        }

        foreach (var key in new[]
        {
            "ScanDebug_Runtime_StatusFilmProfileExported",
            "ScanDebug_Runtime_StatusFilmProfileExportCanceled",
            "ScanDebug_Runtime_StatusFilmProfileInvalid",
            "ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary",
            "ScanDebug_Runtime_StatusSaveFilmProfileFailed",
            "ScanDebug_Runtime_StatusLoadFilmProfileCanceled",
            "ScanDebug_Runtime_StatusFilmProfileLoaded",
            "ScanDebug_Runtime_StatusFilmProfileStaged",
            "ScanDebug_Runtime_StatusFilmProfileApplied",
            "ScanDebug_Runtime_StatusFilmProfileImportInvalid",
            "ScanDebug_Runtime_StatusLoadFilmProfileFailed"
        })
        {
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }

        Assert.DoesNotContain("loaded", englishResources["ScanDebug_Runtime_StatusFilmProfileStaged"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("applied", englishResources["ScanDebug_Runtime_StatusFilmProfileStaged"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("staged", englishResources["ScanDebug_Runtime_StatusFilmProfileStaged"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applied", englishResources["ScanDebug_Runtime_StatusFilmProfileApplied"], StringComparison.OrdinalIgnoreCase);
        Assert.Empty(GetPlaceholderIndexes(englishResources["ScanDebug_Runtime_StatusFilmProfileImportInvalid"]));
        Assert.Empty(GetPlaceholderIndexes(chineseResources["ScanDebug_Runtime_StatusFilmProfileImportInvalid"]));

        Assert.Equal(
            GetPlaceholderIndexes(englishResources["ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary"]),
            GetPlaceholderIndexes(chineseResources["ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary"]));
    }

    [Fact]
    public void ValidationCodesAndFields_HaveRuntimeMappingsAndResources()
    {
        var validationModel = ReadCoreText("Models", "ScanFilmProfileDraft.cs");
        var presenter = ReadAppText("Helpers", "FilmProfileValidationTextPresenter.cs");
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");
        var codes = ExtractEnumNames(validationModel, "ScanFilmProfileValidationCode");
        var expectedValidationResourceKeys = new[]
        {
            "FilmProfile_Validation_DocumentMissing",
            "FilmProfile_Validation_ChannelProfilesMissing",
            "FilmProfile_Validation_MalformedJson",
            "FilmProfile_Validation_SchemaVersionMissing",
            "FilmProfile_Validation_SchemaVersionUnsupported",
            "FilmProfile_Validation_ProfileNameMissing",
            "FilmProfile_Validation_ProfileNameInvalid",
            "FilmProfile_Validation_SavedAtUtcInvalid",
            "FilmProfile_Validation_ChannelRoleInvalid",
            "FilmProfile_Validation_ChannelParametersInvalid",
            "FilmProfile_Validation_ChannelRoleDuplicate",
            "FilmProfile_Validation_NoValidChannelProfiles",
            "FilmProfile_Validation_ColorManagementInvalid",
            "FilmProfile_Validation_AlignmentModeInvalid",
            "FilmProfile_Validation_DngExportModeInvalid",
            "FilmProfile_Validation_AcquisitionInputInvalid",
            "FilmProfile_Validation_RoiInputInvalid",
            "FilmProfile_Validation_ScanRecipeInputInvalid",
            "FilmProfile_Validation_GenericInputInvalid"
        };

        Assert.Equal(16, codes.Length);
        foreach (var key in expectedValidationResourceKeys)
        {
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }

        Assert.Contains("GetValidationIssueText", presenter, StringComparison.Ordinal);
        Assert.Contains("ToResourceKey", presenter, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationPresenter_UsesLocalizedGenericFallbackForUnknownKeys()
    {
        var presenter = ReadAppText("Helpers", "FilmProfileValidationTextPresenter.cs");
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");

        Assert.Contains("FilmProfile_Validation_GenericInputInvalid", presenter, StringComparison.Ordinal);
        Assert.Contains("GetLocalizedOrFallback", presenter, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceKey.GetLocalized()", presenter, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceKey.GetLocalizedFormat(", presenter, StringComparison.Ordinal);
        Assert.Contains("FilmProfile_Validation_GenericInputInvalid", english);
        Assert.Contains("FilmProfile_Validation_GenericInputInvalid", chinese);
    }

    [Fact]
    public void ScanDebugValidationSummary_UsesLocalizedRuntimeIssues()
    {
        var scanDebugSource = ReadAppText("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("FilmProfileValidationTextPresenter.GetValidationIssueText", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("FilmProfileValidationSummary", scanDebugSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileValidationIssues.Select(issue => issue.MessageKey)", scanDebugSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4CjkConfirmationCopy_IsConciseAndRuntimeXamlCopiesStaySynchronized()
    {
        var chinese = ReadResources("zh-CN");

        Assert.Equal(
            "选择其他文件会替换暂存配置或导入错误。取消则保留当前结果。",
            chinese["ScanDebug_Runtime_FilmProfileImportReplacementConfirmationMessage"]);
        Assert.Equal(
            chinese["ScanDebug_Runtime_FilmProfileImportReplacementConfirmationMessage"],
            chinese["ScanDebug_FilmProfileWorkbenchImportReplacementConfirmationMessage.Text"]);
        Assert.DoesNotContain("文件选择器", chinese["ScanDebug_Runtime_FilmProfileImportReplacementConfirmationMessage"], StringComparison.Ordinal);

        Assert.Equal(
            "新建配置会丢弃暂存配置或导入错误。取消则保留当前设置。",
            chinese["ScanDebug_Runtime_FilmProfileImportDiscardConfirmationMessage"]);
        Assert.Equal(
            chinese["ScanDebug_Runtime_FilmProfileImportDiscardConfirmationMessage"],
            chinese["ScanDebug_FilmProfileWorkbenchImportDiscardConfirmationMessage.Text"]);
        Assert.DoesNotContain("确认前", chinese["ScanDebug_Runtime_FilmProfileImportDiscardConfirmationMessage"], StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4StagedValidValidationInfoBar_UsesLocalizedNonEmptyMessage()
    {
        var scanDebugSource = ReadAppText("ViewModels", "ScanDebugViewModel.cs");
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Valid", english["ScanDebug_FilmProfileWorkbenchValidationValid"]);
        Assert.Equal("有效", chinese["ScanDebug_FilmProfileWorkbenchValidationValid"]);
        Assert.Contains("public string StagedFilmProfileImportValidationSummary =>", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationIssues.Count == 0", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("\"ScanDebug_FilmProfileWorkbenchValidationValid\".GetLocalized()", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("FormatFilmProfileValidationIssues(StagedFilmProfileImportValidationIssues)", scanDebugSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4InvalidImportHighDpiValidationCopy_IsConciseAndKeepsFieldPathSeparate()
    {
        const string key = "FilmProfile_Validation_ChannelParametersInvalid";
        var scanDebugSource = ReadAppText("ViewModels", "ScanDebugViewModel.cs");
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Correct the selected channel calibration parameters before saving.", english[key]);
        Assert.Equal("请修正通道校准参数。", chinese[key]);
        Assert.Equal(GetPlaceholderIndexes(english[key]), GetPlaceholderIndexes(chinese[key]));
        Assert.DoesNotContain("保存前", chinese[key], StringComparison.Ordinal);
        Assert.DoesNotContain("所选", chinese[key], StringComparison.Ordinal);
        Assert.Contains("$\"{issue.FieldPath}: {FilmProfileValidationTextPresenter.GetValidationIssueText(issue)}\"", scanDebugSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo19RoiIssueDisplay_UsesLocalizedMessagesWithTechnicalPathSecondary()
    {
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");
        var viewModel = ReadAppText("ViewModels", "ScanDebugViewModel.cs");
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.AdcRoiValidationIssueDisplays, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.FocusRoiValidationIssueDisplays, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.ImageReferenceRoiValidationIssueDisplays, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding Issue}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"{Binding AutomationId}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Message}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TechnicalPath}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationId = FormatRoiIssueAutomationId(issue)", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.AdcRoiValidationIssues, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.FocusRoiValidationIssues, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{x:Bind ViewModel.ImageReferenceRoiValidationIssues, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.Name=\"{Binding FieldPath}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"{issue.Owner}.{issue.FieldPath}: {issue.Code}\"", viewModel, StringComparison.Ordinal);

        foreach (var key in RequiredRoiIssueResourceKeys())
        {
            Assert.True(english.TryGetValue(key, out var englishValue), $"Missing en-US resource {key}");
            Assert.True(chinese.TryGetValue(key, out var chineseValue), $"Missing zh-CN resource {key}");
            Assert.False(string.IsNullOrWhiteSpace(englishValue), $"Empty en-US resource {key}");
            Assert.False(string.IsNullOrWhiteSpace(chineseValue), $"Empty zh-CN resource {key}");
        }

        foreach (var value in GetVisibleRoiIssueValues(english, chinese))
        {
            Assert.DoesNotContain("EffectiveRange", value, StringComparison.Ordinal);
            Assert.DoesNotContain("ColumnRange", value, StringComparison.Ordinal);
            Assert.DoesNotContain("FocusLeftRange", value, StringComparison.Ordinal);
            Assert.DoesNotContain("OutOfBounds", value, StringComparison.Ordinal);
            Assert.DoesNotContain("TooNarrow", value, StringComparison.Ordinal);
        }

        Assert.Equal("ADC 校准使用有效区和屏蔽区。\n无效输入可继续修改。\n仅限制 ADC 校准，不影响对焦。", chinese["ScanDebug_AdcRoiEditorHelpText.Text"]);
        Assert.Equal("用于自动对焦。\n可设置整体、左侧和右侧范围。\n无效输入可修改，不影响 ADC 校准。", chinese["ScanDebug_FocusRoiEditorHelpText.Text"]);
        Assert.Equal("ROI 已修改，尚未应用。\n请点击“应用 ROI”。", chinese["ScanDebug_Runtime_RoiRangeChanged"]);
        Assert.Equal("{0} - {1}\n{2}", chinese["ScanDebug_Runtime_RoiIssueMessage"]);
        Assert.Equal(GetPlaceholderIndexes(english["ScanDebug_Runtime_RoiIssueMessage"]), GetPlaceholderIndexes(chinese["ScanDebug_Runtime_RoiIssueMessage"]));
        Assert.DoesNotContain("并且", chinese["ScanDebug_AdcRoiEditorHelpText.Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("可编辑文本", chinese["ScanDebug_FocusRoiEditorHelpText.Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("提交", chinese["ScanDebug_Runtime_RoiRangeChanged"], StringComparison.Ordinal);
        Assert.Contains("不影响对焦", chinese["ScanDebug_AdcRoiEditorHelpText.Text"], StringComparison.Ordinal);
        Assert.Contains("不影响 ADC 校准", chinese["ScanDebug_FocusRoiEditorHelpText.Text"], StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01ScanDebugMotorSurface_UsesPersistentRunBarStopAndVisibleReadRequiredBindings()
    {
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Contains("x:Uid=\"ScanDebug_StopAllMotorsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"StopAllMotorsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopAllMotorsCommand}\"", xaml, StringComparison.Ordinal);
        var runStart = xaml.IndexOf("x:Name=\"WorkbenchRunBar\"", StringComparison.Ordinal);
        var stopStart = xaml.IndexOf("x:Uid=\"ScanDebug_StopAllMotorsButton\"", StringComparison.Ordinal);
        var workStart = xaml.IndexOf("x:Name=\"WorkbenchSectionSelectorBar\"", StringComparison.Ordinal);
        Assert.True(
            runStart >= 0 && stopStart > runStart && stopStart < workStart,
            "Stop all motors must remain in the non-scrolling run bar, above the task selector and motor card.");
        Assert.Contains("ScanDebug_StopAllMotorsButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", english.Keys);
        Assert.Contains("ScanDebug_StopAllMotorsButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", chinese.Keys);
        Assert.Contains("ScanDebug_StopButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", english.Keys);
        Assert.Contains("ScanDebug_StopButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText", chinese.Keys);

        Assert.Contains("x:Uid=\"ScanDebug_MotionReadRequiredTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.MotionStateReadRequiredText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.MotionStateReadRequiredVisibility, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_MotionReadRequiredRefreshButton\"", xaml, StringComparison.Ordinal);

        foreach (var property in new[]
        {
            "Motor1RoleText",
            "Motor2RoleText",
            "Motor3RoleText",
            "Motor1MoveSummaryText",
            "Motor2MoveSummaryText",
            "Motor3MoveSummaryText",
            "Motor1ApplyConfigEffectText",
            "Motor2ApplyConfigEffectText",
            "Motor3ApplyConfigEffectText"
        })
        {
            Assert.Contains(property, xaml, StringComparison.Ordinal);
        }

        foreach (var motorNumber in new[] { 1, 2, 3 })
        {
            Assert.Contains($"Text=\"{{x:Bind ViewModel.Motor{motorNumber}MoveValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"Text=\"{{x:Bind ViewModel.Motor{motorNumber}SpeedValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"SelectedItem=\"{{x:Bind ViewModel.Motor{motorNumber}MoveDirection, Mode=TwoWay}}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"SelectedItem=\"{{x:Bind ViewModel.Motor{motorNumber}MoveUnit, Mode=TwoWay}}\"", xaml, StringComparison.Ordinal);
        }

        Assert.Equal("{0}: {1}; requested move is invalid: {2}", english["ScanDebug_Runtime_MotorMoveSummaryInvalid"]);
        Assert.Equal("{0}：{1}；请求移动无效：{2}", chinese["ScanDebug_Runtime_MotorMoveSummaryInvalid"]);
    }

    [Fact]
    public void Todo8EmptyNameFallback_IsPresentationOnlyAndResourcesStayPaired()
    {
        var scanDebugSource = ReadAppText("ViewModels", "ScanDebugViewModel.cs");
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Untitled Film Profile", english["ScanDebug_Runtime_FilmProfileUntitled"]);
        Assert.Equal("未命名胶片配置", chinese["ScanDebug_Runtime_FilmProfileUntitled"]);
        Assert.Contains("public string CurrentProfileNameText => string.IsNullOrWhiteSpace(FilmProfileName)", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("? \"ScanDebug_Runtime_FilmProfileUntitled\".GetLocalized()", scanDebugSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileName = \"ScanDebug_Runtime_FilmProfileUntitled\".GetLocalized()", scanDebugSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8_ProfileNameInvalidWarningCopy_DescribesUntitledFallbackWithoutBlockingSave()
    {
        const string key = "FilmProfile_Validation_ProfileNameInvalid";
        var english = ReadResources("en-us");
        var chinese = ReadResources("zh-CN");

        Assert.Equal("Profile name is empty; the UI shows Untitled until you name it.", english[key]);
        Assert.Equal("配置名称为空；命名前界面会显示未命名。", chinese[key]);
        Assert.Equal(GetPlaceholderIndexes(english[key]), GetPlaceholderIndexes(chinese[key]));
        Assert.DoesNotContain("before saving", english[key], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("non-empty", english[key], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("保存前", chinese[key], StringComparison.Ordinal);
        Assert.DoesNotContain("非空", chinese[key], StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugJsonActions_HaveLocalizedButtonResources()
    {
        var xaml = ReadAppText("Views", "ScanDebugPage.xaml");
        var english = ReadResourceNames("en-us");
        var chinese = ReadResourceNames("zh-CN");

        foreach (var key in new[]
        {
            "ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton.Content",
            "ScanDebug_FilmProfileWorkbenchLifecycleOpenJsonButton.Content",
            "ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "ScanDebug_FilmProfileWorkbenchLifecycleOpenJsonButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "ScanDebug_FilmProfileWorkbenchLifecycleApplyStagedButton.Content",
            "ScanDebug_FilmProfileWorkbenchLifecycleDiscardStagedButton.Content",
            "ScanDebug_FilmProfileWorkbenchLifecycleApplyStagedButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "ScanDebug_FilmProfileWorkbenchLifecycleDiscardStagedButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"
        })
        {
            Assert.Contains(key[..key.IndexOf('.')], xaml, StringComparison.Ordinal);
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }
    }
    private static string[] RequiredRoiIssueResourceKeys()
    {
        var fixedKeys = new[]
        {
            "ScanDebug_Runtime_RoiIssueMessage",
            "ScanDebug_Runtime_RoiIssueStatusFormat",
            "ScanDebug_Runtime_RoiIssueTechnicalPath",
            "ScanDebug_Runtime_RoiIssueAutomationName",
            "ScanDebug_Runtime_RoiIssueOwner_Unknown",
            "ScanDebug_Runtime_RoiIssueField_EffectiveRange",
            "ScanDebug_Runtime_RoiIssueField_ShieldRange",
            "ScanDebug_Runtime_RoiIssueField_FocusLeftRange",
            "ScanDebug_Runtime_RoiIssueField_FocusRightRange",
            "ScanDebug_Runtime_RoiIssueField_FocusOverallRange",
            "ScanDebug_Runtime_RoiIssueField_ColumnRange",
            "ScanDebug_Runtime_RoiIssueField_RoiSelection",
            "ScanDebug_Runtime_RoiIssueField_RoiSettings",
            "ScanDebug_Runtime_RoiIssueField_Unknown",
            "ScanDebug_Runtime_RoiIssueCode_Unknown"
        };
        var ownerKeys = Enum.GetNames<ScanRoiOperationOwner>().Select(owner => $"ScanDebug_Runtime_RoiIssueOwner_{owner}");
        var codeKeys = Enum.GetNames<ScanRoiValidationCode>().Select(code => $"ScanDebug_Runtime_RoiIssueCode_{code}");
        return fixedKeys.Concat(ownerKeys).Concat(codeKeys).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> GetVisibleRoiIssueValues(params Dictionary<string, string>[] resources)
    {
        foreach (var resourceSet in resources)
        {
            foreach (var key in RequiredRoiIssueResourceKeys())
            {
                if (key == "ScanDebug_Runtime_RoiIssueTechnicalPath" || key == "ScanDebug_Runtime_RoiIssueAutomationName")
                    continue;

                yield return resourceSet[key];
            }
        }
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
