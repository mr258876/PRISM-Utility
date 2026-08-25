using static PrismUtility.Core.Tests.FilmProfileContractSource;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileLocalizationAccessibilityContractTests
{
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
            "ScanDebug_Runtime_StatusLoadFilmProfileFailed"
        })
        {
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }

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

        foreach (var key in new[] { "ScanDebug_SaveJsonButton.Content", "ScanDebug_LoadJsonButton.Content" })
        {
            Assert.Contains(key[..key.IndexOf('.')], xaml, StringComparison.Ordinal);
            Assert.Contains(key, english);
            Assert.Contains(key, chinese);
        }
    }
}
