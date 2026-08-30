using static PrismUtility.Core.Tests.FilmProfileContractSource;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileLocalizationAccessibilityContractTests
{
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
        ("ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton.Content", "Save profile JSON", "保存配置 JSON"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleSaveCalibrationLibraryButton.Content", "Save to calibration library", "保存到校准库"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleRemoveCalibrationLibraryButton.Content", "Remove from calibration library", "从校准库移除"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleApplyStagedButton.Content", "Apply staged profile", "应用暂存配置"),
        ("ScanDebug_FilmProfileWorkbenchLifecycleDiscardStagedButton.Content", "Discard staged profile", "丢弃暂存配置"),
        ("ScanDebug_FilmProfileWorkbenchStagedImportTitle.Text", "Staged import review", "暂存导入审查"),
        ("ScanDebug_FilmProfileWorkbenchStagedImportHelpText.Text", "Review the loaded JSON before applying it. Apply replaces the current workspace profile; Discard leaves current settings unchanged.", "应用前请审查已加载的 JSON。应用会替换当前工作区配置；丢弃会保留当前设置不变。"),
        ("ScanDebug_FilmProfileWorkbenchSectionMetadata.Text", "1. Profile metadata", "1. 配置元数据"),
        ("ScanDebug_FilmProfileWorkbenchSectionAcquisition.Text", "2. Acquisition setup", "2. 采集设置"),
        ("ScanDebug_FilmProfileWorkbenchSectionRecipe.Text", "3. Scan recipe", "3. 扫描配方"),
        ("ScanDebug_FilmProfileWorkbenchSectionChannelCalibration.Text", "4. Channel calibration", "4. 通道校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionReviewStatus.Text", "5. Review and status", "5. 审查与状态"),
        ("ScanDebug_FilmProfileWorkbenchSectionBasicInfo.Text", "1. Basic info", "1. 基本信息"),
        ("ScanDebug_FilmProfileWorkbenchSectionAcquisitionPlan.Text", "2. Acquisition plan", "2. 采集计划"),
        ("ScanDebug_FilmProfileWorkbenchSectionChannelCalibrationSelector.Text", "3. Channel calibration", "3. 通道校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionLiveCalibration.Text", "4. Live calibration", "4. 实时校准"),
        ("ScanDebug_FilmProfileWorkbenchSectionEngineeringTools.Text", "5. Engineering tools", "5. 工程工具"),
        ("ScanDebug_FilmProfileWorkbenchLiveProgressLabel.Text", "Progress appears here while a scan is running.", "Progress / 进度会在扫描运行时显示在这里。"),
        ("ScanDebug_FilmProfileWorkbenchCurrentValidationTitle.Title", "Current profile validation", "当前配置验证"),
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
        ("ScanDebug_Runtime_StatusFilmProfileImportInvalid", "The selected film profile could not be staged. Review the file and try again.", "所选胶片配置无法暂存。请检查文件后重试。"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationTitle.Text", "Unsaved film profile changes", "胶片配置有未保存更改"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationMessage.Text", "Save or discard changes before opening another profile.", "打开其他配置前请保存或丢弃更改。"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationStayButton.Content", "Keep editing", "继续编辑"),
        ("ScanDebug_FilmProfileWorkbenchDirtyConfirmationDiscardButton.Content", "Discard changes", "丢弃更改"),
        ("ScanDebug_FilmProfileWorkbenchJsonSavePrecise.Content", "Save profile JSON to file", "将配置 JSON 保存到文件"),
        ("ScanDebug_FilmProfileWorkbenchCalibrationLibrarySavePrecise.Content", "Save selected channel calibration to library", "将所选通道校准保存到校准库"),
        ("ScanDebug_FilmProfileWorkbenchCalibrationLibraryRemovePrecise.Content", "Remove selected channel calibration from library", "从校准库移除所选通道校准"),
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
    public void Todo2_FilmProfileDiscardConfirmation_UsesExistingLocalizedResourcesInThePageDialog()
    {
        var page = ReadAppText("Views", "ScanDebugPage.xaml.cs");

        foreach (var key in new[]
        {
            "ScanDebug_FilmProfileWorkbenchDirtyConfirmationTitle.Text",
            "ScanDebug_FilmProfileWorkbenchDirtyConfirmationMessage.Text",
            "ScanDebug_FilmProfileWorkbenchDirtyConfirmationDiscardButton.Content",
            "ScanDebug_FilmProfileWorkbenchDirtyConfirmationStayButton.Content"
        })
        {
            Assert.Contains(key, page, StringComparison.Ordinal);
        }

        Assert.Contains("private async void OnFilmProfileDiscardConfirmationRequested", page, StringComparison.Ordinal);
        Assert.Contains("new ContentDialog", page, StringComparison.Ordinal);
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
}
