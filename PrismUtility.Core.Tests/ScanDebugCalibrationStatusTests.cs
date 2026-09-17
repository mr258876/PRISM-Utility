using System.Globalization;
using System.Reflection;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;
using PRISM_Utility.ViewModels;
using Windows.Storage;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanDebugCalibrationStatusTests
{
    private static readonly IReadOnlyDictionary<string, ScanChannelCalibrationProfile> EmptyProfiles =
        new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);

    private static ScanParameterSnapshot Todo15InputSnapshot { get; } = new(1_000, 0, 1, 0, 1, 48_000);

    [Fact]
    public async Task StatusOwnership_PersistedRedDraftDiffersThenSelectGreen_ReportsRedModified()
    {
        var persistedRed = CreateProfile(1000);
        var draftRed = CreateProfile(1001);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = persistedRed
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Green"));

        await harness.SelectChannelAsync("Green");

        Assert.Equal(persistedRed, harness.Repository.Snapshot.Profiles["Red"]);
        Assert.Equal(draftRed, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.Modified);
    }

    [Fact]
    public async Task StatusOwnership_ValidDraftBlueWithoutRepositoryProfile_ReportsModified()
    {
        var draftBlue = CreateProfile(1002);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Blue"] = draftBlue
            }, "Blue"));

        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Blue"));
        Assert.Equal(draftBlue, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Blue"]);
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.Modified);
    }

    [Fact]
    public async Task StatusOwnership_SelectedAbsentBlueWithoutRepositoryDraftOrLiveCandidate_ReportsMissing()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);

        await harness.SelectChannelAsync("Blue");

        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Blue"));
        Assert.False(harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey("Blue"));
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.Missing);
    }

    [Fact]
    public async Task StatusOwnership_SelectedPersistedOrDraftWithClearedEditors_ReportsInvalid()
    {
        var persistedRed = CreateProfile(1000);
        var persistedHarness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = persistedRed
            },
            null);
        await ClearCurrentEditorAsync(persistedHarness);

        Assert.Equal(persistedRed, persistedHarness.Repository.Snapshot.Profiles["Red"]);
        AssertStatus(persistedHarness.ViewModel, "Red", CalibrationChannelStatusKind.Invalid);

        var draftRed = CreateProfile(1001);
        var draftHarness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));
        await ClearCurrentEditorAsync(draftHarness);

        Assert.False(draftHarness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        Assert.Equal(draftRed, draftHarness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertStatus(draftHarness.ViewModel, "Red", CalibrationChannelStatusKind.Invalid);
    }

    [Fact]
    public async Task StatusOwnership_InitialBlankAbsentSelectedChannel_ReportsMissing()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        await ClearCurrentEditorAsync(harness);

        var selectedRole = harness.ViewModel.SelectedCalibrationChannel;
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey(selectedRole));
        Assert.False(harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles.ContainsKey(selectedRole));
        AssertStatus(harness.ViewModel, selectedRole, CalibrationChannelStatusKind.Missing);
    }

    [Fact]
    public async Task StatusOwnership_NormalizedEqualPersistedAndDraftRoi_ReportsSaved()
    {
        var normalizedRoi = ScanCalibrationRoiSettings.CreateDefault().Normalize();
        var unnormalizedRoi = new ScanCalibrationRoiSettings(
            new ScanColumnRange(normalizedRoi.EffectiveRange.EndInclusive, normalizedRoi.EffectiveRange.Start),
            normalizedRoi.ShieldRange,
            normalizedRoi.FocusLeftRange,
            normalizedRoi.FocusRightRange,
            normalizedRoi.FocusOverallRange);
        var persistedRed = CreateProfile(1000, normalizedRoi);
        var draftRed = CreateProfile(1000, unnormalizedRoi);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = persistedRed
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Green"));

        await harness.SelectChannelAsync("Green");

        Assert.Equal(persistedRed, harness.Repository.Snapshot.Profiles["Red"]);
        Assert.Equal(draftRed, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.Saved);
    }

    [Fact]
    public async Task CopiedCalibration_NewProfileReplacement_ClearsCopiedTargetAndRestoresRepositoryProjection()
    {
        var persistedRed = CreateProfile(1000);
        var persistedGreen = CreateProfile(2000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = persistedRed,
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);

        var draftBeforeReset = harness.Workspace.Snapshot.CurrentDraft;
        Assert.Empty(draftBeforeReset.ChannelProfiles);
        Assert.True(harness.ViewModel.NewFilmProfileCommand.CanExecute(null));
        await harness.ViewModel.NewFilmProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.NotSame(draftBeforeReset, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Empty(harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles);
        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        await harness.FlushAsync();

        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.Saved);
        AssertCurrentParameters(harness.ViewModel, persistedRed.Parameters);
    }

    [Fact]
    public async Task DraftOnlyRed_SwitchingViaGreen_RestoresDraftCalibrationWithoutRepositoryFallback()
    {
        var draftRed = CreateProfile(
            1001,
            CreateRoi(100, 500, 20, 80, 160, 240, 320, 400, 160, 400),
            blackLevel: 123,
            whiteLevel: 60_000);
        var persistedGreen = CreateProfile(
            2000,
            CreateRoi(700, 1_100, 600, 680, 760, 840, 940, 1_020, 760, 1_020),
            blackLevel: 456,
            whiteLevel: 61_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));

        await harness.SelectChannelAsync("Green");
        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);
        await harness.SelectChannelAsync("Red");

        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        AssertCurrentParameters(harness.ViewModel, draftRed.Parameters);
        Assert.Equal(draftRed.RoiSettings.EffectiveRange.Start.ToString(CultureInfo.InvariantCulture), harness.ViewModel.RoiStartInput);
        Assert.Equal(draftRed.RoiSettings.EffectiveRange.EndInclusive.ToString(CultureInfo.InvariantCulture), harness.ViewModel.RoiEndInput);
        var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.BuildExportDocument().Document.Document);
        Assert.Equal(draftRed, exported.ChannelProfiles["Red"]);
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.Modified);
    }

    [Fact]
    public async Task SliceF_RepositoryAndDraftRedDiffer_SelectAwayAndBackPreservesDraftEditorExportAndDraft()
    {
        var repositoryRed = CreateProfile(
            1000,
            CreateRoi(100, 500, 20, 80, 160, 240, 320, 400, 160, 400),
            blackLevel: 111,
            whiteLevel: 60_000);
        var draftRed = CreateProfile(
            2000,
            CreateRoi(700, 1_100, 600, 680, 760, 840, 940, 1_020, 760, 1_020),
            blackLevel: 222,
            whiteLevel: 61_000);
        var persistedGreen = CreateProfile(3000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = repositoryRed,
                ["Green"] = persistedGreen
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");

        AssertCurrentParameters(harness.ViewModel, draftRed.Parameters);
        Assert.Equal(draftRed.RoiSettings.EffectiveRange.Start.ToString(CultureInfo.InvariantCulture), harness.ViewModel.RoiStartInput);
        Assert.Equal(draftRed.RoiSettings.EffectiveRange.EndInclusive.ToString(CultureInfo.InvariantCulture), harness.ViewModel.RoiEndInput);
        Assert.Equal(draftRed, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.BuildExportDocument().Document.Document);
        Assert.Equal(draftRed, exported.ChannelProfiles["Red"]);
        Assert.Equal(repositoryRed, harness.Repository.Snapshot.Profiles["Red"]);
    }

    [Fact]
    public async Task SliceF_SelectedDraftDifferingOnlyInReferenceLevelsReportsModified()
    {
        var repositoryRed = CreateProfile(1000, blackLevel: 111, whiteLevel: 60_000);
        var draftRed = CreateProfile(1000, blackLevel: 222, whiteLevel: 61_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = repositoryRed
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");

        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.Modified);
    }

    [Fact]
    public async Task SliceF_CopiedRedWinsOverDraftAndRepositoryWhenSelected()
    {
        var repositoryRed = CreateProfile(1000, blackLevel: 111, whiteLevel: 60_000);
        var draftRed = CreateProfile(2000, blackLevel: 222, whiteLevel: 61_000);
        var copiedGreen = CreateProfile(3000, blackLevel: 333, whiteLevel: 62_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = repositoryRed,
                ["Green"] = copiedGreen
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));

        await CopyCalibrationAsync(harness, "Green", "Red");
        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");

        AssertCurrentParameters(harness.ViewModel, copiedGreen.Parameters);
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
        Assert.Equal(copiedGreen, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        Assert.Equal(repositoryRed, harness.Repository.Snapshot.Profiles["Red"]);
    }

    [Fact]
    public async Task SliceF_SaveBlackLevelUsesDraftWhiteLevelAndWritesOnlySelectedRole()
    {
        const ushort sample = 321;
        var repositoryRed = CreateProfile(1000, blackLevel: 111, whiteLevel: 1_000);
        var draftRed = CreateProfile(2000, blackLevel: 222, whiteLevel: 60_000);
        var persistedGreen = CreateProfile(3000, blackLevel: 333, whiteLevel: 61_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = repositoryRed,
                ["Green"] = persistedGreen
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"),
            autoCalibration: new StatusAutoCalibration(draftRed.Parameters, captureFrame: true),
            previewSample: sample,
            connected: true);

        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        InvokeApplyScanFrame(harness.ViewModel);
        await harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(sample, savedRed.BlackLevel);
        Assert.Equal(draftRed.WhiteLevel, savedRed.WhiteLevel);
        Assert.Equal(draftRed.Parameters, savedRed.Parameters);
        Assert.All(harness.Repository.SavedProfileRoles, role => Assert.Equal("Red", role));
        Assert.Equal(persistedGreen, harness.Repository.Snapshot.Profiles["Green"]);
    }

    [Fact]
    public async Task SliceF_SaveSelectedProfileUsesDraftReferenceLevelsAndWritesOnlySelectedRole()
    {
        var repositoryRed = CreateProfile(1000, blackLevel: 111, whiteLevel: 1_000);
        var draftRed = CreateProfile(2000, blackLevel: 222, whiteLevel: 60_000);
        var persistedGreen = CreateProfile(3000, blackLevel: 333, whiteLevel: 61_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = repositoryRed,
                ["Green"] = persistedGreen
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = draftRed
            }, "Red"));

        await harness.ViewModel.SaveChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(draftRed, savedRed);
        Assert.Equal(new[] { "Red" }, harness.Repository.SavedProfileRoles);
        Assert.Equal(persistedGreen, harness.Repository.Snapshot.Profiles["Green"]);
    }

    [Fact]
    public async Task CopiedCalibration_ExternalCurrentDraftReplacement_ClearsCopiedTargetAndAppliesDraftValues()
    {
        var persistedGreen = CreateProfile(2000);
        var loadedRed = CreateProfile(3000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);

        harness.Workspace.SetCurrentDraft(CreateDraft(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = loadedRed
            },
            "Red"));
        await harness.FlushAsync();

        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        AssertCurrentParameters(harness.ViewModel, loadedRed.Parameters);
    }

    [Fact]
    public async Task CopiedCalibration_StagedImportApply_ClearsCopiedTargetAndAppliesImportedValues()
    {
        var persistedGreen = CreateProfile(2000);
        var importedRed = CreateProfile(4000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);

        var staged = harness.Workspace.StageImport(new ScanFilmParameterProfileSet(
            ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
            "Imported profile",
            DateTimeOffset.UnixEpoch,
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = importedRed
            },
            "Red",
            ScanFilmAcquisitionSettings.CreateDefault(),
            null));
        Assert.True(staged.Staged);

        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        Assert.Equal(importedRed, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertCurrentParameters(harness.ViewModel, importedRed.Parameters);
    }

    [Fact]
    public async Task CopiedCalibration_OrdinaryEdit_RetainsCopiedTarget()
    {
        var persistedGreen = CreateProfile(2000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.ViewModel.ExposureTicks = "2001";
        await harness.FlushAsync();

        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
        Assert.Equal("2001", harness.ViewModel.ExposureTicks);
    }

    [Fact]
    public async Task CopiedCalibration_ChannelSwitchRoundTrip_RetainsCopiedTarget()
    {
        var persistedGreen = CreateProfile(2000);
        var persistedBlue = CreateProfile(3000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen,
                ["Blue"] = persistedBlue
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        await harness.SelectChannelAsync("Blue");
        await harness.SelectChannelAsync("Red");

        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);
    }

    [Fact]
    public async Task CopiedCalibration_SuccessfulJsonExport_RetainsMarkerCopiedValuesAndRepositoryIsolation()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.FilmProfileFiles.ExportSucceeds = true;
        await harness.ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.FilmProfileFiles.ExportedProfile);
        Assert.Equal(exported.SavedAtUtc, harness.Workspace.Snapshot.CurrentDraft.SavedAtUtc);
        Assert.Equal(persistedGreen, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        Assert.Equal(persistedGreen, exported.ChannelProfiles["Red"]);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
    }

    [Fact]
    public async Task CopiedCalibration_ExportThenChannelRoundTrip_RetainsCopiedValues()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.FilmProfileFiles.ExportSucceeds = true;
        await harness.ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");

        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);
        Assert.Equal(persistedGreen, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
    }

    [Fact]
    public async Task Todo8_ConstructorLeavesEditableProfileNameEmptyAndShowsPresentationFallback()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);

        Assert.Equal(string.Empty, harness.ViewModel.FilmProfileName);
        Assert.Equal(string.Empty, harness.Workspace.Snapshot.CurrentDraft.ProfileName);
        Assert.Equal("ScanDebug_Runtime_FilmProfileUntitled".GetLocalized(), harness.ViewModel.CurrentProfileNameText);
    }

    [Fact]
    public async Task Todo8_FilmProfileNameChangesNotifyCurrentProfileNameTextWithDisplayFallback()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        var changed = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        harness.ViewModel.FilmProfileName = "5207";
        await harness.FlushAsync();
        var namedDisplay = harness.ViewModel.CurrentProfileNameText;
        harness.ViewModel.FilmProfileName = string.Empty;
        await harness.FlushAsync();

        Assert.Equal("5207", namedDisplay);
        Assert.Equal("ScanDebug_Runtime_FilmProfileUntitled".GetLocalized(), harness.ViewModel.CurrentProfileNameText);
        Assert.Equal(string.Empty, harness.ViewModel.FilmProfileName);
        Assert.Contains(nameof(ScanDebugViewModel.CurrentProfileNameText), changed);
        Assert.True(changed.Count(name => name == nameof(ScanDebugViewModel.CurrentProfileNameText)) >= 2);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    public async Task Todo8_NewAndExportKeepEmptyProfileNameDataWhileFallbackStaysPresentationOnly(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            ResourceExtensions.ResetResourceLoader();

            var draft = new ScanFilmProfileDraft(
                string.Empty,
                DateTimeOffset.UnixEpoch,
                new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Red"] = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000)
                },
                "Red",
                ScanFilmAcquisitionSettings.CreateDefault(),
                null);
            var harness = await CreateAttachedHarnessAsync(EmptyProfiles, draft);
            harness.FilmProfileFiles.ExportSucceeds = true;

            Assert.Equal(string.Empty, harness.ViewModel.FilmProfileName);
            Assert.Equal(string.Empty, harness.Workspace.Snapshot.CurrentDraft.ProfileName);
            Assert.Equal("ScanDebug_Runtime_FilmProfileUntitled".GetLocalized(), harness.ViewModel.CurrentProfileNameText);
            Assert.Contains(harness.ViewModel.CurrentFilmProfileValidationIssues, issue =>
                issue.Code == ScanFilmProfileValidationCode.InvalidProfileName
                && issue.FieldPath == "profileName");
            Assert.True(harness.ViewModel.SaveFilmProfileJsonCommand.CanExecute(null));

            await harness.ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);
            await harness.FlushAsync();

            var exported = Assert.IsType<ScanFilmParameterProfileSet>(harness.FilmProfileFiles.ExportedProfile);
            Assert.Equal(string.Empty, exported.ProfileName);
            Assert.DoesNotContain("Untitled Film Profile", exported.ProfileName, StringComparison.Ordinal);
            Assert.DoesNotContain("未命名胶片配置", exported.ProfileName, StringComparison.Ordinal);

            await harness.ViewModel.NewFilmProfileCommand.ExecuteAsync(null);
            await harness.FlushAsync();

            Assert.Equal(string.Empty, harness.ViewModel.FilmProfileName);
            Assert.Equal(string.Empty, harness.Workspace.Snapshot.CurrentDraft.ProfileName);
            Assert.Equal("ScanDebug_Runtime_FilmProfileUntitled".GetLocalized(), harness.ViewModel.CurrentProfileNameText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            ResourceExtensions.ResetResourceLoader();
        }
    }

    [Fact]
    public async Task CopiedCalibration_MetadataOnlyExternalSnapshot_RetainsCopiedMarkerAndValues()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.ViewModel.FilmProfileName = "Copied draft";
        await harness.FlushAsync();
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        harness.Workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            "Renamed copied draft",
            currentDraft.SavedAtUtc.AddMinutes(1),
            currentDraft.ChannelProfiles,
            currentDraft.SelectedCalibrationChannel,
            currentDraft.AcquisitionSettings,
            currentDraft.ScanRecipeSettings));
        await harness.FlushAsync();

        Assert.Equal("Renamed copied draft", harness.ViewModel.FilmProfileName);
        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        AssertStatus(harness.ViewModel, "Red", CalibrationChannelStatusKind.CopiedUnverified);
    }

    [Fact]
    public async Task CopiedCalibration_OpenThenApply_ClearsMarkerAndAppliesReplacement()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var importedRed = CreateProfile(4000, blackLevel: 234, whiteLevel: 59_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.ViewModel.FilmProfileName = "Copied draft";
        await harness.FlushAsync();
        harness.FilmProfileFiles.ImportedProfile = new ScanFilmParameterProfileSet(
            ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
            "Opened profile",
            DateTimeOffset.UnixEpoch.AddDays(1),
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = importedRed
            },
            "Red",
            ScanFilmAcquisitionSettings.CreateDefault(),
            null);

        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(importedRed, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertCurrentParameters(harness.ViewModel, importedRed.Parameters);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
    }

    [Fact]
    public async Task CopiedCalibration_StagedApplySameChannelData_ClearsMarkerAtAuthoritativeApply()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        harness.ViewModel.FilmProfileName = "Copied draft";
        await harness.FlushAsync();
        var copiedDraft = harness.Workspace.Snapshot.CurrentDraft;
        var staged = harness.Workspace.StageImport(new ScanFilmParameterProfileSet(
            ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
            "Replacement with same calibration data",
            copiedDraft.SavedAtUtc.AddMinutes(1),
            copiedDraft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            copiedDraft.SelectedCalibrationChannel,
            copiedDraft.AcquisitionSettings,
            copiedDraft.ScanRecipeSettings));
        Assert.True(staged.Staged);

        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(persistedGreen, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        AssertCurrentParameters(harness.ViewModel, persistedGreen.Parameters);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
    }

    [Fact]
    public async Task CopiedCalibration_SuccessfulSave_RemovesOnlySavedTargetAndPreservesCopiedLevels()
    {
        var persistedGreen = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        await CopyCalibrationAsync(harness, "Green", "Blue");
        await harness.SelectChannelAsync("Red");

        await harness.ViewModel.SaveChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(persistedGreen.Parameters, savedRed.Parameters);
        Assert.Equal(persistedGreen.BlackLevel, savedRed.BlackLevel);
        Assert.Equal(persistedGreen.WhiteLevel, savedRed.WhiteLevel);
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.CopiedUnverified);
    }

    [Fact]
    public async Task CopiedCalibration_SuccessfulClear_RemovesOnlyClearedTargetAndPreservesSiblingCopy()
    {
        var persistedGreen = CreateProfile(2000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = persistedGreen
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        await CopyCalibrationAsync(harness, "Green", "Blue");
        await harness.SelectChannelAsync("Red");

        await harness.ViewModel.ClearChannelProfileCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.CopiedUnverified);
    }

    [Fact]
    public async Task SelectionPersistence_InitialRedThenRapidGreenBlue_WritesBlueLastAndKeepsBlueProjection()
    {
        var red = CreateProfile(1000);
        var green = CreateProfile(2000);
        var blue = CreateProfile(3000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green,
                ["Blue"] = blue
            },
            null);
        harness.Repository.BlockSelectedChannelWrite("Green");
        harness.Repository.BlockSelectedChannelWrite("Blue");
        var blueProjection = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ViewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ScanDebugViewModel.ExposureTicks)
                && harness.ViewModel.ExposureTicks == blue.Parameters.ExposureTicks.ToString(CultureInfo.InvariantCulture))
            {
                blueProjection.TrySetResult(null);
            }
        };

        Assert.Equal("Red", harness.ViewModel.SelectedCalibrationChannel);
        harness.ViewModel.SelectedCalibrationChannel = "Green";
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        harness.ViewModel.SelectedCalibrationChannel = "Blue";

        harness.Repository.ReleaseSelectedChannelWrite("Blue");
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        await harness.Repository.WaitForSelectedChannelWriteCompletionAsync("Green");
        await harness.Repository.WaitForSelectedChannelWriteCompletionAsync("Blue");
        await blueProjection.Task;
        await harness.FlushAsync();

        Assert.Equal("Blue", harness.Repository.Snapshot.SelectedChannel);
        Assert.Equal("Blue", harness.ViewModel.SelectedCalibrationChannel);
        AssertCurrentParameters(harness.ViewModel, blue.Parameters);
        var selectedWrites = harness.Repository.SelectedChannelWriteRoles;
        var blueWriteIndex = selectedWrites.ToList().IndexOf("Blue");
        Assert.True(blueWriteIndex >= 0);
        Assert.DoesNotContain(selectedWrites.Skip(blueWriteIndex + 1), role => string.Equals(role, "Green", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DelayedSave_CapturesRedAndLeavesCurrentGreenStatusAndSiblingCopyUntouched()
    {
        var red = CreateProfile(1000);
        var green = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var blue = CreateProfile(3000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green,
                ["Blue"] = blue
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        await CopyCalibrationAsync(harness, "Green", "Blue");
        await harness.SelectChannelAsync("Red");
        harness.Repository.BlockProfileSave("Red");

        var saveTask = harness.ViewModel.SaveChannelProfileCommand.ExecuteAsync(null);
        await harness.Repository.WaitForProfileSaveAsync("Red").WaitAsync(TimeSpan.FromSeconds(5));
        await harness.SelectChannelAsync("Green");
        harness.ViewModel.StatusText = "green-current-status";
        harness.ViewModel.CalibrationChannelStatusText = "green-current-calibration-status";

        harness.Repository.ReleaseProfileSave("Red");
        await saveTask;
        await harness.FlushAsync();

        Assert.Equal(new[] { "Red" }, harness.Repository.SavedProfileRoles);
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        Assert.Equal(blue, harness.Repository.Snapshot.Profiles["Blue"]);
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.CopiedUnverified);
        Assert.Equal("green-current-status", harness.ViewModel.StatusText);
        Assert.Equal("green-current-calibration-status", harness.ViewModel.CalibrationChannelStatusText);
    }

    [Fact]
    public async Task DelayedClear_CapturesRedBeforeConfirmationAndCleansOnlyRedMarker()
    {
        var red = CreateProfile(1000);
        var green = CreateProfile(2000);
        var blue = CreateProfile(3000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green,
                ["Blue"] = blue
            },
            null);

        await CopyCalibrationAsync(harness, "Green", "Red");
        await CopyCalibrationAsync(harness, "Green", "Blue");
        await harness.SelectChannelAsync("Red");
        var promptEntered = new TaskCompletionSource<ScanCalibrationPromptRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ViewModel.CalibrationPromptRequested += (_, request) => promptEntered.TrySetResult(request);

        var clearTask = harness.ViewModel.ClearChannelProfileCommand.ExecuteAsync(null);
        var prompt = await promptEntered.Task;
        await harness.SelectChannelAsync("Green");
        harness.ViewModel.StatusText = "green-current-status";
        harness.ViewModel.CalibrationChannelStatusText = "green-current-calibration-status";
        prompt.CompletionSource.TrySetResult(true);

        await clearTask;
        await harness.FlushAsync();

        Assert.Equal(new[] { "Red" }, harness.Repository.ClearedProfileRoles);
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        Assert.NotEqual(CalibrationChannelStatusKind.CopiedUnverified, GetStatus(harness.ViewModel, "Red"));
        AssertStatus(harness.ViewModel, "Blue", CalibrationChannelStatusKind.CopiedUnverified);
        Assert.Equal("green-current-status", harness.ViewModel.StatusText);
        Assert.Equal("green-current-calibration-status", harness.ViewModel.CalibrationChannelStatusText);
    }

    [Fact]
    public async Task DelayedLevelSave_CapturesRedAndPreservesGreenLevels()
    {
        const ushort sample = 321;
        var red = CreateProfile(1000, blackLevel: 12, whiteLevel: 1000);
        var green = CreateProfile(2000, blackLevel: 34, whiteLevel: 2000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            null,
            autoCalibration: new StatusAutoCalibration(red.Parameters, captureFrame: true),
            previewSample: sample,
            connected: true);

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        InvokeApplyScanFrame(harness.ViewModel);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));
        harness.Repository.BlockProfileSave("Red");

        var saveTask = harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        await harness.Repository.WaitForProfileSaveAsync("Red");
        await harness.SelectChannelAsync("Green");
        harness.ViewModel.StatusText = "green-current-status";

        harness.Repository.ReleaseProfileSave("Red");
        await saveTask;
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(sample, savedRed.BlackLevel);
        Assert.Equal(red.WhiteLevel, savedRed.WhiteLevel);
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        Assert.Equal("green-current-status", harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task DelayedAutoCalibration_CapturesRedBeforeSwitchingToGreenWithoutSaving()
    {
        var red = CreateProfile(1000);
        var green = CreateProfile(2000);
        var calibratedRed = CreateProfile(1100);
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            null,
            autoCalibration: new StatusAutoCalibration(calibratedRed.Parameters, autoBlackEntered: entered, autoBlackRelease: release),
            connected: true);

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        var calibrationTask = harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await entered.Task;
        await harness.SelectChannelAsync("Green");
        release.TrySetResult(null);

        await calibrationTask;
        await harness.FlushAsync();

        Assert.Equal(red.Parameters, harness.Repository.Snapshot.Profiles["Red"].Parameters);
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        Assert.Empty(harness.Repository.SavedProfileRoles);
    }

    [Fact]
    public async Task AutoCalibration_ChannelSwitch_DropsRedProgressAndFinalProjectionWithoutSaving()
    {
        var redRoi = CreateRoi(100, 500, 20, 80, 160, 240, 320, 400, 160, 400);
        var greenRoi = CreateRoi(700, 1_100, 600, 680, 760, 840, 940, 1_020, 760, 1_020);
        var red = CreateProfile(1000, redRoi);
        var green = CreateProfile(2000, greenRoi);
        var progressRed = CreateProfile(1100, redRoi);
        var calibratedRed = CreateProfile(1200, redRoi);
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressPublished = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var autoCalibration = new StatusAutoCalibration(
            autoBlackResult: calibratedRed.Parameters,
            autoBlackProgressSnapshot: progressRed.Parameters,
            autoBlackProgressStatus: "red-progress-status",
            autoBlackEntered: entered,
            autoBlackProgressRelease: progressRelease,
            autoBlackProgressPublished: progressPublished,
            autoBlackCompletionRelease: completionRelease);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            null,
            autoCalibration: autoCalibration,
            connected: true);

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        var calibrationTask = harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        try
        {
            await entered.Task;
            await harness.SelectChannelAsync("Green");
            harness.ViewModel.StatusText = "green-current-status";
            harness.ViewModel.CalibrationChannelStatusText = "green-current-calibration-status";

            progressRelease.TrySetResult(null);
            await progressPublished.Task;
            await harness.FlushAsync();

            AssertCurrentParameters(harness.ViewModel, green.Parameters);
            Assert.Equal("green-current-status", harness.ViewModel.StatusText);
            Assert.Equal("green-current-calibration-status", harness.ViewModel.CalibrationChannelStatusText);

            completionRelease.TrySetResult(null);
            await calibrationTask;
            await harness.FlushAsync();

            Assert.Equal(redRoi, autoCalibration.LastAutoBlackRoiSettings);
            Assert.Empty(harness.Repository.SavedProfileRoles);
            Assert.Equal(red.Parameters, harness.Repository.Snapshot.Profiles["Red"].Parameters);
            Assert.Equal(redRoi, harness.Repository.Snapshot.Profiles["Red"].RoiSettings);
            Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
            AssertCurrentParameters(harness.ViewModel, green.Parameters);
            Assert.Equal("green-current-status", harness.ViewModel.StatusText);
            Assert.Equal("green-current-calibration-status", harness.ViewModel.CalibrationChannelStatusText);
        }
        finally
        {
            completionRelease.TrySetResult(null);
            await calibrationTask;
            await harness.FlushAsync();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AutoCalibration_ChannelSwitch_CancelOrFailureDoesNotPublishRedStateToGreen(bool canceled)
    {
        var red = CreateProfile(1000, CreateRoi(100, 500, 20, 80, 160, 240, 320, 400, 160, 400));
        var green = CreateProfile(2000, CreateRoi(700, 1_100, 600, 680, 760, 840, 940, 1_020, 760, 1_020));
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var autoCalibration = new StatusAutoCalibration(
            autoBlackResult: red.Parameters,
            autoBlackEntered: entered,
            autoBlackCompletionRelease: release,
            autoBlackFailure: canceled
                ? new OperationCanceledException("red calibration canceled")
                : new InvalidOperationException("red calibration failed"));
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            null,
            autoCalibration: autoCalibration,
            connected: true);

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        var calibrationTask = harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await entered.Task;
        await harness.SelectChannelAsync("Green");
        harness.ViewModel.StatusText = "green-current-status";
        harness.ViewModel.CalibrationChannelStatusText = "green-current-calibration-status";

        release.TrySetResult(null);
        await calibrationTask;
        await harness.FlushAsync();

        Assert.Empty(harness.Repository.SavedProfileRoles);
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        AssertCurrentParameters(harness.ViewModel, green.Parameters);
        Assert.Equal("green-current-status", harness.ViewModel.StatusText);
        Assert.Equal("green-current-calibration-status", harness.ViewModel.CalibrationChannelStatusText);
    }

    [Fact]
    public async Task Todo15_CandidateUsesDeviceSnapshotAndRestoresIlluminationBeforePendingReview()
    {
        var editor = CreateProfile(1_000).Parameters;
        var device = CreateProfile(1_250).Parameters;
        var candidate = CreateProfile(1_100).Parameters;
        var illuminationState = new ScanIlluminationState(11, 22, 33, 44, 3, 4, 5, 6, 7, 8, 9);
        var parameters = new StatusParameters(loadedSnapshot: device);
        var illumination = new StatusIllumination(illuminationState);
        var autoCalibration = new StatusAutoCalibration(candidate);
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = CreateProfile(1_000) },
            null,
            autoCalibration: autoCalibration,
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator,
            parameters: parameters,
            illumination: illumination);
        await PrepareTodo15CandidateAsync(harness, coordinator);
        var draftBeforeCalibration = harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"].Parameters;

        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.NotEqual(editor, device);
        Assert.Equal(device, autoCalibration.LastAutoBlackInput);
        Assert.Equal(device, pending.OriginalSnapshot);
        Assert.Equal(candidate, pending.CandidateSnapshot);
        AssertCurrentParameters(harness.ViewModel, draftBeforeCalibration);
        Assert.Equal(draftBeforeCalibration, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"].Parameters);
        Assert.Equal([device], parameters.AppliedSnapshots);
        Assert.Equal([illuminationState], illumination.RestoredStates);
        Assert.All(parameters.ApplyTokensCanBeCanceled, Assert.False);
        Assert.All(illumination.RestoreTokensCanBeCanceled, Assert.False);
    }

    [Fact]
    public async Task Todo15_AcceptApplyFailure_RollsBackOriginalDeviceParameters()
    {
        var original = CreateProfile(1_250).Parameters;
        var candidate = CreateProfile(1_100).Parameters;
        var parameters = new StatusParameters(
            loadedSnapshot: original,
            channelApplyFailure: new InvalidOperationException("planned apply failure"),
            failOnChannelApplyNumber: 2);
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = CreateProfile(1_000) },
            null,
            autoCalibration: new StatusAutoCalibration(candidate),
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator,
            parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Failed, pending.State);
        Assert.Equal("device-apply.failed", pending.Failure?.Code);
        Assert.Equal([original, candidate, original], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_AcceptAndSaveFailure_RollsBackOriginalDeviceParameters()
    {
        var original = CreateProfile(1_250).Parameters;
        var candidate = CreateProfile(1_100).Parameters;
        var parameters = new StatusParameters(loadedSnapshot: original);
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = CreateProfile(1_000) },
            null,
            autoCalibration: new StatusAutoCalibration(candidate),
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator,
            parameters: parameters);
        harness.Repository.FailProfileSave("Red", new InvalidOperationException("planned save failure"));

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.ViewModel.AcceptAndSavePendingCalibrationCommand.ExecuteAsync(null);

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Failed, pending.State);
        Assert.Equal("persistence.failed-after-device-apply", pending.Failure?.Code);
        Assert.Equal([original, candidate, original], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_CandidateSuccess_PublishesPendingMetricsAfterOriginalRestoreWithoutRepositoryWrite()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var beforeMetrics = new ScanCalibrationMetrics(12.5m, 42m, 1.25m, 3.5m);
        var afterMetrics = new ScanCalibrationMetrics(2.5m, 4m, 0.25m, 0.5m);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            attachRuntimeBindings: true,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters, beforeMetrics: beforeMetrics, afterMetrics: afterMetrics),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Pending, pending.State);
        Assert.Equal(Todo15InputSnapshot, pending.OriginalSnapshot);
        Assert.Equal(candidate.Parameters, pending.CandidateSnapshot);
        Assert.Equal(beforeMetrics, pending.BeforeMetrics);
        Assert.Equal(afterMetrics, pending.AfterMetrics);
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.All(parameters.ApplyTokensCanBeCanceled, Assert.False);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Todo15_CalibrationCancellationOrFailure_RestoresOriginalWithSafeTokenWithoutRepositoryWrite(bool canceled)
    {
        var original = CreateProfile(1_000);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(original.Parameters, autoBlackFailure: canceled ? new OperationCanceledException("planned cancellation") : new InvalidOperationException("planned failure")),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Null(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.All(parameters.ApplyTokensCanBeCanceled, Assert.False);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo19_InvalidRawAdcRoi_BlocksCalibrationBeforeCoordinatorOrServiceSideEffects()
    {
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var autoCalibration = new StatusAutoCalibration(Todo15InputSnapshot);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            autoCalibration: autoCalibration,
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator);
        var invalid = ScanCalibrationRoiSettings.CreateDefault() with
        {
            EffectiveRange = new ScanColumnRange(int.MinValue, int.MaxValue)
        };
        SetPrivateField(harness.ViewModel, "_roiSettings", invalid);

        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(0, coordinator.RunConnectedSessionStateCallCount);
        Assert.Null(autoCalibration.LastAutoBlackInput);
        Assert.Equal(invalid, ReadPrivateField<ScanCalibrationRoiSettings>(harness.ViewModel, "_roiSettings"));
    }

    [Fact]
    public async Task Todo15_RestoreFailure_LeavesNoAcceptablePendingCandidateAndReportsFailure()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters(channelApplyFailure: new InvalidOperationException("planned restore failure"));
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Null(harness.ViewModel.PendingCalibrationResult);
        Assert.False(harness.ViewModel.AcceptPendingCalibrationCommand.CanExecute(null));
        Assert.Equal(1, parameters.ChannelApplyCount);
        Assert.NotEqual("ScanDebug_Runtime_StatusAutoCalibrationSucceeded".GetLocalized(), harness.ViewModel.StatusText);
        Assert.NotEmpty(harness.ViewModel.StatusText);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_ChannelChangeBeforePublication_InvalidatesCandidateAndDropsStaleCallbacks()
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(2_000);
        var candidate = CreateProfile(1_100);
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressPublished = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters, autoBlackEntered: entered, autoBlackProgressSnapshot: candidate.Parameters, autoBlackProgressStatus: "stale red progress", autoBlackProgressRelease: progressRelease, autoBlackProgressPublished: progressPublished, autoBlackCompletionRelease: completionRelease),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        var calibrationTask = harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        try
        {
            await entered.Task;
            await harness.SelectChannelAsync("Green");
            harness.ViewModel.StatusText = "green status";
            progressRelease.TrySetResult(null);
            await progressPublished.Task;
            await harness.FlushAsync();
            Assert.Equal("green status", harness.ViewModel.StatusText);
            AssertCurrentParameters(harness.ViewModel, green.Parameters);

            completionRelease.TrySetResult(null);
            await calibrationTask;
            await harness.FlushAsync();

            var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
            Assert.Equal(PendingCalibrationResultState.Invalidated, pending.State);
            Assert.Equal(CalibrationCandidateStaleness.Channel, pending.Staleness);
            Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
            Assert.Equal(0, harness.Repository.SaveProfileCount);
        }
        finally
        {
            progressRelease.TrySetResult(null);
            completionRelease.TrySetResult(null);
            await calibrationTask;
        }
    }

    [Fact]
    public async Task Todo15_WorkspaceChange_InvalidatesPendingCandidateBeforeAccept()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            attachRuntimeBindings: true,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        var selectedChannel = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult).SelectedChannel;
        harness.Workspace.SetCurrentDraft(CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, selectedChannel));
        await harness.FlushAsync();
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Invalidated, pending.State);
        Assert.True(pending.Staleness.HasFlag(CalibrationCandidateStaleness.Workspace));
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Theory]
    [InlineData(ScannerSessionState.Disconnected)]
    [InlineData(ScannerSessionState.Faulted)]
    [InlineData(ScannerSessionState.ReconnectPrompt)]
    public async Task Todo15_DeviceSessionReset_InvalidatesUnknownCandidateBeforeAccept(ScannerSessionState resetState)
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        coordinator.PublishSnapshot(new ScannerDeviceSessionSnapshot(resetState, null, null, null, ScannerReconnectPromptState.None, DateTimeOffset.UtcNow));
        await harness.FlushAsync();
        coordinator.PublishSnapshot(new ScannerDeviceSessionSnapshot(ScannerSessionState.Connected, null, null, null, ScannerReconnectPromptState.None, DateTimeOffset.UtcNow));
        await harness.FlushAsync();
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Invalidated, pending.State);
        Assert.Equal(CalibrationCandidateStaleness.Device, pending.Staleness);
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_Accept_AppliesCandidateProjectsItAndDoesNotSave()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Accept, pending.State);
        Assert.Equal([Todo15InputSnapshot, candidate.Parameters], parameters.AppliedSnapshots);
        AssertCurrentParameters(harness.ViewModel, candidate.Parameters);
        Assert.Equal(original, harness.Repository.Snapshot.Profiles["Red"]);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_AcceptAndSave_AppliesCandidateBeforeCapturedRoleWriteAndPreservesCapturedRoiLevels()
    {
        var red = CreateProfile(1_000, CreateRoi(100, 500, 20, 80, 160, 240, 320, 400, 160, 400), blackLevel: 111, whiteLevel: 61_000);
        var green = CreateProfile(2_000, CreateRoi(700, 1_100, 600, 680, 760, 840, 940, 1_020, 760, 1_020), blackLevel: 222, whiteLevel: 62_000);
        var candidate = CreateProfile(1_100);
        var applyEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parameters = new StatusParameters(applyEntered, releaseApply, blockOnChannelApplyNumber: 2);
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var autoCalibration = new StatusAutoCalibration(candidate.Parameters);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green }, null,
            autoCalibration: autoCalibration,
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        var acceptTask = harness.ViewModel.AcceptAndSavePendingCalibrationCommand.ExecuteAsync(null);
        try
        {
            await applyEntered.Task;
            Assert.Equal(0, harness.Repository.SaveProfileCount);
            await harness.SelectChannelAsync("Green");
            releaseApply.TrySetResult(null);
            await acceptTask;
            await harness.FlushAsync();

            Assert.Single(harness.Repository.SavedProfileRoles, role => string.Equals("Red", role, StringComparison.OrdinalIgnoreCase));
            Assert.Equal([Todo15InputSnapshot, candidate.Parameters], parameters.AppliedSnapshots);
            var saved = harness.Repository.Snapshot.Profiles["Red"];
            Assert.Equal(candidate.Parameters, saved.Parameters);
            Assert.Equal(autoCalibration.LastAutoBlackRoiSettings, saved.RoiSettings);
            Assert.Equal(red.BlackLevel, saved.BlackLevel);
            Assert.Equal(red.WhiteLevel, saved.WhiteLevel);
        }
        finally
        {
            releaseApply.TrySetResult(null);
            await acceptTask;
        }
    }

    [Fact]
    public async Task Todo15_Accept_DeviceApplyFailure_WritesNothingAndPublishesTypedFailure()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var failure = new InvalidOperationException("planned device apply failure");
        var parameters = new StatusParameters(channelApplyFailure: failure, failOnChannelApplyNumber: 2);
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        var failureResult = Assert.IsType<ScanCalibrationCandidateFailure>(pending.Failure);
        Assert.Equal(PendingCalibrationResultState.Failed, pending.State);
        Assert.Equal("device-apply.failed", failureResult.Code);
        Assert.Equal(failure.Message, failureResult.Detail);
        Assert.Equal([Todo15InputSnapshot, candidate.Parameters, Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_AcceptAndSave_RepositoryFailureAfterDeviceApplyPublishesDistinctTypedFailure()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var failure = new InvalidOperationException("planned repository failure");
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);
        harness.Repository.FailProfileSave("Red", failure);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.ViewModel.AcceptAndSavePendingCalibrationCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        var failureResult = Assert.IsType<ScanCalibrationCandidateFailure>(pending.Failure);
        Assert.Equal(PendingCalibrationResultState.Failed, pending.State);
        Assert.Equal("persistence.failed-after-device-apply", failureResult.Code);
        Assert.Equal(failure.Message, failureResult.Detail);
        Assert.Equal([Todo15InputSnapshot, candidate.Parameters, Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
        Assert.Contains(harness.Repository.CallLog, entry => string.Equals("SaveProfile:Red", entry, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Todo15_RuntimeConflict_RejectsPendingAcceptAtExecutionBoundaryWithoutSideEffects()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        harness.ViewModel.IsRunning = true;
        await harness.ViewModel.AcceptPendingCalibrationCommand.ExecuteAsync(null);
        harness.ViewModel.IsRunning = false;
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Pending, pending.State);
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task Todo15_RestoreBeforeReview_MakesRestoreAndCancelNoOpSafeWithoutAdditionalDeviceApply()
    {
        var original = CreateProfile(1_000);
        var candidate = CreateProfile(1_100);
        var parameters = new StatusParameters();
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = original }, null,
            autoCalibration: new StatusAutoCalibration(candidate.Parameters),
            connected: true, scanSession: session, sessionCoordinator: coordinator, parameters: parameters);

        await PrepareTodo15CandidateAsync(harness, coordinator);
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        harness.ViewModel.RestorePendingCalibrationCommand.Execute(null);
        harness.ViewModel.RestorePendingCalibrationCommand.Execute(null);
        harness.ViewModel.CancelPendingCalibrationCommand.Execute(null);
        await harness.FlushAsync();

        var pending = Assert.IsType<PendingCalibrationResult>(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal(PendingCalibrationResultState.Restored, pending.State);
        Assert.Equal([Todo15InputSnapshot], parameters.AppliedSnapshots);
        Assert.Equal(0, harness.Repository.SaveProfileCount);
    }

    [Fact]
    public async Task CopiedCalibration_SaveBlackLevel_PreservesCopiedWhiteLevel()
    {
        const ushort sample = 321;
        var source = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = source
            },
            null,
            autoCalibration: new StatusAutoCalibration(
                autoBlackResult: source.Parameters,
                captureFrame: true,
                autoBlackFailure: new OperationCanceledException("capture preview without saving")),
            previewSample: sample,
            connected: true);

        await CopyCalibrationAsync(harness, "Green", "Red");
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        InvokeApplyScanFrame(harness.ViewModel);

        await harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(sample, savedRed.BlackLevel);
        Assert.Equal(source.WhiteLevel, savedRed.WhiteLevel);
        Assert.Equal(source.Parameters, savedRed.Parameters);
    }

    [Fact]
    public async Task CopiedCalibration_SaveWhiteLevel_PreservesCopiedBlackLevel()
    {
        const ushort sample = 321;
        var source = CreateProfile(2000, blackLevel: 123, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = source
            },
            null,
            autoCalibration: new StatusAutoCalibration(
                autoBlackResult: source.Parameters,
                captureFrame: true,
                autoBlackFailure: new OperationCanceledException("capture preview without saving")),
            previewSample: sample,
            connected: true);

        await CopyCalibrationAsync(harness, "Green", "Red");
        Assert.False(harness.Repository.Snapshot.Profiles.ContainsKey("Red"));
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        InvokeApplyScanFrame(harness.ViewModel);

        await harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.Equal(source.BlackLevel, savedRed.BlackLevel);
        Assert.Equal(sample, savedRed.WhiteLevel);
        Assert.Equal(source.Parameters, savedRed.Parameters);
    }

    [Fact]
    public async Task DelayedWhiteLevelSave_CapturesRedAndLeavesGreenLevelsUnchanged()
    {
        const ushort sample = 321;
        var red = CreateProfile(1000, blackLevel: 12, whiteLevel: 1000);
        var green = CreateProfile(2000, blackLevel: 34, whiteLevel: 2000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            null,
            autoCalibration: new StatusAutoCalibration(
                autoBlackResult: red.Parameters,
                captureFrame: true),
            previewSample: sample,
            connected: true);

        await harness.SelectChannelAsync("Green");
        await harness.SelectChannelAsync("Red");
        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        InvokeApplyScanFrame(harness.ViewModel);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.CanExecute(null));
        harness.Repository.BlockProfileSave("Red");

        var saveTask = harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.ExecuteAsync(null);
        await harness.Repository.WaitForProfileSaveAsync("Red");
        await harness.SelectChannelAsync("Green");
        harness.ViewModel.StatusText = "green-current-status";

        harness.Repository.ReleaseProfileSave("Red");
        await saveTask;
        await harness.FlushAsync();

        var savedRed = harness.Repository.Snapshot.Profiles["Red"];
        Assert.All(harness.Repository.SavedProfileRoles, role => Assert.Equal("Red", role));
        Assert.Equal(red.BlackLevel, savedRed.BlackLevel);
        Assert.Equal(sample, savedRed.WhiteLevel);
        Assert.Equal(green, harness.Repository.Snapshot.Profiles["Green"]);
        Assert.Equal("green-current-status", harness.ViewModel.StatusText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelectionPersistence_CanceledOrFailedWriteReleasesGateForCurrentBlueSelection(bool canceled)
    {
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Green"] = CreateProfile(2000),
                ["Blue"] = CreateProfile(3000)
            },
            null);
        harness.Repository.BlockSelectedChannelWrite(
            "Green",
            canceled ? new OperationCanceledException() : new InvalidOperationException("selection write failed"));

        harness.ViewModel.SelectedCalibrationChannel = "Green";
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        await harness.Repository.WaitForSelectedChannelWriteCompletionAsync("Green");

        harness.ViewModel.SelectedCalibrationChannel = "Blue";
        await harness.Repository.WaitForSelectedChannelWriteCompletionAsync("Blue");
        await harness.FlushAsync();

        Assert.Equal("Blue", harness.Repository.Snapshot.SelectedChannel);
        Assert.Equal("Blue", harness.ViewModel.SelectedCalibrationChannel);
        Assert.Contains(harness.Repository.SelectedChannelWriteRoles, role => string.Equals(role, "Blue", StringComparison.OrdinalIgnoreCase));
    }


    public static TheoryData<string> Todo10AcquisitionUiFieldCases => new()
    {
         { nameof(ScanFilmAcquisitionSettings.Rows) },
         { nameof(ScanFilmAcquisitionSettings.ScanMotorId) },
         { nameof(ScanFilmAcquisitionSettings.TargetLinePitchMicrometers) },
         { nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive) },
        { nameof(ScanFilmAcquisitionSettings.WarmUpEnabled) },
        { nameof(ScanFilmAcquisitionSettings.TransportStrategy) },
        { nameof(ScanFilmAcquisitionSettings.AcquisitionChannelAssignment) }
    };

    [Theory]
    [MemberData(nameof(Todo10AcquisitionUiFieldCases))]
    public async Task Todo10ProfileOwnedAcquisitionUiMutations_SynchronizeDraftWithoutStaleExistingValues(string fieldName)
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var ledRoles = harness.ViewModel.AcquisitionChannels.Select(channel => channel.Role).ToArray();

        switch (fieldName)
        {
            case nameof(ScanFilmAcquisitionSettings.Rows):
                harness.ViewModel.SelectedRows = "333";
                break;
            case nameof(ScanFilmAcquisitionSettings.ScanMotorId):
                harness.ViewModel.SelectedScanMotor = "Motor1";
                break;
            case nameof(ScanFilmAcquisitionSettings.TargetLinePitchMicrometers):
                harness.ViewModel.MotorDistancePerLineUnit = ScanMotorDistanceText.MicrometersUnit;
                harness.ViewModel.MotorDistancePerLineValue = "12.6";
                break;
            case nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive):
                harness.ViewModel.SelectedStartingDirection = "Forward";
                break;
            case nameof(ScanFilmAcquisitionSettings.WarmUpEnabled):
                harness.ViewModel.IsWarmUpEnabled = false;
                break;
            case nameof(ScanFilmAcquisitionSettings.TransportStrategy):
                harness.ViewModel.IsAlternateMotorDirectionEnabled = true;
                break;
            case nameof(ScanFilmAcquisitionSettings.AcquisitionChannelAssignment):
                foreach (var channel in harness.ViewModel.AcquisitionChannels)
                    channel.IsSelected = false;
                harness.ViewModel.AcquisitionChannels[0].IsSelected = true;
                harness.ViewModel.AcquisitionChannels[2].IsSelected = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null);
        }

        await harness.FlushAsync();

        var acquisition = Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings);
        switch (fieldName)
        {
            case nameof(ScanFilmAcquisitionSettings.Rows):
                Assert.Equal(333, acquisition.Rows);
                break;
            case nameof(ScanFilmAcquisitionSettings.ScanMotorId):
                Assert.Equal((byte)0, acquisition.ScanMotorId);
                break;
            case nameof(ScanFilmAcquisitionSettings.TargetLinePitchMicrometers):
                Assert.Equal(12.6, acquisition.TargetLinePitchMicrometers);
                Assert.Equal("12.6", harness.ViewModel.MotorDistancePerLineValue);
                var roundTrippedDocument = Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.BuildExportDocument().Document.Document);
                var roundTrippedJson = new ScanFilmProfileDocumentService().Serialize(roundTrippedDocument);
                Assert.True(harness.Workspace.StageImport(roundTrippedJson).Staged);
                await harness.Workspace.ApplyStagedImportAsync(CancellationToken.None);
                await harness.FlushAsync();
                Assert.Equal("12.6", harness.ViewModel.MotorDistancePerLineValue);
                Assert.Equal(12.6, Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings).TargetLinePitchMicrometers);
                break;
            case nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive):
                Assert.True(acquisition.StartingDirectionPositive);
                break;
            case nameof(ScanFilmAcquisitionSettings.WarmUpEnabled):
                Assert.False(acquisition.WarmUpEnabled);
                break;
            case nameof(ScanFilmAcquisitionSettings.TransportStrategy):
                Assert.Equal(ScanFilmTransportStrategy.AlternateDirection, acquisition.TransportStrategy);
                break;
            case nameof(ScanFilmAcquisitionSettings.AcquisitionChannelAssignment):
                Assert.Equal(new[] { ledRoles[0], "Unused", ledRoles[2], "Unused" }, acquisition.AcquisitionChannelAssignment?.Roles);
                Assert.Equal(new[] { false, false, false, false }, acquisition.AcquisitionChannelAssignment?.ReversedFlags);
                break;
        }

    }

    [Fact]
    public async Task Todo10ProfileOwnedAcquisitionTargetLinePitch_DerivedIntervalChangesDoNotAuthorTargetPitchAndExplicitEditsPersist()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        var nullTargetDraft = new ScanFilmProfileDraft(
            currentDraft.ProfileName,
            currentDraft.SavedAtUtc,
            currentDraft.ChannelProfiles,
            currentDraft.SelectedCalibrationChannel,
            currentDraft.AcquisitionSettings! with { TargetLinePitchMicrometers = null, MotorIntervalNs = 3000 },
            currentDraft.ScanRecipeSettings);
        harness.Workspace.SetCurrentDraft(nullTargetDraft);
        harness.Workspace.MarkExported(Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.BuildExportDocument().Document.Document));
        await harness.FlushAsync();

        Assert.False(harness.Workspace.Snapshot.IsDirty);
        harness.ViewModel.MotorDistancePerLineUnit = ScanMotorDistanceText.MicrometersUnit;
        harness.ViewModel.MotorIntervalUs = "123456";
        await harness.FlushAsync();

        var derivedAcquisition = Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings);
        Assert.Null(derivedAcquisition.TargetLinePitchMicrometers);
        Assert.NotEmpty(harness.ViewModel.MotorDistancePerLineValue);

        harness.ViewModel.MotorDistancePerLineValue = "12.6";
        await harness.FlushAsync();

        var authoredAcquisition = Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings);
        Assert.Equal(12.6, authoredAcquisition.TargetLinePitchMicrometers);
        Assert.Equal("12.6", harness.ViewModel.MotorDistancePerLineValue);

        harness.ViewModel.SelectedRows = "256";
        await harness.FlushAsync();

        Assert.Equal(12.6, Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings).TargetLinePitchMicrometers);
    }

    [Fact]
    public async Task Todo10ProfileOwnedAcquisitionTargetLinePitch_ImportProjectionPreservesNullDerivedStateAndRestoresExplicitState()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        var nullTargetDraft = new ScanFilmProfileDraft(
            currentDraft.ProfileName,
            currentDraft.SavedAtUtc,
            currentDraft.ChannelProfiles,
            currentDraft.SelectedCalibrationChannel,
            currentDraft.AcquisitionSettings! with { TargetLinePitchMicrometers = null },
            currentDraft.ScanRecipeSettings);
        harness.Workspace.SetCurrentDraft(nullTargetDraft);
        harness.ViewModel.MotorDistancePerLineUnit = ScanMotorDistanceText.MicrometersUnit;
        var service = new ScanFilmProfileDocumentService();
        var defaultDocument = Assert.IsType<ScanFilmParameterProfileSet>(harness.Workspace.BuildExportDocument().Document.Document);
        var defaultJson = service.Serialize(defaultDocument);

        Assert.True(harness.Workspace.StageImport(defaultJson).Staged);
        await harness.Workspace.ApplyStagedImportAsync(CancellationToken.None);
        await harness.FlushAsync();

        Assert.False(harness.Workspace.Snapshot.IsDirty);
        Assert.Null(Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings).TargetLinePitchMicrometers);

        var explicitDocument = defaultDocument with
        {
            AcquisitionSettings = defaultDocument.AcquisitionSettings! with { TargetLinePitchMicrometers = 12.6, MotorIntervalNs = 3000 }
        };
        var explicitJson = service.Serialize(explicitDocument);

        Assert.True(harness.Workspace.StageImport(explicitJson).Staged);
        await harness.Workspace.ApplyStagedImportAsync(CancellationToken.None);
        await harness.FlushAsync();

        Assert.Equal(12.6, Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings).TargetLinePitchMicrometers);
        Assert.Equal("12.6", harness.ViewModel.MotorDistancePerLineValue);
    }

    [Fact]
    public async Task Todo10ProfileOwnedAcquisitionApply_PreservesDuplicateRoleSlotsAndPerSlotReversal()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Red", "Green", "Unused");

        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        var acquisition = currentDraft.AcquisitionSettings! with
        {
            AcquisitionChannelAssignment = new ScanChannelAssignment("Red", "Unused", "Green", "Unused", false, false, false, false)
        };
        var recipe = new ScanFilmScanRecipeSettings(
            new ScanChannelAssignment("Blue", "Green", "Green", "Unused", true, false, true, false));
        var updatedDraft = new ScanFilmProfileDraft(
            currentDraft.ProfileName,
            currentDraft.SavedAtUtc,
            currentDraft.ChannelProfiles,
            currentDraft.SelectedCalibrationChannel,
            acquisition,
            recipe);
        harness.Workspace.SetCurrentDraft(updatedDraft);
        await harness.FlushAsync();

        Assert.Equal(new[] { 0, 1, 2 }, harness.ViewModel.AcquisitionChannels.Select(channel => channel.LedIndex));
        Assert.Equal(new[] { "Red", "Red", "Green" }, harness.ViewModel.AcquisitionChannels.Select(channel => channel.Role));
        Assert.Equal(new[] { true, false, true }, harness.ViewModel.AcquisitionChannels.Select(channel => channel.IsSelected));
        Assert.True(harness.ViewModel.IsChannel1Reversed);
        Assert.False(harness.ViewModel.IsChannel2Reversed);
        Assert.True(harness.ViewModel.IsChannel3Reversed);
        Assert.False(harness.ViewModel.IsChannel4Reversed);
    }

    [Fact]
    public async Task Todo10ProfileOwnedAcquisitionImportProjection_ResetsOptionalOmissionsAndLeavesPreviewOnlyStateOutOfDraft()
    {
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, CreateFixtureDraft("full-v6.json"));
        var dirtyPreviewSnapshot = harness.Workspace.Snapshot;

        harness.ViewModel.IsPreviewEnabled = false;
        harness.ViewModel.IsWaterfallEnabled = false;
        harness.ViewModel.IsWhiteLevelPreviewEnabled = false;
        harness.ViewModel.PreviewGamma = "1.7";
        await harness.FlushAsync();

        Assert.True(harness.Workspace.Snapshot.CurrentDraft.HasSameContentAs(dirtyPreviewSnapshot.CurrentDraft));
        Assert.Equal(dirtyPreviewSnapshot.IsDirty, harness.Workspace.Snapshot.IsDirty);

        harness.ViewModel.SelectedRows = "333";
        harness.ViewModel.SelectedScanMotor = "Motor1";
        harness.ViewModel.MotorDistancePerLineUnit = ScanMotorDistanceText.MicrometersUnit;
        harness.ViewModel.MotorDistancePerLineValue = "7250";
        harness.ViewModel.SelectedStartingDirection = "Forward";
        harness.ViewModel.IsWarmUpEnabled = false;
        harness.ViewModel.IsAlternateMotorDirectionEnabled = true;
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = false;
        harness.ViewModel.AcquisitionChannels[0].IsSelected = true;
        harness.ViewModel.AcquisitionChannels[2].IsSelected = true;
        await harness.FlushAsync();

        var imported = new ScanFilmProfileDocumentService().Parse(ReadFixture("minimal-v5.json"));
        Assert.True(imported.CanApply);
        Assert.True(harness.Workspace.StageImport(ReadFixture("minimal-v5.json")).Staged);
        await harness.Workspace.ApplyStagedImportAsync(CancellationToken.None);
        await harness.FlushAsync();

        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        var acquisition = Assert.IsType<ScanFilmAcquisitionSettings>(harness.Workspace.Snapshot.CurrentDraft.AcquisitionSettings);
        Assert.Equal(defaults.Rows, acquisition.Rows);
        Assert.Equal(defaults.ScanMotorId, acquisition.ScanMotorId);
        Assert.Equal(defaults.TargetLinePitchMicrometers, acquisition.TargetLinePitchMicrometers);
        Assert.Equal(defaults.StartingDirectionPositive, acquisition.StartingDirectionPositive);
        Assert.Equal(defaults.WarmUpEnabled, acquisition.WarmUpEnabled);
        Assert.Equal(defaults.TransportStrategy, acquisition.TransportStrategy);
        Assert.Equal(defaults.AcquisitionChannelAssignment, acquisition.AcquisitionChannelAssignment);
        Assert.Equal(defaults.Rows.ToString(CultureInfo.InvariantCulture), harness.ViewModel.SelectedRows);
        Assert.Equal("Motor2", harness.ViewModel.SelectedScanMotor);
        Assert.Equal("Forward", harness.ViewModel.SelectedStartingDirection);
        Assert.False(harness.ViewModel.IsWarmUpEnabled);
        Assert.True(harness.ViewModel.IsAlternateMotorDirectionEnabled);
        Assert.Equal(defaults.Rows, int.Parse(harness.ViewModel.SelectedRows, CultureInfo.InvariantCulture));
        Assert.Equal(4, harness.ViewModel.AcquisitionChannels.Count(channel => channel.IsSelected));
        Assert.False(harness.Workspace.Snapshot.IsDirty);
    }

    [Fact]
    public async Task CaptureMode_DefaultTransportAndComputedProjectionsUseOneAuthoritativeSessionMode()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);

        Assert.Equal("Transport", GetCaptureModeName(harness.ViewModel));
        Assert.False(harness.ViewModel.IsContinuousScanEnabled);
        Assert.True(harness.ViewModel.IsScanMotorTransportEnabled);

        SetCaptureModeByName(harness.ViewModel, "Continuous");
        await harness.FlushAsync();

        Assert.True(harness.ViewModel.IsContinuousScanEnabled);
        Assert.False(harness.ViewModel.IsScanMotorTransportEnabled);

        SetCaptureModeByName(harness.ViewModel, "Single");
        await harness.FlushAsync();

        Assert.False(harness.ViewModel.IsContinuousScanEnabled);
        Assert.False(harness.ViewModel.IsScanMotorTransportEnabled);
    }

    [Fact]
    public async Task CaptureMode_GivenContinuousAndMultipleChannels_WhenStartRequested_ThenRetainsModeAndRejectsBeforeIo()
    {
        var startScanCalls = 0;
        var session = new StatusSession(
            isConnected: true,
            startScan: (_, _, _) =>
            {
                startScanCalls++;
                return Task.FromResult(new ScanStartResult(false, "unexpected scan", null));
            });
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var workflow = new StatusWorkflow();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator, workflow: workflow);
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = false;
        harness.ViewModel.AcquisitionChannels[0].IsSelected = true;
        harness.ViewModel.AcquisitionChannels[1].IsSelected = true;
        SetCaptureModeByName(harness.ViewModel, "Continuous");
        await harness.FlushAsync();

        Assert.False(harness.ViewModel.StartScanCommand.CanExecute(null));
        Assert.Equal("Continuous", GetCaptureModeName(harness.ViewModel));
        Assert.Equal("ScanDebug_Runtime_ErrorContinuousRequiresSingleChannel".GetLocalized(), harness.ViewModel.StartDisabledReasonText);

        await InvokeStartScanAsync(harness.ViewModel);
        await harness.FlushAsync();

        Assert.Equal("Continuous", GetCaptureModeName(harness.ViewModel));
        Assert.Equal("ScanDebug_Runtime_ErrorContinuousRequiresSingleChannel".GetLocalized(), harness.ViewModel.StatusText);
        Assert.Equal(0, workflow.ExecuteCount);
        Assert.Equal(0, coordinator.RunConnectedSessionStateCallCount);
        Assert.Equal(0, startScanCalls);
    }

    [Fact]
    public async Task CaptureMode_GivenSingleWithMultipleChannels_WhenStarted_ThenWorkflowRunsWithoutMotorTransport()
    {
        ScanWorkflowRequest? capturedRequest = null;
        var workflow = new StatusWorkflow((request, _, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new ScanWorkflowResult(1, [], 0, 0, 1_000, 100_000));
        });
        var harness = await StatusHarness.CreateAsync(CreateKnownRoleProfiles(), null, connected: true, workflow: workflow);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = false;
        harness.ViewModel.AcquisitionChannels[0].IsSelected = true;
        harness.ViewModel.AcquisitionChannels[2].IsSelected = true;
        SetCaptureModeByName(harness.ViewModel, "Single");
        await harness.FlushAsync();

        Assert.True(harness.ViewModel.StartScanCommand.CanExecute(null));
        await harness.ViewModel.StartScanCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest.EnableMotorTransport);
        Assert.Null(capturedRequest.LinePitchInput);
        Assert.Equal(["Red", "Unused", "Blue", "Unused"], capturedRequest.PassChannelRoles);
    }

    private static async Task ConfigureDeviceChannelRolesAsync(StatusHarness harness, params string[] roles)
    {
        var deviceSettings = ReadPrivateField<IScanDeviceSettingsService>(harness.ViewModel, "_deviceSettings");
        Assert.NotNull(deviceSettings);
        await deviceSettings.SetSettingsAsync(new ScanDeviceSettings(
            ScanMotorMechanicalSettings.CreateDefault(),
            ScanMotorMechanicalSettings.CreateDefault(),
            ScanMotorMechanicalSettings.CreateDefault(),
            roles[0],
            roles[1],
            roles[2],
            roles[3]));
        await harness.ViewModel.RefreshDeviceSettingsBindingsAsync();
        await harness.FlushAsync();
    }

    private static async Task CopyCalibrationAsync(StatusHarness harness, string sourceRole, string targetRole)
    {
        await harness.SelectChannelAsync(targetRole);
        harness.ViewModel.SelectedCalibrationCopySourceChannel = sourceRole;
        Assert.True(harness.ViewModel.CopyCalibrationProfileFromChannelCommand.CanExecute(null));
        harness.ViewModel.CopyCalibrationProfileFromChannelCommand.Execute(null);
        await harness.FlushAsync();
    }

    private static Task<StatusHarness> CreateAttachedHarnessAsync(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> persistedProfiles,
        ScanFilmProfileDraft? currentDraft,
        StatusAutoCalibration? autoCalibration = null,
        ushort? previewSample = null,
        bool connected = false)
        => StatusHarness.CreateAsync(
            persistedProfiles,
            currentDraft,
            attachRuntimeBindings: true,
            autoCalibration: autoCalibration,
            previewSample: previewSample,
            connected: connected);

    private static async Task PrepareTodo15CandidateAsync(StatusHarness harness, StatusSessionCoordinator coordinator)
    {
        coordinator.NotifySnapshotChanged();
        await harness.FlushAsync();
        await SetValidParameterInputsAsync(harness, "48");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
    }

    private static CalibrationChannelStatusKind GetStatus(ScanDebugViewModel viewModel, string role)
        => Assert.Single(viewModel.CalibrationChannelItems, channel => string.Equals(channel.Role, role, StringComparison.OrdinalIgnoreCase)).StatusKind;

    private static void AssertCurrentParameters(ScanDebugViewModel viewModel, ScanParameterSnapshot expected)
    {
        Assert.Equal(expected.ExposureTicks.ToString(CultureInfo.InvariantCulture), viewModel.ExposureTicks);
        Assert.Equal(expected.Adc1Offset.ToString(CultureInfo.InvariantCulture), viewModel.Adc1Offset);
        Assert.Equal(expected.Adc1Gain.ToString(CultureInfo.InvariantCulture), viewModel.Adc1Gain);
        Assert.Equal(expected.Adc2Offset.ToString(CultureInfo.InvariantCulture), viewModel.Adc2Offset);
        Assert.Equal(expected.Adc2Gain.ToString(CultureInfo.InvariantCulture), viewModel.Adc2Gain);
        Assert.Equal(expected.SysClockKhz.ToString(CultureInfo.InvariantCulture), viewModel.SysClockKhz);
    }

    private static async Task ClearCurrentEditorAsync(StatusHarness harness)
    {
        harness.ViewModel.ExposureTicks = string.Empty;
        harness.ViewModel.Adc1Offset = string.Empty;
        harness.ViewModel.Adc1Gain = string.Empty;
        harness.ViewModel.Adc2Offset = string.Empty;
        harness.ViewModel.Adc2Gain = string.Empty;
        harness.ViewModel.SysClockKhz = string.Empty;
        harness.ViewModel.RoiStartInput = string.Empty;
        harness.ViewModel.RoiEndInput = string.Empty;
        await harness.FlushAsync();
    }

    private static void AssertStatus(ScanDebugViewModel viewModel, string role, CalibrationChannelStatusKind expectedKind)
    {
        var item = Assert.Single(viewModel.CalibrationChannelItems, channel => string.Equals(channel.Role, role, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(expectedKind, item.StatusKind);
        Assert.Equal(GetExpectedStatusText(expectedKind), item.StatusText);
    }

    private static string GetExpectedStatusText(CalibrationChannelStatusKind statusKind)
        => statusKind switch
        {
            CalibrationChannelStatusKind.Saved => "ScanDebug_Runtime_ChannelStatusSaved".GetLocalized(),
            CalibrationChannelStatusKind.Modified => "ScanDebug_Runtime_ChannelStatusModified".GetLocalized(),
            CalibrationChannelStatusKind.Invalid => "ScanDebug_Runtime_ChannelStatusInvalid".GetLocalized(),
            CalibrationChannelStatusKind.Missing => "ScanDebug_Runtime_ChannelStatusMissing".GetLocalized(),
            CalibrationChannelStatusKind.CopiedUnverified => "ScanDebug_Runtime_ChannelStatusCopiedUnverified".GetLocalized(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusKind), statusKind, null)
        };

    private static ScanFilmProfileDraft CreateDraft(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> profiles,
        string selectedChannel)
        => new(
            "Status ownership",
            DateTimeOffset.UnixEpoch,
            profiles,
            selectedChannel,
            ScanFilmAcquisitionSettings.CreateDefault(),
            null);

    private static ScanFilmProfileDraft CreateFixtureDraft(string fileName)
        => ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(new ScanFilmProfileDocumentService().Parse(ReadFixture(fileName)).Document)).Draft;

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_UsesServiceRowsMetadataForSparseActivePassesAndClearsOnFinalResult()
    {
        var firstSnapshotQueued = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondSnapshot = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSnapshotQueued = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFinalResult = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var roles = new[] { "Unused", "Blue", "Unused", "IR" };
        var profiles = new[]
        {
            new ScanParameterSnapshot(0, 0, 0, 0, 0, 100_000),
            new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 100_000),
            new ScanParameterSnapshot(0, 0, 0, 0, 0, 100_000),
            new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 50_000)
        };
        var finalResult = new ScanWorkflowResult(
            2,
            new[]
            {
                new ScanPassCapture(2, 1, true, 2, 101, new byte[ScanDebugConstants.BytesPerLine * 2]),
                new ScanPassCapture(4, 3, false, 2, 202, new byte[ScanDebugConstants.BytesPerLine * 2])
            },
            101,
            111_111,
            1_000,
            100_000);
        var workflow = new StatusWorkflow(async (_, onRowsAvailable, ct) =>
        {
            onRowsAvailable!(new ScanWorkflowRowsAvailable(
                1, 2, 1, 1, true, "Blue", new byte[ScanDebugConstants.BytesPerLine * 2], 2, 101, 111_111));
            firstSnapshotQueued.TrySetResult(null);
            await releaseSecondSnapshot.Task.WaitAsync(ct);
            onRowsAvailable!(new ScanWorkflowRowsAvailable(
                2, 2, 3, 3, false, "IR", new byte[ScanDebugConstants.BytesPerLine * 2], 2, 202, 222_222));
            secondSnapshotQueued.TrySetResult(null);
            await releaseFinalResult.Task.WaitAsync(ct);
            return finalResult;
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;
        var request = new ScanWorkflowRequest(
            2,
            false,
            [0, 0, 0, 0],
            roles,
            profiles,
            0,
            999_999,
            true,
            true,
            1_000,
            100_000,
            EnableMotorTransport: true,
            EnableLedAutoControl: false,
            LinePitchInput: new ScanWorkflowLinePitchInput(
                5.0,
                new ScanMotorMechanicalSettings(200, 16, 8.0),
                ScanDebugConstants.MotionMinIntervalNs,
                [null, null, null, null]));

        var workflowTask = InvokeRunWorkflowScanAsync(harness.ViewModel, request);
        await firstSnapshotQueued.Task;
        await harness.FlushAsync();

        var firstPreview = Assert.Single(channelImages.PartialResults);
        Assert.Equal(111_111u, firstPreview.MotorIntervalNs);
        Assert.Equal(101u, firstPreview.ComputedMotorStepsPerPass);
        Assert.Equal((true, 101u), (firstPreview.Passes[1].DirectionPositive, firstPreview.Passes[1].MotorSteps));
        Assert.Equal((false, 0u), (firstPreview.Passes[3].DirectionPositive, firstPreview.Passes[3].MotorSteps));

        releaseSecondSnapshot.TrySetResult(null);
        await secondSnapshotQueued.Task;
        InvokeApplyStreamingPreviewFrame(harness.ViewModel);
        await harness.FlushAsync();

        var secondPreview = channelImages.PartialResults.Last();
        Assert.Equal(111_111u, secondPreview.MotorIntervalNs);
        Assert.Equal(101u, secondPreview.ComputedMotorStepsPerPass);
        Assert.Equal((true, 101u), (secondPreview.Passes[1].DirectionPositive, secondPreview.Passes[1].MotorSteps));
        Assert.Equal((false, 202u), (secondPreview.Passes[3].DirectionPositive, secondPreview.Passes[3].MotorSteps));

        releaseFinalResult.TrySetResult(null);
        await workflowTask;
        await harness.FlushAsync();

        Assert.Null(ReadPrivateField<ScanWorkflowResult>(harness.ViewModel, "_streamingWorkflowPreviewResult"));
        Assert.Same(finalResult, ReadPrivateField<ScanWorkflowResult>(harness.ViewModel, "_lastWorkflowResult"));
    }

    [Fact]
    public async Task StreamingWorkflowPreview_QueuedApplyReleasedAfterFinalHandoff_DoesNotClearFinalResult()
    {
        var finalResult = CreateStreamingPreviewFinalResult();
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            onRowsAvailable!(CreateStreamingPreviewSnapshot());
            return Task.FromResult(finalResult);
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        var workflowTask = InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        await harness.Dispatcher.WaitForPendingAsync();
        await workflowTask;

        Assert.Same(finalResult, ReadPrivateField<ScanWorkflowResult>(harness.ViewModel, "_lastWorkflowResult"));
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, finalResult);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_CallbackAfterFinalHandoff_DoesNotResurrectStreamingState()
    {
        ScanWorkflowRowsAvailableHandler? lateCallback = null;
        var finalResult = CreateStreamingPreviewFinalResult();
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            lateCallback = onRowsAvailable;
            return Task.FromResult(finalResult);
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        lateCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, finalResult);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_CallbackAfterSuccessfulNoDataResult_DoesNotResurrectStreamingState()
    {
        ScanWorkflowRowsAvailableHandler? lateCallback = null;
        var noDataResult = new ScanWorkflowResult(1, [], 0, 0, 1_000, 100_000);
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            lateCallback = onRowsAvailable;
            return Task.FromResult(noDataResult);
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        lateCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, finalResult: null);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_AppliedPartialThenSuccessfulNoData_ClearsStateAndIgnoresRetainedCallback()
    {
        ScanWorkflowRowsAvailableHandler? retainedCallback = null;
        var releaseNoDataResult = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new StatusWorkflow(async (_, onRowsAvailable, ct) =>
        {
            retainedCallback = onRowsAvailable;
            onRowsAvailable!(CreateStreamingPreviewSnapshot());
            await releaseNoDataResult.Task.WaitAsync(ct);
            return new ScanWorkflowResult(1, [], 0, 0, 1_000, 100_000);
        });
        var previewPresenter = new StatusPreviewPresenter();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, previewPresenter: previewPresenter);
        harness.ViewModel.IsPreviewEnabled = true;

        var workflowTask = InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        await harness.Dispatcher.WaitForPendingAsync();
        harness.Dispatcher.DrainAll();

        Assert.NotNull(ReadPrivateField<ScanWorkflowResult>(harness.ViewModel, "_streamingWorkflowPreviewResult"));
        Assert.NotNull(ReadPrivateField<ScanChannelAssignment>(harness.ViewModel, "_streamingWorkflowPreviewAssignment"));
        var completedRows = Assert.Single(ReadPrivateField<Dictionary<int, int>>(harness.ViewModel, "_streamingWorkflowPreviewCompletedRowsByPassIndex")!);
        Assert.Equal(0, completedRows.Key);
        Assert.Equal(1, completedRows.Value);
        Assert.Single(previewPresenter.RenderedBuffers);

        releaseNoDataResult.TrySetResult(null);
        await workflowTask;

        Assert.Null(ReadPrivateField<ScanWorkflowResult>(harness.ViewModel, "_streamingWorkflowPreviewResult"));
        Assert.Null(ReadPrivateField<ScanChannelAssignment>(harness.ViewModel, "_streamingWorkflowPreviewAssignment"));
        Assert.Null(ReadPrivateField<Dictionary<int, int>>(harness.ViewModel, "_streamingWorkflowPreviewCompletedRowsByPassIndex"));
        var renderedBufferCount = previewPresenter.RenderedBuffers.Count;
        retainedCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        Assert.Equal(renderedBufferCount, previewPresenter.RenderedBuffers.Count);
        Assert.False(ReadPrivateField<bool>(harness.ViewModel, "_isStreamingPreviewActive"));
    }

    [Fact]
    public async Task StreamingWorkflowPreview_PreviousGenerationCallbackAfterNewScanBegins_DoesNotRenderStaleData()
    {
        ScanWorkflowRowsAvailableHandler? previousCallback = null;
        var firstResult = CreateStreamingPreviewFinalResult();
        var secondResult = CreateStreamingPreviewFinalResult();
        var invocation = 0;
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            invocation++;
            if (invocation == 1)
                previousCallback = onRowsAvailable;
            else
                onRowsAvailable!(CreateStreamingPreviewSnapshot());

            return Task.FromResult(invocation == 1 ? firstResult : secondResult);
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        previousCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, secondResult);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_CallbackAfterCancellation_DoesNotResurrectStreamingState()
    {
        ScanWorkflowRowsAvailableHandler? lateCallback = null;
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            lateCallback = onRowsAvailable;
            return Task.FromCanceled<ScanWorkflowResult>(new CancellationToken(canceled: true));
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        lateCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, finalResult: null);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_CallbackAfterFailure_DoesNotResurrectStreamingState()
    {
        ScanWorkflowRowsAvailableHandler? lateCallback = null;
        var workflow = new StatusWorkflow((_, onRowsAvailable, _) =>
        {
            lateCallback = onRowsAvailable;
            return Task.FromException<ScanWorkflowResult>(new InvalidOperationException("workflow failure"));
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        await InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        lateCallback!(CreateStreamingPreviewSnapshot());
        harness.Dispatcher.DrainAll();

        AssertNoStreamingPreview(harness.ViewModel, channelImages, finalResult: null);
    }

    [Fact]
    public async Task SingleScanPreview_CoordinatorThrowsAfterQueuedRowsCallback_DoesNotRenderAfterReturn()
    {
        var session = new StatusSession(
            isConnected: true,
            startScan: (rows, _, onRowsAvailable) =>
            {
                onRowsAvailable!(new byte[rows * ScanDebugConstants.BytesPerLine], rows);
                return Task.FromResult(new ScanStartResult(true, "unused", []));
            });
        var coordinator = new StatusSessionCoordinator(session, new InvalidOperationException("coordinator failed"));
        var previewPresenter = new StatusPreviewPresenter();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            previewPresenter: previewPresenter,
            scanSession: session,
            sessionCoordinator: coordinator);
        harness.ViewModel.IsPreviewEnabled = true;

        var scanTask = InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.Dispatcher.WaitForPendingAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanTask);
        harness.Dispatcher.DrainAll();

        AssertSingleScanPreviewEnded(harness.ViewModel, previewPresenter);
    }

    [Fact]
    public async Task SingleScanPreview_SessionThrowsAfterQueuedRowsCallback_DoesNotRenderAfterReturn()
    {
        var previewPresenter = new StatusPreviewPresenter();
        var harness = await CreateSingleScanHarnessAsync(
            previewPresenter,
            (rows, _, onRowsAvailable) =>
            {
                onRowsAvailable!(new byte[rows * ScanDebugConstants.BytesPerLine], rows);
                return Task.FromException<ScanStartResult>(new InvalidOperationException("session failed"));
            });

        var scanTask = InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.Dispatcher.WaitForPendingAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanTask);
        harness.Dispatcher.DrainAll();

        AssertSingleScanPreviewEnded(harness.ViewModel, previewPresenter);
    }

    [Fact]
    public async Task SingleScanPreview_SessionCancelsAfterQueuedRowsCallback_DoesNotRenderAfterReturn()
    {
        var previewPresenter = new StatusPreviewPresenter();
        var harness = await CreateSingleScanHarnessAsync(
            previewPresenter,
            (rows, _, onRowsAvailable) =>
            {
                onRowsAvailable!(new byte[rows * ScanDebugConstants.BytesPerLine], rows);
                return Task.FromCanceled<ScanStartResult>(new CancellationToken(canceled: true));
            });

        var scanTask = InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.Dispatcher.WaitForPendingAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanTask);
        harness.Dispatcher.DrainAll();

        AssertSingleScanPreviewEnded(harness.ViewModel, previewPresenter);
    }

    [Fact]
    public async Task SingleScanPreview_UnsuccessfulResultAfterQueuedRowsCallback_PreservesNoDataBehavior()
    {
        var previewPresenter = new StatusPreviewPresenter();
        var harness = await CreateSingleScanHarnessAsync(
            previewPresenter,
            (rows, _, onRowsAvailable) =>
            {
                onRowsAvailable!(new byte[rows * ScanDebugConstants.BytesPerLine], rows);
                return Task.FromResult(new ScanStartResult(false, "single scan rejected", null));
            });
        harness.ViewModel.StatusText = "before scan";

        var scanTask = InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.Dispatcher.WaitForPendingAsync();
        await scanTask;
        harness.Dispatcher.DrainAll();

        AssertSingleScanPreviewEnded(harness.ViewModel, previewPresenter);
        Assert.Equal("single scan rejected", harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task StreamingWorkflowPreview_DetachedPassBuffersStayImmutableWhenRowsCallbackMutatesDuringPartialConsumer()
    {
        ScanWorkflowRowsAvailableHandler? retainedCallback = null;
        var releaseFinalResult = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var partialConsumerEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePartialConsumer = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preMutationBytes = Enumerable.Repeat((byte)0x11, ScanDebugConstants.BytesPerLine).ToArray();
        var postMutationBytes = Enumerable.Repeat((byte)0x22, ScanDebugConstants.BytesPerLine).ToArray();
        var workflow = new StatusWorkflow(async (_, onRowsAvailable, ct) =>
        {
            retainedCallback = onRowsAvailable;
            onRowsAvailable!(CreateStreamingPreviewSnapshot(preMutationBytes));
            await releaseFinalResult.Task.WaitAsync(ct);
            return CreateStreamingPreviewFinalResult();
        });
        var previewPresenter = new StatusPreviewPresenter(partialConsumerEntered, releasePartialConsumer);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, workflow: workflow, previewPresenter: previewPresenter);
        harness.ViewModel.IsPreviewEnabled = true;

        var workflowTask = InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingPreviewRequest());
        await harness.Dispatcher.WaitForPendingAsync();
        var applyTask = Task.Run(() => InvokeApplyStreamingPreviewFrame(harness.ViewModel));
        await partialConsumerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        retainedCallback!(CreateStreamingPreviewSnapshot(postMutationBytes));
        releasePartialConsumer.TrySetResult(null);
        await applyTask;

        var detachedPreviewBuffer = Assert.Single(previewPresenter.RenderedBuffers);
        Assert.All(detachedPreviewBuffer, value => Assert.Equal((byte)0x11, value));

        releaseFinalResult.TrySetResult(null);
        await workflowTask;
        harness.Dispatcher.DrainAll();
    }

    [Fact]
    public async Task StreamingWorkflowPreview_DeltasMergeInOrderAtExactRowOffsets()
    {
        var firstRow = CreateStreamingPreviewRow(0x11);
        var secondRow = CreateStreamingPreviewRow(0x22);
        var thirdRow = CreateStreamingPreviewRow(0x33);
        var scenario = await StartStreamingDeltaPreviewAsync(
            3,
            onRowsAvailable =>
            {
                onRowsAvailable(CreateStreamingDeltaSnapshot(0, 1, 1, firstRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(1, 1, 2, secondRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(2, 1, 3, thirdRow));
            });

        try
        {
            var preview = Assert.Single(scenario.ChannelImages.PartialResults);
            AssertPassRows(preview.Passes[0], firstRow, secondRow, thirdRow);

            var completedRows = Assert.Single(scenario.ChannelImages.PartialCompletedRows);
            Assert.Equal(3, completedRows[0]);
        }
        finally
        {
            await scenario.CompleteAsync();
        }
    }

    [Fact]
    public async Task StreamingWorkflowPreview_StaleDeltaCannotOverwriteOrRegressCompletedRows()
    {
        var firstRow = CreateStreamingPreviewRow(0x41);
        var secondRow = CreateStreamingPreviewRow(0x52);
        var staleRow = CreateStreamingPreviewRow(0xF3);
        var scenario = await StartStreamingDeltaPreviewAsync(
            3,
            onRowsAvailable =>
            {
                onRowsAvailable(CreateStreamingDeltaSnapshot(0, 1, 1, firstRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(1, 1, 2, secondRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(0, 1, 1, staleRow));
            });

        try
        {
            var preview = Assert.Single(scenario.ChannelImages.PartialResults);
            AssertPassRows(preview.Passes[0], firstRow, secondRow, new byte[ScanDebugConstants.BytesPerLine]);

            var completedRows = Assert.Single(scenario.ChannelImages.PartialCompletedRows);
            Assert.Equal(2, completedRows[0]);
        }
        finally
        {
            await scenario.CompleteAsync();
        }
    }

    [Fact]
    public async Task StreamingWorkflowPreview_GappedDeltaCannotMutateOrAdvanceCompletedRows()
    {
        var firstRow = CreateStreamingPreviewRow(0x61);
        var gappedRow = CreateStreamingPreviewRow(0xD2);
        var scenario = await StartStreamingDeltaPreviewAsync(
            3,
            onRowsAvailable =>
            {
                onRowsAvailable(CreateStreamingDeltaSnapshot(0, 1, 1, firstRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(2, 1, 3, gappedRow));
            });

        try
        {
            var preview = Assert.Single(scenario.ChannelImages.PartialResults);
            AssertPassRows(
                preview.Passes[0],
                firstRow,
                new byte[ScanDebugConstants.BytesPerLine],
                new byte[ScanDebugConstants.BytesPerLine]);

            var completedRows = Assert.Single(scenario.ChannelImages.PartialCompletedRows);
            Assert.Equal(1, completedRows[0]);
        }
        finally
        {
            await scenario.CompleteAsync();
        }
    }

    [Fact]
    public async Task StreamingWorkflowPreview_MalformedDeltaLengthIsAtomicallyRejected()
    {
        var firstRow = CreateStreamingPreviewRow(0x71);
        var malformedRow = CreateStreamingPreviewRow(0xE2)[..^1];
        var scenario = await StartStreamingDeltaPreviewAsync(
            3,
            onRowsAvailable =>
            {
                onRowsAvailable(CreateStreamingDeltaSnapshot(0, 1, 1, firstRow));
                onRowsAvailable(CreateStreamingDeltaSnapshot(1, 1, 2, malformedRow));
            });

        try
        {
            var preview = Assert.Single(scenario.ChannelImages.PartialResults);
            AssertPassRows(
                preview.Passes[0],
                firstRow,
                new byte[ScanDebugConstants.BytesPerLine],
                new byte[ScanDebugConstants.BytesPerLine]);

            var completedRows = Assert.Single(scenario.ChannelImages.PartialCompletedRows);
            Assert.Equal(1, completedRows[0]);
        }
        finally
        {
            await scenario.CompleteAsync();
        }
    }

    [Fact]
    public async Task StreamingWorkflowPreview_LegacyWholeBufferCopiesOnlyCompletedPrefix()
    {
        var firstRow = CreateStreamingPreviewRow(0x81);
        var secondRow = CreateStreamingPreviewRow(0x92);
        var untouchedRow = CreateStreamingPreviewRow(0xA3);
        var fullTargetBuffer = firstRow.Concat(secondRow).Concat(untouchedRow).ToArray();
        var legacySnapshot = new ScanWorkflowRowsAvailable(
            1,
            2,
            0,
            0,
            true,
            "Red",
            fullTargetBuffer,
            2);
        Assert.Equal(-1, legacySnapshot.RowCount);
        var scenario = await StartStreamingDeltaPreviewAsync(3, onRowsAvailable => onRowsAvailable(legacySnapshot));

        try
        {
            var preview = Assert.Single(scenario.ChannelImages.PartialResults);
            AssertPassRows(preview.Passes[0], firstRow, secondRow, new byte[ScanDebugConstants.BytesPerLine]);

            var completedRows = Assert.Single(scenario.ChannelImages.PartialCompletedRows);
            Assert.Equal(2, completedRows[0]);
        }
        finally
        {
            await scenario.CompleteAsync();
        }
    }

    private static async Task<StreamingWorkflowPreviewScenario> StartStreamingDeltaPreviewAsync(
        int rows,
        Action<ScanWorkflowRowsAvailableHandler> publishRows)
    {
        var releaseFinalResult = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new StatusWorkflow(async (_, onRowsAvailable, ct) =>
        {
            publishRows(onRowsAvailable!);
            await releaseFinalResult.Task.WaitAsync(ct);
            return new ScanWorkflowResult(rows, [], 0, 0, 1_000, 100_000);
        });
        var channelImages = new StatusChannelImages();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            workflow: workflow,
            channelImages: channelImages);
        harness.ViewModel.IsPreviewEnabled = true;

        var workflowTask = InvokeRunWorkflowScanAsync(harness.ViewModel, CreateStreamingDeltaPreviewRequest(rows));
        await harness.Dispatcher.WaitForPendingAsync();
        await harness.FlushAsync();

        return new StreamingWorkflowPreviewScenario(harness, channelImages, workflowTask, releaseFinalResult);
    }

    private static ScanWorkflowRequest CreateStreamingDeltaPreviewRequest(int rows)
        => new(
            rows,
            false,
            [0, 0],
            ["Red", "Green"],
            [
                new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 100_000),
                new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 100_000)
            ],
            0,
            999_999,
            true,
            false,
            1_000,
            100_000,
            EnableMotorTransport: false,
            EnableLedAutoControl: false);

    private static ScanWorkflowRowsAvailable CreateStreamingDeltaSnapshot(
        int startRow,
        int rowCount,
        int completedRows,
        byte[] imageBytes)
        => new(1, 2, 0, 0, true, "Red", imageBytes, completedRows)
        {
            StartRow = startRow,
            RowCount = rowCount
        };

    private static byte[] CreateStreamingPreviewRow(byte value)
        => Enumerable.Repeat(value, ScanDebugConstants.BytesPerLine).ToArray();

    private static void AssertPassRows(ScanPassCapture pass, params byte[][] expectedRows)
    {
        Assert.Equal(expectedRows.Length * ScanDebugConstants.BytesPerLine, pass.ImageBytes.Length);
        for (var row = 0; row < expectedRows.Length; row++)
        {
            var offset = row * ScanDebugConstants.BytesPerLine;
            Assert.Equal(expectedRows[row], pass.ImageBytes.AsSpan(offset, ScanDebugConstants.BytesPerLine).ToArray());
        }
    }

    private sealed class StreamingWorkflowPreviewScenario
    {
        private readonly StatusHarness _harness;
        private readonly Task _workflowTask;
        private readonly TaskCompletionSource<object?> _releaseFinalResult;

        public StreamingWorkflowPreviewScenario(
            StatusHarness harness,
            StatusChannelImages channelImages,
            Task workflowTask,
            TaskCompletionSource<object?> releaseFinalResult)
        {
            _harness = harness;
            ChannelImages = channelImages;
            _workflowTask = workflowTask;
            _releaseFinalResult = releaseFinalResult;
        }

        public StatusChannelImages ChannelImages { get; }

        public async Task CompleteAsync()
        {
            _releaseFinalResult.TrySetResult(null);
            await _workflowTask;
            await _harness.FlushAsync();
        }
    }

    private static ScanWorkflowRequest CreateStreamingPreviewRequest()
        => new(
            1,
            false,
            [0],
            ["Blue"],
            [new ScanParameterSnapshot(1_000, 0, 0, 0, 0, 100_000)],
            0,
            999_999,
            true,
            false,
            1_000,
            100_000,
            EnableMotorTransport: false,
            EnableLedAutoControl: false);

    private static ScanWorkflowRowsAvailable CreateStreamingPreviewSnapshot(byte[]? imageBytes = null)
        => new(1, 1, 0, 0, true, "Blue", imageBytes ?? new byte[ScanDebugConstants.BytesPerLine], 1, 101, 111_111);

    private static ScanWorkflowResult CreateStreamingPreviewFinalResult()
        => new(
            1,
            [new ScanPassCapture(1, 0, true, 1, 101, new byte[ScanDebugConstants.BytesPerLine])],
            101,
            111_111,
            1_000,
            100_000);

    private static void AssertNoStreamingPreview(ScanDebugViewModel viewModel, StatusChannelImages channelImages, ScanWorkflowResult? finalResult)
    {
        Assert.Null(ReadPrivateField<ScanWorkflowResult>(viewModel, "_streamingWorkflowPreviewResult"));
        Assert.Same(finalResult, ReadPrivateField<ScanWorkflowResult>(viewModel, "_lastWorkflowResult"));
        Assert.Empty(channelImages.PartialResults);
    }

    private static Task InvokeRunWorkflowScanAsync(ScanDebugViewModel viewModel, ScanWorkflowRequest request)
        => (Task)typeof(ScanDebugViewModel)
            .GetMethod("RunWorkflowScanAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [request, CancellationToken.None])!;

    private static Task InvokeRunSingleScanAsync(ScanDebugViewModel viewModel, int rows, CancellationToken ct)
        => (Task)typeof(ScanDebugViewModel)
            .GetMethod("RunSingleScanAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [rows, ct])!;

    private static Task InvokeStartScanAsync(ScanDebugViewModel viewModel)
        => (Task)typeof(ScanDebugViewModel)
            .GetMethod("StartScan", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null)!;

    private static string GetCaptureModeName(ScanDebugViewModel viewModel)
    {
        var property = typeof(ScanDebugViewModel).GetProperty("SelectedCaptureMode");
        Assert.NotNull(property);
        return property.GetValue(viewModel)?.ToString() ?? string.Empty;
    }

    private static void SetCaptureModeByName(ScanDebugViewModel viewModel, string modeName)
    {
        var property = typeof(ScanDebugViewModel).GetProperty("SelectedCaptureMode");
        Assert.NotNull(property);
        Assert.True(property.PropertyType.IsEnum, "SelectedCaptureMode must be a typed enum.");
        property.SetValue(viewModel, Enum.Parse(property.PropertyType, modeName));
    }

    private static async Task<StatusHarness> CreateSingleScanHarnessAsync(
        StatusPreviewPresenter previewPresenter,
        Func<int, CancellationToken, ScanRowsAvailableHandler?, Task<ScanStartResult>> startScan)
    {
        var session = new StatusSession(isConnected: true, startScan: startScan);
        return await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            previewPresenter: previewPresenter,
            scanSession: session);
    }

    private static void AssertSingleScanPreviewEnded(ScanDebugViewModel viewModel, StatusPreviewPresenter previewPresenter)
    {
        Assert.False(ReadPrivateField<bool>(viewModel, "_isStreamingPreviewActive"));
        Assert.False(ReadPrivateField<bool>(viewModel, "_isStreamingPreviewQueued"));
        Assert.Equal(0, previewPresenter.RenderCallCount);
        Assert.Null(ReadPrivateField<ScanWorkflowResult>(viewModel, "_lastWorkflowResult"));
    }

    private static T? ReadPrivateField<T>(ScanDebugViewModel viewModel, string fieldName)
        => (T?)typeof(ScanDebugViewModel)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(viewModel);

    private static void SetPrivateField<T>(ScanDebugViewModel viewModel, string fieldName, T value)
        => typeof(ScanDebugViewModel)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, value);

    private static void InvokeApplyStreamingPreviewFrame(ScanDebugViewModel viewModel)
    {
        var previewSessionVersion = ReadPrivateField<int>(viewModel, "_streamingPreviewSessionVersion");
        typeof(ScanDebugViewModel)
            .GetMethod("ApplyStreamingPreviewFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [previewSessionVersion]);
    }

    private static void InvokeApplyScanFrame(ScanDebugViewModel viewModel)
    {
        typeof(ScanDebugViewModel)
            .GetMethod("ApplyScanFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [new byte[ScanDebugConstants.BytesPerLine], 1, "test frame"]);
        var width = ScanDebugConstants.DecodedPixelsPerLine;
        viewModel.PreviewFrame = new ScanPreviewFrame(new byte[width * 4], width, 1, width * 4, ScanPreviewPixelFormat.Bgra8, 1);
        typeof(ScanDebugViewModel)
            .GetMethod("RefreshColumnSampleStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null);
    }

    private static void InvokeSetDeviceTimingState(ScanDebugViewModel viewModel, ScanDeviceTimingState timingState)
        => typeof(ScanDebugViewModel)
            .GetMethod("SetDeviceTimingState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [timingState]);

    private static async Task InvokeApplyParametersAsync(ScanDebugViewModel viewModel)
    {
        var task = Assert.IsAssignableFrom<Task>(typeof(ScanDebugViewModel)
            .GetMethod("ApplyParameters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null));
        await task;
    }

    private static bool InvokeTryBuildDebugWorkflowRequest(ScanDebugViewModel viewModel, int rows, out string error)
    {
        object?[] arguments = [rows, null, string.Empty];
        var result = (bool)typeof(ScanDebugViewModel)
            .GetMethod("TryBuildDebugWorkflowRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, arguments)!;
        error = Assert.IsType<string>(arguments[2]);
        return result;
    }

    private static bool InvokeTryBuildAutofocusRequest(ScanDebugViewModel viewModel, out string error)
    {
        object?[] arguments = [null, string.Empty];
        var result = (bool)typeof(ScanDebugViewModel)
            .GetMethod("TryBuildAutofocusRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, arguments)!;
        error = Assert.IsType<string>(arguments[1]);
        return result;
    }

    private static async Task SetValidParameterInputsAsync(StatusHarness harness, string sysClockMhz)
    {
        var sysClockKhz = (uint)(decimal.Parse(sysClockMhz, CultureInfo.InvariantCulture) * 1_000m);
        harness.ViewModel.ExposureMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(1_000, sysClockKhz)
            .ToString("0.###", CultureInfo.InvariantCulture);
        harness.ViewModel.Adc1Offset = "0";
        harness.ViewModel.Adc1Gain = "1";
        harness.ViewModel.Adc2Offset = "0";
        harness.ViewModel.Adc2Gain = "1";
        harness.ViewModel.SysClockMhz = sysClockMhz;
        await harness.FlushAsync();
    }

    private static IReadOnlyDictionary<string, ScanChannelCalibrationProfile> CreateKnownRoleProfiles()
        => new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Red"] = CreateProfile(1_000),
            ["Green"] = CreateProfile(1_000),
            ["Blue"] = CreateProfile(1_000),
            ["IR"] = CreateProfile(1_000)
        };

    [Fact]
    public void DeviceTimingState_GivenReadRequiredRole_WhenCheckingDeviceRead_ThenReportsThatRole()
    {
        var state = new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, ["Red"]);

        Assert.True(state.RequiresDeviceRead);
        Assert.True(state.RequiresDeviceReadForChannel("Red"));
        Assert.False(state.RequiresDeviceReadForChannel("Green"));
    }

    [Fact]
    public void DeviceTimingState_GivenDeviceKnownWithPendingRoles_WhenCheckingRevalidation_ThenPreservesRoleMembershipWithoutDeviceRead()
    {
        var state = new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]);

        Assert.True(state.IsRevalidationRequired("Red"));
        Assert.True(state.IsRevalidationRequired("Green"));
        Assert.False(state.RequiresDeviceRead);
        Assert.False(state.RequiresDeviceReadForChannel("Red"));
    }

    [Fact]
    public async Task DeviceTimingState_GivenDeviceKnownPendingRoles_WhenApplyingSelectedChannelParametersSucceeds_ThenOnlySelectedRoleIsRevalidated()
    {
        var applyEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(applyEntered, releaseApply);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        var exposureMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(1_000, 125_000)
            .ToString("0.###", CultureInfo.InvariantCulture);

        harness.ViewModel.ExposureMicroseconds = exposureMicroseconds;
        harness.ViewModel.Adc1Offset = "0";
        harness.ViewModel.Adc1Gain = "1";
        harness.ViewModel.Adc2Offset = "0";
        harness.ViewModel.Adc2Gain = "1";
        harness.ViewModel.SysClockMhz = "125";
        await harness.FlushAsync();
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));

        var applyTask = harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await applyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["Red", "Green"], harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
        releaseApply.TrySetResult(null);
        await applyTask;
        await harness.FlushAsync();

        var timingState = harness.ViewModel.DeviceTimingState;
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, timingState.StateKind);
        Assert.Equal(["Green"], timingState.RevalidationRequiredChannelRoles);
        Assert.False(timingState.RequiresDeviceRead);
        Assert.False(timingState.IsRevalidationRequired("Red"));
        Assert.True(timingState.IsRevalidationRequired("Green"));
        Assert.False(timingState.RequiresDeviceReadForChannel("Green"));
    }

    [Fact]
    public async Task DeviceTimingState_GivenGlobalClockWriteThenClockCancellation_WhenApplyingDeviceClock_ThenRequiresReadForEveryKnownRole()
    {
        var globalClockApplied = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(globalClockApplied: globalClockApplied, cancelGlobalClockApply: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));

        Assert.True(harness.ViewModel.ApplyDeviceClockCommand.CanExecute(null));
        var applyTask = harness.ViewModel.ApplyDeviceClockCommand.ExecuteAsync(null);
        await globalClockApplied.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await applyTask;
        await harness.FlushAsync();

        var timingState = harness.ViewModel.DeviceTimingState;
        Assert.Equal(1, parameters.GlobalClockApplyCount);
        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.ReadRequired, timingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, timingState.RevalidationRequiredChannelRoles);
        Assert.Contains(harness.ViewModel.SelectedCalibrationChannel, timingState.RevalidationRequiredChannelRoles, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("ScanDebug_Runtime_StatusDeviceClockUpdateCanceled".GetLocalized(), harness.ViewModel.StatusText);
        Assert.NotEqual("ScanDebug_Runtime_StatusParametersUpdated".GetLocalized(), harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task DeviceTimingState_GivenKnownClock_WhenApplyingSelectedChannelParameters_ThenNeverWritesGlobalClockAndClearsOnlyCapturedRole()
    {
        var applyEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(applyEntered, releaseApply);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));

        var applyTask = harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await applyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.SelectedCalibrationChannel = "Blue";
        await harness.FlushAsync();
        releaseApply.TrySetResult(null);
        await applyTask;
        await harness.FlushAsync();

        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(1, parameters.ChannelApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.False(harness.ViewModel.DeviceTimingState.IsRevalidationRequired("Red"));
        Assert.True(harness.ViewModel.DeviceTimingState.IsRevalidationRequired("Green"));
        Assert.False(harness.ViewModel.DeviceTimingState.IsRevalidationRequired("Blue"));
    }

    [Theory]
    [InlineData(ScanDeviceClockStateKind.Unknown)]
    [InlineData(ScanDeviceClockStateKind.Edited)]
    [InlineData(ScanDeviceClockStateKind.ReadRequired)]
    public async Task DeviceTimingState_GivenClockStateNotDeviceKnown_WhenDirectlyApplyingSelectedChannelParameters_ThenRejectsWithoutWrites(
        ScanDeviceClockStateKind stateKind)
    {
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        var unsafeTimingState = new ScanDeviceTimingState(stateKind, ["Red", "Green"]);
        InvokeSetDeviceTimingState(harness.ViewModel, unsafeTimingState);

        Assert.False(harness.ViewModel.ApplyParametersCommand.CanExecute(null));
        await InvokeApplyParametersAsync(harness.ViewModel);
        await harness.FlushAsync();

        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(stateKind, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(unsafeTimingState.RevalidationRequiredChannelRoles, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenClockEditWhileChannelApplyPending_WhenOriginalApplyCompletes_ThenNewerEditAndPendingRolesRemain()
    {
        var applyEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(applyEntered, releaseApply);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));

        var applyTask = harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await applyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.SysClockMhz = "126";
        await harness.FlushAsync();
        releaseApply.TrySetResult(null);
        await applyTask;
        await harness.FlushAsync();

        Assert.Equal(1, parameters.ChannelApplyCount);
        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.Edited, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenReadRequiredWhileChannelApplyPending_WhenOriginalApplyCompletes_ThenNewerReadRequiredAndPendingRolesRemain()
    {
        var applyEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(applyEntered, releaseApply);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));

        var applyTask = harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await applyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, harness.ViewModel.CalibrationChannelOptions));
        releaseApply.TrySetResult(null);
        await applyTask;
        await harness.FlushAsync();

        Assert.Equal(1, parameters.ChannelApplyCount);
        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.ReadRequired, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenClockEditDuringSynchronousChannelApplyClaim_WhenApplyingParameters_ThenRejectsBeforeDeviceWrite()
    {
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));
        var claims = ReadPrivateField<ScanDebugRuntimeOperationClaims>(harness.ViewModel, "_runtimeOperationClaims")!;
        var changed = 0;
        EventHandler? editClockOnClaim = null;
        editClockOnClaim = (_, _) =>
        {
            claims.Changed -= editClockOnClaim;
            changed++;
            harness.ViewModel.SysClockMhz = "126";
        };
        claims.Changed += editClockOnClaim;

        await harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(1, changed);
        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.Edited, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenSelectedChannelChangesDuringSynchronousChannelApplyClaim_WhenApplyingParameters_ThenRejectsBeforeClearingAnotherRole()
    {
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, ["Red", "Green"]));
        var claims = ReadPrivateField<ScanDebugRuntimeOperationClaims>(harness.ViewModel, "_runtimeOperationClaims")!;
        var changed = 0;
        EventHandler? changeChannelOnClaim = null;
        changeChannelOnClaim = (_, _) =>
        {
            claims.Changed -= changeChannelOnClaim;
            changed++;
            harness.ViewModel.SelectedCalibrationChannel = "Green";
        };
        claims.Changed += changeChannelOnClaim;

        await harness.ViewModel.ApplyParametersCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(1, changed);
        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(["Red", "Green"], harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Theory]
    [InlineData(ScanDeviceClockStateKind.Edited)]
    [InlineData(ScanDeviceClockStateKind.ReadRequired)]
    public async Task DeviceTimingState_GivenUnsafeClockState_WhenCheckingRecoveryAndChannelApply_ThenOnlyDeviceClockApplyIsAvailable(ScanDeviceClockStateKind stateKind)
    {
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            CreateKnownRoleProfiles(),
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "126");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(stateKind, ["Red", "Green", "Blue", "IR"]));

        Assert.True(harness.ViewModel.ApplyDeviceClockCommand.CanExecute(null));
        Assert.False(harness.ViewModel.ApplyParametersCommand.CanExecute(null));
        Assert.False(harness.ViewModel.StartScanCommand.CanExecute(null));
        Assert.False(harness.ViewModel.ApplyIlluminationCommand.CanExecute(null));
        Assert.False(harness.ViewModel.EnableMotorCommand.CanExecute("1"));
        Assert.False(harness.ViewModel.MoveMotorCommand.CanExecute("1"));
        Assert.False(harness.ViewModel.ApplyMotorConfigCommand.CanExecute("1"));
        Assert.False(harness.ViewModel.AutoBlackAdjustCommand.CanExecute(null));
        Assert.False(harness.ViewModel.AutoWhiteAdjustCommand.CanExecute(null));
        Assert.False(harness.ViewModel.AutoCalibrateCommand.CanExecute(null));
        Assert.False(harness.ViewModel.AutoFocusCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false, "125")]
    [InlineData(true, "29.999")]
    [InlineData(true, "200.001")]
    [InlineData(true, "NaN")]
    [InlineData(true, "125.0005")]
    public async Task DeviceTimingState_GivenOfflineOrInvalidClock_WhenApplyingDeviceClock_ThenRejectsWithoutWritesOrFalseKnownState(bool connected, string sysClockMhz)
    {
        var session = new StatusSession(isConnected: connected);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: connected,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(connected ? session : null, useConnectedSession: connected),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "125");
        harness.ViewModel.SysClockMhz = sysClockMhz;
        await harness.FlushAsync();
        var before = harness.ViewModel.DeviceTimingState;

        Assert.False(harness.ViewModel.ApplyDeviceClockCommand.CanExecute(null));
        await harness.ViewModel.ApplyDeviceClockCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(0, parameters.GlobalClockApplyCount);
        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(before.StateKind, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(before.RevalidationRequiredChannelRoles, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenDeviceClockApplySucceeds_ThenOneGlobalWriteNoChannelWriteAndAllRolesNeedRevalidation()
    {
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters();
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "126.125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.Edited, ["Red", "Green"]));

        await harness.ViewModel.ApplyDeviceClockCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(1, parameters.GlobalClockApplyCount);
        Assert.Equal(0, parameters.ChannelApplyCount);
        Assert.Equal(126_125u, parameters.AppliedGlobalClocks.Single());
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceTimingState_GivenClockEditWhileClockApplyPending_WhenOriginalApplyCompletes_ThenNewerEditRemainsUnsafe()
    {
        var globalClockApplied = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseClock = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true);
        var parameters = new StatusParameters(globalClockApplied: globalClockApplied, releaseGlobalClockApply: releaseClock);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            parameters: parameters);
        await SetValidParameterInputsAsync(harness, "126");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.Edited, ["Red", "Green"]));

        var applyTask = harness.ViewModel.ApplyDeviceClockCommand.ExecuteAsync(null);
        await globalClockApplied.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.SysClockMhz = "127";
        await harness.FlushAsync();
        releaseClock.TrySetResult(null);
        await applyTask;
        await harness.FlushAsync();

        Assert.Equal([126_000u], parameters.AppliedGlobalClocks);
        Assert.Equal(ScanDeviceClockStateKind.Edited, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Theory]
    [InlineData("Red")]
    [InlineData("Green")]
    [InlineData("Blue")]
    [InlineData("IR")]
    public async Task DeviceTimingState_GivenPendingActiveWorkflowRole_WhenBuildingWorkflowRequest_ThenRejectsThatRole(string pendingRole)
    {
        var harness = await StatusHarness.CreateAsync(CreateKnownRoleProfiles(), null);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, [pendingRole]));

        var requestBuilt = InvokeTryBuildDebugWorkflowRequest(harness.ViewModel, 1, out var error);

        Assert.False(requestBuilt);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(ScanDeviceClockStateKind.Edited, false)]
    [InlineData(ScanDeviceClockStateKind.ReadRequired, false)]
    [InlineData(ScanDeviceClockStateKind.DeviceKnown, true)]
    public async Task DeviceTimingState_GivenUnsafeTiming_WhenTogglingWarmUpOff_ThenRoutesOnlyDisableThroughCoordinatorAndBlocksEnable(
        ScanDeviceClockStateKind stateKind,
        bool hasPendingActiveRole)
    {
        var session = new StatusSession(isConnected: true);
        var coordinator = new StatusSessionCoordinator(session);
        var harness = await StatusHarness.CreateAsync(
            CreateKnownRoleProfiles(),
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));

        harness.ViewModel.IsWarmUpEnabled = true;
        await harness.FlushAsync();
        Assert.Equal([true], coordinator.WarmUpRequests);
        Assert.Equal([true], session.WarmUpEnabledRequests);
        coordinator.WarmUpRequests.Clear();
        session.WarmUpEnabledRequests.Clear();

        InvokeSetDeviceTimingState(
            harness.ViewModel,
            new ScanDeviceTimingState(stateKind, hasPendingActiveRole ? ["Red"] : []));
        harness.ViewModel.IsWarmUpEnabled = false;
        await harness.FlushAsync();

        Assert.Equal([false], coordinator.WarmUpRequests);
        Assert.Equal([false], session.WarmUpEnabledRequests);

        harness.ViewModel.IsWarmUpEnabled = true;
        await harness.FlushAsync();

        Assert.Equal([false], coordinator.WarmUpRequests);
        Assert.Equal([false], session.WarmUpEnabledRequests);
    }

    [Fact]
    public async Task WarmUpToggle_GivenDisconnectedSession_WhenChangingState_ThenDoesNotCallCoordinator()
    {
        var coordinator = new StatusSessionCoordinator();
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, sessionCoordinator: coordinator);

        harness.ViewModel.IsWarmUpEnabled = true;
        await harness.FlushAsync();
        harness.ViewModel.IsWarmUpEnabled = false;
        await harness.FlushAsync();

        Assert.Empty(coordinator.WarmUpRequests);
    }

    [Fact]
    public async Task DeviceTimingState_GivenEditedClock_WhenStartingWorkflowOrMotor_ThenRejectsBeforeFakeSessionSideEffectsAndKeepsRecoveryOperationsAvailable()
    {
        var session = new StatusSession(isConnected: true);
        var workflow = new StatusWorkflow();
        var harness = await StatusHarness.CreateAsync(
            CreateKnownRoleProfiles(),
            null,
            connected: true,
            workflow: workflow,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
        harness.ViewModel.SysClockMhz = "126";
        await harness.FlushAsync();

        var timingState = harness.ViewModel.DeviceTimingState;
        Assert.Equal(ScanDeviceClockStateKind.Edited, timingState.StateKind);
        Assert.Equal(harness.ViewModel.CalibrationChannelOptions, timingState.RevalidationRequiredChannelRoles);
        Assert.False(harness.ViewModel.StartScanCommand.CanExecute(null));
        Assert.False(harness.ViewModel.EnableMotorCommand.CanExecute("1"));
        Assert.False(InvokeTryBuildDebugWorkflowRequest(harness.ViewModel, 1, out _));

        await harness.ViewModel.StartScanCommand.ExecuteAsync(null);
        await harness.ViewModel.EnableMotorCommand.ExecuteAsync("1");

        Assert.Equal(0, workflow.ExecuteCount);
        Assert.Equal(0, session.SetMotorEnabledCallCount);
        Assert.True(harness.ViewModel.ApplyDeviceClockCommand.CanExecute(null));
        Assert.False(harness.ViewModel.ApplyParametersCommand.CanExecute(null));
        Assert.True(harness.ViewModel.RefreshMotionCommand.CanExecute(null));
        Assert.True(harness.ViewModel.DisableMotorCommand.CanExecute("1"));
        Assert.True(harness.ViewModel.StopMotorCommand.CanExecute("1"));
    }

    [Fact]
    public async Task DeviceGlobalClock_GivenLiveClockAndLoadedDraftWithStaleProfileClock_ThenProfileExposureRebindsWithoutReplacingLiveClock()
    {
        var staleProfile = new ScanChannelCalibrationProfile(
            new ScanParameterSnapshot(1_000, -10, 12, 8, 15, 30_000),
            ScanCalibrationRoiSettings.CreateDefault().Normalize(),
            14,
            64_000);
        var harness = await CreateAttachedHarnessAsync(EmptyProfiles, null);
        var expectedMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(staleProfile.Parameters.ExposureTicks, staleProfile.Parameters.SysClockKhz);
        var expectedTicks = ScanTimingMath.NanosecondsToExposureTicks(expectedMicroseconds * 1_000d, 125_000);

        harness.ViewModel.SysClockMhz = "125";
        harness.ViewModel.SysClockKhz = "125000";
        await harness.FlushAsync();
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
        harness.FilmProfileFiles.ImportedProfile = new ScanFilmParameterProfileSet(
            ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
            "Loaded stale-clock profile",
            DateTimeOffset.UnixEpoch.AddDays(1),
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = staleProfile
            },
            "Red",
            ScanFilmAcquisitionSettings.CreateDefault(),
            null);

        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal("125", harness.ViewModel.SysClockMhz);
        Assert.Equal("125000", harness.ViewModel.SysClockKhz);
        Assert.Equal(expectedMicroseconds.ToString("0.###", CultureInfo.InvariantCulture), harness.ViewModel.ExposureMicroseconds);
        Assert.Equal(expectedTicks.ToString(CultureInfo.InvariantCulture), harness.ViewModel.ExposureTicks);
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, harness.ViewModel.DeviceTimingState.StateKind);
    }

    [Fact]
    public async Task DeviceGlobalClock_GivenDeviceKnownTiming_WhenOnlyExposureChanges_ThenTicksUpdateWithoutGlobalClockEditOrAllChannelRevalidation()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        var initialExposure = ScanTimingMath.ExposureTicksToMicroseconds(1_000, 125_000).ToString("0.###", CultureInfo.InvariantCulture);
        var editedExposure = ScanTimingMath.ExposureTicksToMicroseconds(2_000, 125_000).ToString("0.###", CultureInfo.InvariantCulture);

        harness.ViewModel.SysClockMhz = "125";
        harness.ViewModel.SysClockKhz = "125000";
        harness.ViewModel.ExposureMicroseconds = initialExposure;
        await harness.FlushAsync();
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));

        harness.ViewModel.ExposureMicroseconds = editedExposure;
        await harness.FlushAsync();

        Assert.Equal("2000", harness.ViewModel.ExposureTicks);
        Assert.Equal("125000", harness.ViewModel.SysClockKhz);
        Assert.Equal(ScanDeviceClockStateKind.DeviceKnown, harness.ViewModel.DeviceTimingState.StateKind);
        Assert.Empty(harness.ViewModel.DeviceTimingState.RevalidationRequiredChannelRoles);
    }

    [Fact]
    public async Task DeviceGlobalClock_GivenConflictingLegacyChannelClock_WhenSelectionChanges_ThenMicrosecondsProjectAndTicksRebindToTheLiveClock()
    {
        var red = new ScanChannelCalibrationProfile(
            new ScanParameterSnapshot(1_000, -10, 12, 8, 15, 125_000),
            ScanCalibrationRoiSettings.CreateDefault().Normalize(),
            14,
            64_000);
        var green = new ScanChannelCalibrationProfile(
            new ScanParameterSnapshot(1_000, -10, 12, 8, 15, 30_000),
            ScanCalibrationRoiSettings.CreateDefault().Normalize(),
            14,
            64_000);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            }, "Red"));
        var expectedMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(green.Parameters.ExposureTicks, green.Parameters.SysClockKhz);
        var expectedTicks = (ushort)Math.Round(
            ((expectedMicroseconds * 125_000d / 1_000d) - 45_827d) / 6d,
            MidpointRounding.AwayFromZero);

        await harness.SelectChannelAsync("Red");
        harness.ViewModel.SysClockMhz = "125";
        await harness.FlushAsync();
        await harness.SelectChannelAsync("Green");

        Assert.Equal("125", harness.ViewModel.SysClockMhz);
        Assert.Equal(expectedMicroseconds.ToString("0.###", CultureInfo.InvariantCulture), harness.ViewModel.ExposureMicroseconds);
        Assert.Equal(expectedTicks.ToString(CultureInfo.InvariantCulture), harness.ViewModel.ExposureTicks);
    }

    [Fact]
    public async Task Todo17FocusMapping_GivenPersistedMapping_WhenDeviceSettingsInitialize_ThenEditorHydratesOnceFromPersistedValues()
    {
        var persistedMapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: true);
        var deviceSettings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with { FocusMotorMapping = persistedMapping });

        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, deviceSettings: deviceSettings);

        Assert.Equal("Motor2", harness.ViewModel.FocusMappingLeftMotor);
        Assert.Equal("Motor3", harness.ViewModel.FocusMappingRightMotor);
        Assert.Equal("Dir1", harness.ViewModel.FocusMappingZPositiveDirection);
        Assert.Equal("Dir1", harness.ViewModel.FocusMappingTiltPositiveDirection);

        harness.ViewModel.FocusMappingLeftMotor = "Motor1";
        await deviceSettings.InitializeAsync();
        await harness.FlushAsync();

        Assert.Equal("Motor1", harness.ViewModel.FocusMappingLeftMotor);
    }

    [Fact]
    public async Task Todo17AutofocusInputs_WhenEdited_ThenRefreshCanRunActionAndAllAutofocusCommands()
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        var propertyChanged = new List<string?>();
        var commandChanges = new List<string>();
        harness.ViewModel.PropertyChanged += (_, eventArgs) => propertyChanged.Add(eventArgs.PropertyName);
        harness.ViewModel.AutoFocusCommand.CanExecuteChanged += (_, _) => commandChanges.Add(nameof(ScanDebugViewModel.AutoFocusCommand));
        harness.ViewModel.QuickFocusCommand.CanExecuteChanged += (_, _) => commandChanges.Add(nameof(ScanDebugViewModel.QuickFocusCommand));
        harness.ViewModel.FineFocusCommand.CanExecuteChanged += (_, _) => commandChanges.Add(nameof(ScanDebugViewModel.FineFocusCommand));

        void AssertAutofocusCanExecute(bool expected)
        {
            Assert.Equal(expected, harness.ViewModel.CanRunAutoFocusAction);
            Assert.Equal(expected, harness.ViewModel.AutoFocusCommand.CanExecute(null));
            Assert.Equal(expected, harness.ViewModel.QuickFocusCommand.CanExecute(null));
            Assert.Equal(expected, harness.ViewModel.FineFocusCommand.CanExecute(null));
        }

        async Task AssertEditRefreshesAutofocusAvailabilityAsync(Action edit)
        {
            propertyChanged.Clear();
            commandChanges.Clear();

            edit();
            await harness.FlushAsync();

            Assert.Contains(nameof(ScanDebugViewModel.CanRunAutoFocusAction), propertyChanged);
            Assert.Contains(nameof(ScanDebugViewModel.AutoFocusCommand), commandChanges);
            Assert.Contains(nameof(ScanDebugViewModel.QuickFocusCommand), commandChanges);
            Assert.Contains(nameof(ScanDebugViewModel.FineFocusCommand), commandChanges);
        }

        AssertAutofocusCanExecute(true);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusSampleRows = "96");
        AssertAutofocusCanExecute(true);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusTiltProbeSteps = "0.75");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusZProbeSteps = "1.25");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusMotorIntervalUs = "600");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusMaxTiltIterations = "9");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusMaxZIterations = "11");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusSampleRows = "0");
        AssertAutofocusCanExecute(false);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.AutofocusSampleRows = "96");
        AssertAutofocusCanExecute(true);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.SelectedAutofocusPreset = ScanAutofocusPresetKind.Custom);
        Assert.Equal("ScanDebug_Runtime_AutofocusPresetCustom".GetLocalized(), harness.ViewModel.SelectedAutofocusPresetDisplayName);
        harness.ViewModel.SelectedAutofocusPreset = ScanAutofocusPresetKind.Standard;
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.SelectedAutofocusPresetKey = nameof(ScanAutofocusPresetKind.Custom));
        Assert.Equal(ScanAutofocusPresetKind.Custom, harness.ViewModel.SelectedAutofocusPreset);
        harness.ViewModel.SelectedAutofocusPreset = ScanAutofocusPresetKind.Standard;
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.SelectedAutofocusPresetDisplayName = "ScanDebug_Runtime_AutofocusPresetCustom".GetLocalized());
        Assert.Equal(ScanAutofocusPresetKind.Custom, harness.ViewModel.SelectedAutofocusPreset);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingLeftMotor = "Motor2");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingRightMotor = "Motor1");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingRightMotor = "Motor2");
        AssertAutofocusCanExecute(false);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingRightMotor = "Motor3");
        AssertAutofocusCanExecute(true);
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingZPositiveDirection = "Dir1");
        await AssertEditRefreshesAutofocusAvailabilityAsync(() => harness.ViewModel.FocusMappingTiltPositiveDirection = "Dir1");
        AssertAutofocusCanExecute(true);
    }

    [Fact]
    public async Task Todo17Autofocus_GivenUnequalMappedMechanics_WhenRunRequested_ThenRejectsBeforeSessionOrAutofocusIo()
    {
        var autoFocus = new StatusAutoFocus();
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var deviceSettings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            Motor1 = new ScanMotorMechanicalSettings(200, 16, 8.0),
            Motor3 = new ScanMotorMechanicalSettings(200, 8, 8.0),
            FocusMotorMapping = new ScanFocusMotorMapping(0, 2, ZPositiveDirection: true, TiltPositiveDirection: false)
        });
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            autoFocus: autoFocus,
            deviceSettings: deviceSettings,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        Assert.False(InvokeTryBuildAutofocusRequest(harness.ViewModel, out var error));
        Assert.Contains("mechanics", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(harness.ViewModel.AutoFocusCommand.CanExecute(null));
        await harness.ViewModel.AutoFocusCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(0, autoFocus.CallCount);
        Assert.Empty(session.MoveAndWaitRequests);
        Assert.Empty(session.StopMotorRequests);
    }

    [Fact]
    public async Task Todo19_ExactMinimumRoi_ProfileProjectionDoesNotWidenPersistedRanges()
    {
        var exact = CreateRoi(100, 101, 0, 1, 100, 102, 104, 106, 100, 106);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile> { ["Blue"] = CreateProfile(1_000, exact) },
            null);

        await harness.SelectChannelAsync("Blue");

        Assert.Equal(exact, ReadPrivateField<ScanCalibrationRoiSettings>(harness.ViewModel, "_roiSettings"));
    }

    [Fact]
    public async Task Todo19_InvalidPendingFocusRoi_DirectAutoFocusReturnsBeforeInitializationWarmUpOrSessionIo()
    {
        var initializationEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialization = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deviceSettings = new StatusDeviceSettings(initializeEntered: initializationEntered, releaseInitialize: releaseInitialization);
        var autoFocus = new StatusAutoFocus();
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            autoFocus: autoFocus,
            deviceSettings: deviceSettings,
            sessionCoordinator: coordinator);
        await initializationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var invalid = ScanCalibrationRoiSettings.CreateDefault() with { FocusLeftRange = new ScanColumnRange(100, 101) };
        SetPrivateField(harness.ViewModel, "_roiSettings", invalid);

        var directExecution = harness.ViewModel.AutoFocusCommand.ExecuteAsync(null);
        await Task.Delay(250);
        var returnedBeforeInitialization = directExecution.IsCompleted;
        releaseInitialization.TrySetResult(null);
        await directExecution;
        await harness.ViewModel.QuickFocusCommand.ExecuteAsync(null);
        await harness.ViewModel.FineFocusCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.True(returnedBeforeInitialization);
        Assert.Equal(0, autoFocus.CallCount);
        Assert.Empty(coordinator.WarmUpRequests);
        Assert.Equal(0, coordinator.RunConnectedSessionStateCallCount);
        Assert.Empty(session.WarmUpEnabledRequests);
        Assert.Empty(session.SetMotorEnabledRequests);
        Assert.Empty(session.MoveAndWaitRequests);
        Assert.Empty(session.StopMotorRequests);
    }

    [Fact]
    public async Task Todo17Autofocus_GivenEqualMappedMechanics_WhenRunRequested_ThenPreservesMappedIdsAndDirections()
    {
        var autoFocus = new StatusAutoFocus((_, request, _, _, _) => Task.FromResult(new ScanAutofocusResult(request.SampleRows, 0, 0, 1, 1, 1, 0)));
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var deviceSettings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            Motor2 = new ScanMotorMechanicalSettings(200, 32, 8.0),
            Motor3 = new ScanMotorMechanicalSettings(200, 32, 8.0),
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: false)
        });
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            autoFocus: autoFocus,
            deviceSettings: deviceSettings,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        Assert.True(harness.ViewModel.AutoFocusCommand.CanExecute(null));
        await harness.ViewModel.AutoFocusCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var request = Assert.Single(autoFocus.Requests);
        Assert.Equal(new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: false), request.FocusMotorMapping);
        Assert.True(request.TiltProbeSteps > 0);
        Assert.True(request.ZProbeSteps > 0);
    }

    [Fact]
    public async Task Todo17FocusMappingTest_GivenMoveFaultAfterBoundaryEntry_ThenClaimsMoveMotorStopsMappedMotorAndPreservesFailureStatus()
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates())
        {
            MoveAndWaitFailure = new IOException("injected move failure")
        };
        var deviceSettings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: false)
        });
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: deviceSettings,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        await harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        var move = Assert.Single(session.MoveAndWaitRequests);
        Assert.Equal((byte)1, move.MotorId);
        Assert.True(move.Direction);
        Assert.Equal(4u, move.Steps);
        var stop = Assert.Single(session.StopMotorRequests);
        Assert.Equal((byte)1, stop.MotorId);
        Assert.False(stop.TokenCanBeCanceled);
        Assert.Contains("injected move failure", harness.ViewModel.FocusMappingStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.SetMotorEnabledRequests);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenOnlineAutofocusCancellationSynchronouslyClearsState_ThenCapturedMappedMotorsStillStop()
    {
        var autoFocusEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var autoFocus = new StatusAutoFocus(async (_, request, _, _, ct) =>
        {
            autoFocusEntered.TrySetResult(null);
            var completion = new TaskCompletionSource<ScanAutofocusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(() => completion.TrySetCanceled(ct));
            return await completion.Task;
        });
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var deviceSettings = new StatusDeviceSettings(ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, ZPositiveDirection: true, TiltPositiveDirection: false)
        });
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            autoFocus: autoFocus,
            deviceSettings: deviceSettings,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        var autofocusTask = harness.ViewModel.AutoFocusCommand.ExecuteAsync(null);
        await autoFocusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        harness.ViewModel.FocusMappingLeftMotor = "Motor1";
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        await autofocusTask;
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal([(byte)1, (byte)2], session.StopMotorRequests.Select(request => request.MotorId).Distinct().Order().ToArray());
        Assert.DoesNotContain(session.StopMotorRequests, request => request.MotorId == 0);
        Assert.Equal("ScanDebug_Runtime_StatusStopAllFocusLocalCleanup".GetLocalized(), harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenManualFocusWaitingForSettings_ThenQueuesNoMoveWaitOrScan()
    {
        var initializeEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialize = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var motion = new FocusMotionProbe();
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var deviceSettings = new StatusDeviceSettings(
            ScanDeviceSettings.CreateDefault(),
            initializeEntered,
            releaseInitialize);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: deviceSettings,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        await initializeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        releaseInitialize.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(motion.ManualMoveRequests);
        Assert.Empty(motion.WaitRequests);
        Assert.Empty(motion.ScanRequests);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenFirstManualLeftMoveBlocked_ThenQueuesNoRightMoveWaitOrScan()
    {
        var motion = new FocusMotionProbe { BlockFirstManualMove = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await motion.FirstManualMoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        motion.ReleaseFirstManualMove.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(motion.ManualMoveRequests);
        Assert.Empty(motion.WaitRequests);
        Assert.Empty(motion.ScanRequests);
    }

    [Fact]
    public async Task Todo17ManualFocusRelease_GivenFirstJogInProgress_ThenCompletesExactlyOneInitialJog()
    {
        var motion = new FocusMotionProbe { BlockFirstManualMove = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await motion.FirstManualMoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.EndManualFocusHold();
        motion.ReleaseFirstManualMove.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, motion.ManualMoveRequests.Count);
        Assert.Equal(2, motion.WaitRequests.Count);
        Assert.Single(motion.ScanRequests);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenMappingForwardCompletionBlocked_ThenQueuesNoReverse()
    {
        var motion = new FocusMotionProbe { BlockMappingForwardCompletion = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        var mappingTask = harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);
        await motion.MappingForwardCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        motion.ReleaseMappingForwardCompletion.TrySetResult(null);
        await mappingTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(motion.MappingMoveRequests);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenFirstMappedStopFails_ThenAttemptsRemainingMappedStop()
    {
        var motion = new FocusMotionProbe
        {
            BlockMappingForwardCompletion = true,
            StopFailureMotorId = 0
        };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        var mappingTask = harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);
        await motion.MappingForwardCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        motion.ReleaseMappingForwardCompletion.TrySetResult(null);
        await mappingTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(session.StopMotorRequests, request => request.MotorId == 0);
        Assert.Contains(session.StopMotorRequests, request => request.MotorId == 2);
    }

    [Fact]
    public async Task Todo17StopAllFocus_GivenCompletedMappingTest_ThenRepeatedStopIsSafe()
    {
        var motion = new FocusMotionProbe();
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        await harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);
        var stopsAfterMappingTest = session.StopMotorRequests.Count;

        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);
        await harness.ViewModel.StopAllFocusCommand.ExecuteAsync(null);

        Assert.Equal(stopsAfterMappingTest, session.StopMotorRequests.Count);
        Assert.Equal("ScanDebug_Runtime_StatusStopAllFocusLocalCleanup".GetLocalized(), harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task Todo17Disconnect_GivenFirstManualLeftMoveBlocked_ThenQueuesNoFollowOnCommands()
    {
        using var connectionCts = new CancellationTokenSource();
        var motion = new FocusMotionProbe(connectionCts.Token) { BlockFirstManualMove = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await motion.FirstManualMoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        connectionCts.Cancel();
        motion.ReleaseFirstManualMove.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(motion.ManualMoveRequests);
        Assert.Empty(motion.WaitRequests);
        Assert.Empty(motion.ScanRequests);
    }

    [Fact]
    public async Task Todo17ManualFocusSafety_GivenDistanceJustOverMaximum_ThenRejectsBeforeSessionIo()
    {
        await AssertManualFocusRejectedBeforeSessionIoAsync(
            distanceMm: "1.001",
            intervalUs: null,
            ScanDeviceSettings.CreateDefault(),
            "ScanDebug_Runtime_ErrorManualFocusDistanceMaximum".GetLocalizedFormat("1.0"));
    }

    [Fact]
    public async Task Todo17ManualFocusSafety_GivenExactMaximumDistanceWithDefaultMechanics_ThenAcceptsOneJog()
    {
        var motion = new FocusMotionProbe { BlockFirstManualMove = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(),
            sessionCoordinator: coordinator);
        harness.ViewModel.ManualFocusDistanceMm = "1";

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await motion.FirstManualMoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.EndManualFocusHold();
        motion.ReleaseFirstManualMove.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, coordinator.RunConnectedSessionStateCallCount);
        Assert.Equal(2, motion.ManualMoveRequests.Count);
        Assert.All(motion.ManualMoveRequests, request => Assert.Equal(400u, request.Steps));
        Assert.Equal(2, motion.WaitRequests.Count);
        Assert.Single(motion.ScanRequests);
    }

    [Fact]
    public async Task Todo17ManualFocusSafety_GivenStepCountOverMaximum_ThenRejectsBeforeSessionIo()
    {
        var extremeMechanics = new ScanMotorMechanicalSettings(1_000_001, 1, 1.0);
        var settings = ScanDeviceSettings.CreateDefault() with
        {
            Motor1 = extremeMechanics,
            Motor3 = extremeMechanics
        };

        await AssertManualFocusRejectedBeforeSessionIoAsync(
            distanceMm: "1",
            intervalUs: null,
            settings,
            "ScanDebug_Runtime_ErrorManualFocusStepRange".GetLocalizedFormat(1_000_000));
    }

    [Fact]
    public async Task Todo17ManualFocusSafety_GivenEstimatedDurationOverMaximum_ThenRejectsBeforeSessionIo()
    {
        await AssertManualFocusRejectedBeforeSessionIoAsync(
            distanceMm: "1",
            intervalUs: "75001",
            ScanDeviceSettings.CreateDefault(),
            "ScanDebug_Runtime_ErrorManualFocusDurationMaximum".GetLocalizedFormat(30_000));
    }

    [Fact]
    public async Task Todo17ManualFocusSafety_GivenExactStepAndDurationMaximums_ThenAcceptsOneJog()
    {
        var boundaryMechanics = new ScanMotorMechanicalSettings(1_000_000, 1, 1.0);
        var settings = ScanDeviceSettings.CreateDefault() with
        {
            Motor1 = boundaryMechanics,
            Motor3 = boundaryMechanics
        };
        var motion = new FocusMotionProbe { BlockFirstManualMove = true };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(settings),
            sessionCoordinator: coordinator);
        harness.ViewModel.ManualFocusDistanceMm = "1";
        harness.ViewModel.AutofocusMotorIntervalUs = "30";

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await motion.FirstManualMoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.ViewModel.EndManualFocusHold();
        motion.ReleaseFirstManualMove.TrySetResult(null);
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, coordinator.RunConnectedSessionStateCallCount);
        Assert.Equal(2, motion.ManualMoveRequests.Count);
        Assert.All(motion.ManualMoveRequests, request => Assert.Equal(1_000_000u, request.Steps));
        Assert.All(motion.ManualMoveRequests, request => Assert.Equal(30_000u, request.IntervalNs));
        Assert.Equal(2, motion.WaitRequests.Count);
        Assert.Single(motion.ScanRequests);
    }

    private static async Task AssertManualFocusRejectedBeforeSessionIoAsync(
        string distanceMm,
        string? intervalUs,
        ScanDeviceSettings settings,
        string expectedError)
    {
        var motion = new FocusMotionProbe();
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            deviceSettings: new StatusDeviceSettings(settings),
            sessionCoordinator: coordinator);
        harness.ViewModel.ManualFocusDistanceMm = distanceMm;
        if (intervalUs is not null)
            harness.ViewModel.AutofocusMotorIntervalUs = intervalUs;

        harness.ViewModel.BeginManualFocusHold(positive: true);
        var manualFocusTask = ReadPrivateField<Task>(harness.ViewModel, "_manualFocusTask")!;
        await manualFocusTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, coordinator.RunConnectedSessionStateCallCount);
        Assert.Empty(motion.ManualMoveRequests);
        Assert.Empty(motion.WaitRequests);
        Assert.Empty(motion.ScanRequests);
        Assert.Equal(expectedError, harness.ViewModel.StatusText);
    }

    [Fact]
    public async Task Todo19_InvalidPendingAdcEdit_DisablesOnlyAdcCommandsAndDoesNotMutateDraftOrSession()
    {
        var session = new StatusSession(isConnected: true);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;
        var canExecuteNotifications = 0;
        harness.ViewModel.AutoBlackAdjustCommand.CanExecuteChanged += (_, _) => canExecuteNotifications++;

        Assert.True(harness.ViewModel.AutoBlackAdjustCommand.CanExecute(null));
        harness.ViewModel.RoiStartInput = "not-a-number";

        Assert.False(harness.ViewModel.AutoBlackAdjustCommand.CanExecute(null));
        Assert.True(harness.ViewModel.AutoFocusCommand.CanExecute(null));
        harness.ViewModel.ApplySelectedRoiInputsCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Equal("not-a-number", harness.ViewModel.RoiStartInput);
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Empty(session.SetMotorEnabledRequests);
        Assert.True(canExecuteNotifications > 0);
    }

    [Fact]
    public async Task Todo19_RoiIssueNavigation_MapsOnlyExactOwnerAndFieldPath()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        ScanRoiIssueNavigationRequest? request = null;
        var requestCount = 0;
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        harness.ViewModel.RoiIssueNavigationRequested += navigation =>
        {
            request = navigation;
            requestCount++;
        };

        Assert.True(harness.ViewModel.TryNavigateToRoiIssue(new ScanRoiValidationIssue(
            ScanRoiOperationOwner.AutoFocus,
            ScanRoiValidationCode.Overlap,
            "FocusRightRange")));

        Assert.Equal("Focus Right", harness.ViewModel.SelectedRoiSelection);
        Assert.Equal(new ScanRoiIssueNavigationRequest(ScanRoiOperationOwner.AutoFocus, ScanRoiEditorTarget.FocusRight, "FocusRightRange"), request);
        Assert.False(harness.ViewModel.TryNavigateToRoiIssue(new ScanRoiValidationIssue(
            ScanRoiOperationOwner.AutoFocus,
            ScanRoiValidationCode.Overlap,
            "UnknownRange")));
        Assert.Equal("Focus Right", harness.ViewModel.SelectedRoiSelection);
        Assert.Equal(1, requestCount);
        Assert.Same(currentDraft, harness.Workspace.Snapshot.CurrentDraft);
    }

    [Fact]
    public async Task Todo23_MalformedImportPreservesCurrentDraftAndValidationAndDisablesApply()
    {
        var currentProfile = CreateProfile(1_000);
        var harness = await CreateAttachedHarnessAsync(
            EmptyProfiles,
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = currentProfile
            }, "Red"));
        harness.ViewModel.FilmProfileName = string.Empty;
        await harness.FlushAsync();
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        var currentIssues = harness.ViewModel.CurrentFilmProfileValidationIssues.ToArray();
        harness.FilmProfileFiles.ImportResult = new ScanFilmProfileFileImportResult(
            false,
            null,
            new ScanFilmProfileValidationResult(
            [
                new ScanFilmProfileValidationIssue(
                    ScanFilmProfileValidationCode.MalformedJson,
                    "document",
                    ScanFilmProfileValidationSeverity.Error,
                    "FilmProfile.Validation.MalformedJson")
            ]));

        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Same(currentDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Equal(currentIssues, harness.ViewModel.CurrentFilmProfileValidationIssues);
        Assert.Contains(harness.ViewModel.CurrentFilmProfileValidationIssues, issue =>
            issue.Code == ScanFilmProfileValidationCode.InvalidProfileName
            && issue.FieldPath == "profileName");
        var stagedIssue = Assert.Single(harness.ViewModel.StagedFilmProfileImportValidationIssues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, stagedIssue.Code);
        Assert.Equal("document", stagedIssue.FieldPath);
        Assert.NotSame(harness.ViewModel.CurrentFilmProfileValidationIssues, harness.ViewModel.StagedFilmProfileImportValidationIssues);
        Assert.False(harness.ViewModel.IsStagedFilmProfileImportValid);
        Assert.False(harness.ViewModel.CanApplyStagedFilmProfileImport);
        Assert.False(harness.ViewModel.ApplyStagedFilmProfileImportCommand.CanExecute(null));
        Assert.True(harness.ViewModel.DiscardStagedFilmProfileImportCommand.CanExecute(null));
        Assert.Equal(string.Empty, harness.ViewModel.FilmProfileName);
    }

    [Fact]
    public async Task Todo23_ValidStagedReviewDiscardPreservesCurrentDraftUntilApply()
    {
        var currentProfile = CreateProfile(1_000);
        var importedProfile = CreateProfile(2_000);
        var harness = await CreateAttachedHarnessAsync(
            EmptyProfiles,
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = currentProfile
            }, "Red"));
        harness.ViewModel.FilmProfileName = string.Empty;
        await harness.FlushAsync();
        var currentDraft = harness.Workspace.Snapshot.CurrentDraft;
        var currentIssueSnapshot = harness.ViewModel.CurrentFilmProfileValidationIssues
            .Select(issue => new
            {
                issue.Code,
                issue.FieldPath,
                issue.Severity,
                issue.MessageKey,
                MessageArguments = issue.MessageArguments.ToArray()
            })
            .ToArray();
        Assert.NotEmpty(currentIssueSnapshot);

        void AssertCurrentValidationIsPreserved()
        {
            var actualIssues = harness.ViewModel.CurrentFilmProfileValidationIssues;
            Assert.Equal(currentIssueSnapshot.Length, actualIssues.Count);
            for (var index = 0; index < currentIssueSnapshot.Length; index++)
            {
                var expected = currentIssueSnapshot[index];
                var actual = actualIssues[index];
                Assert.Equal(expected.Code, actual.Code);
                Assert.Equal(expected.FieldPath, actual.FieldPath);
                Assert.Equal(expected.Severity, actual.Severity);
                Assert.Equal(expected.MessageKey, actual.MessageKey);
                Assert.Equal(expected.MessageArguments, actual.MessageArguments);
            }
        }

        harness.FilmProfileFiles.ImportedProfile = new ScanFilmParameterProfileSet(
            ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
            "Imported profile",
            DateTimeOffset.UnixEpoch.AddDays(1),
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = importedProfile
            },
            "Red",
            ScanFilmAcquisitionSettings.CreateDefault(),
            null);

        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Same(currentDraft, harness.Workspace.Snapshot.CurrentDraft);
        AssertCurrentValidationIsPreserved();
        Assert.True(harness.ViewModel.HasPendingFilmProfileImportResult);
        Assert.Empty(harness.ViewModel.StagedFilmProfileImportValidationIssues);
        Assert.True(harness.ViewModel.IsStagedFilmProfileImportValid);
        Assert.True(harness.ViewModel.CanApplyStagedFilmProfileImport);
        Assert.True(harness.ViewModel.ApplyStagedFilmProfileImportCommand.CanExecute(null));

        harness.ViewModel.DiscardStagedFilmProfileImportCommand.Execute(null);
        await harness.FlushAsync();

        Assert.Same(currentDraft, harness.Workspace.Snapshot.CurrentDraft);
        AssertCurrentValidationIsPreserved();
        Assert.False(harness.ViewModel.HasPendingFilmProfileImportResult);
        Assert.False(harness.ViewModel.ApplyStagedFilmProfileImportCommand.CanExecute(null));

        await harness.ViewModel.LoadFilmProfileJsonCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.Same(currentDraft, harness.Workspace.Snapshot.CurrentDraft);
        AssertCurrentValidationIsPreserved();
        await harness.ViewModel.ApplyStagedFilmProfileImportCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal("Imported profile", harness.Workspace.Snapshot.CurrentDraft.ProfileName);
        Assert.Equal(importedProfile, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Red"]);
        Assert.False(harness.ViewModel.HasPendingFilmProfileImportResult);
        Assert.Empty(harness.ViewModel.StagedFilmProfileImportValidationIssues);
    }

    [Fact]
    public async Task Todo23_CurrentValidationIssueDisplays_ProjectGlobalAndSectionsWithoutStagedOwnership()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        var notifications = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        var profileNameIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid");
        var acquisitionIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "acquisitionSettings.rows", "FilmProfile.Validation.AcquisitionInputInvalid");
        var channelIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Red.parameters", "FilmProfile.Validation.ChannelParametersInvalid");
        var unknownIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.MissingRequiredProperty, "future.path", "FilmProfile.Validation.GenericInputInvalid", ScanFilmProfileValidationSeverity.Warning);
        var stagedIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson");

        harness.ViewModel.CurrentFilmProfileValidationIssues = [unknownIssue, channelIssue, acquisitionIssue, profileNameIssue];
        harness.ViewModel.StagedFilmProfileImportValidationIssues = [stagedIssue];
        await harness.FlushAsync();

        Assert.Equal(4, harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.Count);
        Assert.Single(harness.ViewModel.BasicCurrentFilmProfileValidationIssueDisplays);
        Assert.Single(harness.ViewModel.AcquisitionCurrentFilmProfileValidationIssueDisplays);
        Assert.Single(harness.ViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays);
        Assert.Contains(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays, display => ReferenceEquals(display.Issue, unknownIssue) && !display.CanNavigate);
        Assert.All(harness.ViewModel.StagedFilmProfileImportValidationIssueDisplays, display => Assert.False(display.CanNavigate));
        Assert.Equal(harness.ViewModel.CurrentFilmProfileValidationHeadline, harness.ViewModel.CurrentFilmProfileValidationIssueCountText);
        Assert.Equal(harness.ViewModel.BasicCurrentFilmProfileValidationHeadline, harness.ViewModel.BasicCurrentFilmProfileValidationIssueCountText);
        Assert.Equal(harness.ViewModel.AcquisitionCurrentFilmProfileValidationHeadline, harness.ViewModel.AcquisitionCurrentFilmProfileValidationIssueCountText);
        Assert.Equal(harness.ViewModel.ChannelCalibrationCurrentFilmProfileValidationHeadline, harness.ViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueCountText);
        Assert.Equal(harness.ViewModel.StagedFilmProfileImportValidationHeadline, harness.ViewModel.StagedFilmProfileImportValidationIssueCountText);
        Assert.DoesNotContain(Environment.NewLine, harness.ViewModel.CurrentFilmProfileValidationHeadline, StringComparison.Ordinal);
        Assert.Contains(nameof(ScanDebugViewModel.CurrentFilmProfileValidationIssueDisplays), notifications);
        Assert.Contains(nameof(ScanDebugViewModel.BasicCurrentFilmProfileValidationIssueDisplays), notifications);
        Assert.Contains(nameof(ScanDebugViewModel.AcquisitionCurrentFilmProfileValidationIssueDisplays), notifications);
        Assert.Contains(nameof(ScanDebugViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays), notifications);
        Assert.Contains(nameof(ScanDebugViewModel.StagedFilmProfileImportValidationIssueDisplays), notifications);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_SameRoleStructurallyEquivalentRowEmitsWithoutNewSelectionWrite()
    {
        var red = CreateProfile(1_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red }, "Red"));
        var issue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Red.parameters", "FilmProfile.Validation.ChannelParametersInvalid", arguments: ["Red"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var display = harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.Single();
        var equivalentDisplay = new ScanFilmProfileValidationIssueDisplay
        {
            Source = ScanFilmProfileIssueSource.CurrentDraft,
            Issue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Red.parameters", "FilmProfile.Validation.ChannelParametersInvalid", arguments: ["Red"]),
            FieldPath = "channelProfiles.Red.parameters",
            Message = display.Message,
            AutomationId = display.AutomationId,
            AutomationName = display.AutomationName,
            CanNavigate = true
        };
        var writeCount = harness.Repository.SelectedChannelWriteRoles.Count;
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var result = await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(equivalentDisplay);

        Assert.True(result);
        var request = Assert.Single(requests);
        Assert.Equal(ScanFilmProfileIssueNavigationSection.ChannelCalibration, request.Section);
        Assert.Equal(ScanFilmProfileIssueEditorTarget.ChannelParameters, request.EditorTarget);
        Assert.Equal("Red", request.ChannelRole);
        Assert.Equal(writeCount, harness.Repository.SelectedChannelWriteRoles.Count);
        Assert.Empty(harness.Repository.SavedProfileRoles);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_ChangedRoleProjectionReplacesSyntheticIssueWithoutEmitting()
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(2_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green }, "Red"));
        harness.Repository.BlockSelectedChannelWrite("Green");
        var issue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Green.parameters", "FilmProfile.Validation.ChannelParametersInvalid", arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var display = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays);
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        Assert.Empty(requests);
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        Assert.False(await navigation);

        Assert.Empty(requests);
        Assert.Equal("Green", harness.ViewModel.SelectedCalibrationChannel);
        Assert.Equal(green.Parameters.ExposureTicks.ToString(CultureInfo.InvariantCulture), harness.ViewModel.ExposureTicks);
        Assert.Equal(["Green"], harness.Repository.SelectedChannelWriteRoles);
        Assert.Empty(harness.Repository.SavedProfileRoles);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Todo23_CurrentIssueNavigation_FailedOrCanceledSelectionDoesNotEmitOrWriteProfile(bool canceled)
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(0);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green }, "Red"));
        harness.Repository.BlockSelectedChannelWrite("Green", canceled ? new OperationCanceledException() : new InvalidOperationException("selection write failed"));
        var issue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Green.parameters", "FilmProfile.Validation.ChannelParametersInvalid", arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.Single());
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        Assert.False(await navigation);

        Assert.Empty(requests);
        Assert.Empty(harness.Repository.SavedProfileRoles);
        Assert.Empty(harness.Repository.ClearedProfileRoles);
        Assert.Contains("ScanDebug_Runtime_CalibrationChannel_LoadFailed", harness.ViewModel.CalibrationChannelStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_StaleAwaitSuppressesRequestWhenSelectionChanges()
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(0);
        var blue = CreateProfile(3_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green, ["Blue"] = blue },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green, ["Blue"] = blue }, "Red"));
        harness.Repository.BlockSelectedChannelWrite("Green");
        var issue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "channelProfiles.Green.parameters", "FilmProfile.Validation.ChannelParametersInvalid", arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.Single());
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        harness.ViewModel.SelectedCalibrationChannel = "Blue";
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        Assert.False(await navigation);

        Assert.Empty(requests);
        Assert.Equal("Blue", harness.ViewModel.SelectedCalibrationChannel);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_RemovedCurrentIssueDuringBlockedGreenLoadDoesNotEmit()
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(2_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green }, "Red"));
        harness.Repository.BlockSelectedChannelWrite("Green");
        var issue = CreateFilmProfileIssue(
            ScanFilmProfileValidationCode.InvalidChannelParameters,
            "channelProfiles.Green.parameters",
            "FilmProfile.Validation.ChannelParametersInvalid",
            arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var display = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays);
        Assert.Equal(ScanFilmProfileValidationCode.InvalidChannelParameters, display.Issue.Code);
        Assert.Equal("channelProfiles.Green.parameters", display.Issue.FieldPath);
        Assert.Equal(ScanFilmProfileValidationSeverity.Error, display.Issue.Severity);
        Assert.Equal("FilmProfile.Validation.ChannelParametersInvalid", display.Issue.MessageKey);
        Assert.Equal(["Green"], display.Issue.MessageArguments);
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        void AssertSelectionProjectionValidation()
        {
            Assert.Collection(harness.ViewModel.CurrentFilmProfileValidationIssues,
                parametersIssue =>
                {
                    Assert.Equal(ScanFilmProfileValidationCode.InvalidChannelParameters, parametersIssue.Code);
                    Assert.Equal("ChannelProfiles.Selected.Parameters", parametersIssue.FieldPath);
                    Assert.Equal(ScanFilmProfileValidationSeverity.Error, parametersIssue.Severity);
                    Assert.Equal("FilmProfile.Validation.ChannelParametersInvalid", parametersIssue.MessageKey);
                    Assert.Empty(parametersIssue.MessageArguments);
                },
                profileNameIssue =>
                {
                    Assert.Equal(ScanFilmProfileValidationCode.InvalidProfileName, profileNameIssue.Code);
                    Assert.Equal("profileName", profileNameIssue.FieldPath);
                    Assert.Equal(ScanFilmProfileValidationSeverity.Warning, profileNameIssue.Severity);
                    Assert.Equal("FilmProfile.Validation.ProfileNameInvalid", profileNameIssue.MessageKey);
                    Assert.Empty(profileNameIssue.MessageArguments);
                });
        }

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");

        AssertSelectionProjectionValidation();
        Assert.Empty(requests);
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        var result = await navigation;

        AssertSelectionProjectionValidation();
        Assert.True(!result && requests.Count == 0,
            $"A removed current issue must not request focus after await; result={result}, focusRequests={requests.Count}.");
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_PendingImportDuringBlockedGreenLoadDoesNotEmit()
    {
        var red = CreateProfile(1_000);
        var green = CreateProfile(2_000);
        var initialDraft = CreateDraft(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green },
            "Red");
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = green },
            initialDraft);
        harness.Repository.BlockSelectedChannelWrite("Green");
        var issue = CreateFilmProfileIssue(
            ScanFilmProfileValidationCode.InvalidChannelParameters,
            "channelProfiles.Green.parameters",
            "FilmProfile.Validation.ChannelParametersInvalid",
            arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var display = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays);
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        var currentDraftBeforeImport = harness.Workspace.Snapshot.CurrentDraft;
        var importError = new ScanFilmProfileValidationIssue(
            ScanFilmProfileValidationCode.MalformedJson,
            "document",
            ScanFilmProfileValidationSeverity.Error,
            "FilmProfile.Validation.MalformedJson");
        harness.Workspace.SetImportError(new ScanFilmProfileValidationResult([importError]));

        Assert.True(harness.ViewModel.HasPendingFilmProfileImportResult);
        Assert.Same(currentDraftBeforeImport, harness.Workspace.Snapshot.CurrentDraft);
        var stagedIssue = Assert.Single(harness.ViewModel.StagedFilmProfileImportValidationIssues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, stagedIssue.Code);
        Assert.Equal("document", stagedIssue.FieldPath);
        Assert.Empty(requests);
        harness.Repository.ReleaseSelectedChannelWrite("Green");

        Assert.False(await navigation);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_WorkspaceReplacementDuringBlockedGreenLoadDoesNotEmit()
    {
        var red = CreateProfile(1_000);
        var originalGreen = CreateProfile(2_000);
        var replacementGreen = CreateProfile(3_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = originalGreen },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = originalGreen }, "Red"));
        harness.Repository.BlockSelectedChannelWrite("Green");
        var issue = CreateFilmProfileIssue(
            ScanFilmProfileValidationCode.InvalidChannelParameters,
            "channelProfiles.Green.parameters",
            "FilmProfile.Validation.ChannelParametersInvalid",
            arguments: ["Green"]);
        harness.ViewModel.CurrentFilmProfileValidationIssues = [issue];
        var display = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays);
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        var navigation = harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display);
        await harness.Repository.WaitForSelectedChannelWriteAsync("Green");
        var replacementDraft = CreateDraft(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red, ["Green"] = replacementGreen },
            "Green");
        harness.Workspace.SetCurrentDraft(replacementDraft);

        Assert.Same(replacementDraft, harness.Workspace.Snapshot.CurrentDraft);
        Assert.Equal(replacementGreen, harness.Workspace.Snapshot.CurrentDraft.ChannelProfiles["Green"]);
        Assert.Empty(harness.ViewModel.CurrentFilmProfileValidationIssues);
        Assert.Empty(requests);
        harness.Repository.ReleaseSelectedChannelWrite("Green");
        var result = await navigation;

        Assert.True(!result && requests.Count == 0,
            $"A replaced workspace draft must not request focus after await; result={result}, focusRequests={requests.Count}.");
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_RealInvalidGreenUsesSelectedParametersPath()
    {
        var green = CreateProfile(2_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Green"] = green },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Green"] = green }, "Green"));
        await harness.SelectChannelAsync("Green");
        await ClearCurrentEditorAsync(harness);
        var issue = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssues, candidate =>
            candidate.Code == ScanFilmProfileValidationCode.InvalidChannelParameters);
        Assert.Equal("ChannelProfiles.Selected.Parameters", issue.FieldPath);
        Assert.Equal(ScanFilmProfileValidationSeverity.Error, issue.Severity);
        Assert.Equal("FilmProfile.Validation.ChannelParametersInvalid", issue.MessageKey);
        Assert.Empty(issue.MessageArguments);
        var display = Assert.Single(harness.ViewModel.CurrentFilmProfileValidationIssueDisplays, candidate => ReferenceEquals(candidate.Issue, issue));
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;
        var writeCount = harness.Repository.SelectedChannelWriteRoles.Count;

        Assert.True(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(display));
        var request = Assert.Single(requests);
        Assert.Equal(ScanFilmProfileIssueNavigationSection.ChannelCalibration, request.Section);
        Assert.Equal(ScanFilmProfileIssueEditorTarget.ChannelParameters, request.EditorTarget);
        Assert.Equal("ChannelProfiles.Selected.Parameters", request.FieldPath);
        Assert.Null(request.ChannelRole);
        Assert.Equal(writeCount, harness.Repository.SelectedChannelWriteRoles.Count);
    }

    [Fact]
    public async Task Todo23_CurrentIssueNavigation_RejectsImportPendingStaleUnknownAndUnsafeRows()
    {
        var red = CreateProfile(1_000);
        var harness = await StatusHarness.CreateAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red }, "Red"));
        var currentIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid");
        var importIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid");
        var unknownIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.MissingRequiredProperty, "unknown.path", "FilmProfile.Validation.GenericInputInvalid");
        var unsafeRoleIssue = CreateFilmProfileIssue(ScanFilmProfileValidationCode.InvalidChannelRole, "channelProfiles.Red.parameters", "FilmProfile.Validation.ChannelRoleInvalid");
        harness.ViewModel.CurrentFilmProfileValidationIssues = [currentIssue, unknownIssue, unsafeRoleIssue];
        harness.ViewModel.StagedFilmProfileImportValidationIssues = [importIssue];
        var staleDisplay = harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.First(display => ReferenceEquals(display.Issue, currentIssue));
        var importDisplay = harness.ViewModel.StagedFilmProfileImportValidationIssueDisplays.Single();
        var unknownDisplay = harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.First(display => ReferenceEquals(display.Issue, unknownIssue));
        var unsafeRoleDisplay = harness.ViewModel.CurrentFilmProfileValidationIssueDisplays.First(display => ReferenceEquals(display.Issue, unsafeRoleIssue));
        var requests = new List<ScanFilmProfileIssueNavigationRequest>();
        harness.ViewModel.CurrentFilmProfileIssueNavigationRequested += requests.Add;

        harness.ViewModel.CurrentFilmProfileValidationIssues = [];

        Assert.False(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(staleDisplay));
        Assert.False(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(importDisplay));
        harness.ViewModel.CurrentFilmProfileValidationIssues = [unknownIssue, unsafeRoleIssue];
        Assert.False(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(unknownDisplay));
        Assert.False(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(unsafeRoleDisplay));
        Assert.False(await harness.ViewModel.TryRequestCurrentFilmProfileIssueNavigationAsync(null));
        Assert.Empty(requests);
        Assert.Empty(harness.Repository.SelectedChannelWriteRoles);
    }

    [Fact]
    public async Task Todo19_CurrentInvalidAdcRoiInput_ExposesClickableOwnerIssueWithoutChangingOtherOwners()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);

        harness.ViewModel.RoiStartInput = "not-a-number";

        var issue = Assert.Single(harness.ViewModel.AdcRoiValidationIssues);
        Assert.Equal(ScanRoiOperationOwner.AdcCalibration, issue.Owner);
        Assert.Equal(ScanRoiValidationCode.Missing, issue.Code);
        Assert.Equal("EffectiveRange", issue.FieldPath);
        Assert.Empty(harness.ViewModel.FocusRoiValidationIssues);
        Assert.Equal("not-a-number", harness.ViewModel.RoiStartInput);

        Assert.True(harness.ViewModel.TryNavigateToRoiIssue(issue));
        Assert.Equal("BW Active", harness.ViewModel.SelectedRoiSelection);
        Assert.Equal("not-a-number", harness.ViewModel.RoiStartInput);
    }

    [Fact]
    public async Task Todo19_CurrentInvalidFocusRoiInput_ExposesClickableOwnerIssueWithoutChangingAdcOwner()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        harness.ViewModel.SelectedRoiSelection = "Focus Left";

        harness.ViewModel.RoiStartInput = "-1";
        harness.ViewModel.RoiEndInput = "1";

        var issue = Assert.Single(harness.ViewModel.FocusRoiValidationIssues);
        Assert.Equal(ScanRoiOperationOwner.AutoFocus, issue.Owner);
        Assert.Equal(ScanRoiValidationCode.OutOfBounds, issue.Code);
        Assert.Equal("FocusLeftRange", issue.FieldPath);
        Assert.Empty(harness.ViewModel.AdcRoiValidationIssues);
        Assert.Equal("-1", harness.ViewModel.RoiStartInput);

        Assert.True(harness.ViewModel.TryNavigateToRoiIssue(issue));
        Assert.Equal("Focus Left", harness.ViewModel.SelectedRoiSelection);
        Assert.Equal("-1", harness.ViewModel.RoiStartInput);
    }

    [Fact]
    public async Task Todo19_CurrentInvalidReferenceInput_ExposesImageReferenceNavigationIssue()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        ScanRoiIssueNavigationRequest? request = null;
        harness.ViewModel.RoiIssueNavigationRequested += navigation => request = navigation;

        harness.ViewModel.ColumnSampleStartInput = "bad-ref";

        var issue = Assert.Single(harness.ViewModel.ImageReferenceRoiValidationIssues);
        Assert.Equal(ScanRoiOperationOwner.ImageReferenceSampling, issue.Owner);
        Assert.Equal(ScanRoiValidationCode.Missing, issue.Code);
        Assert.Equal("ColumnRange", issue.FieldPath);
        Assert.Equal("bad-ref", harness.ViewModel.ColumnSampleStartInput);

        Assert.True(harness.ViewModel.TryNavigateToRoiIssue(issue));
        Assert.Equal(new ScanRoiIssueNavigationRequest(ScanRoiOperationOwner.ImageReferenceSampling, ScanRoiEditorTarget.ImageReferenceColumn, "ColumnRange"), request);
        Assert.Equal("bad-ref", harness.ViewModel.ColumnSampleStartInput);
    }

    [Fact]
    public async Task Todo19_InvalidReferenceRange_DoesNotMutateDraftOrEnableReferenceSave()
    {
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null);
        var originalDraft = harness.Workspace.Snapshot.CurrentDraft;

        harness.ViewModel.UpdateColumnSampleRange(-1, 0, 100);

        Assert.False(harness.ViewModel.TryGetColumnSampleRange(100, out _));
        Assert.False(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));
        Assert.False(harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.CanExecute(null));
        Assert.Same(originalDraft, harness.Workspace.Snapshot.CurrentDraft);
    }

    [Fact]
    public async Task Todo19_ReferenceSample_ChannelChangeRequiresNewCurrentChannelFrameBeforeSave()
    {
        const ushort sample = 123;
        var red = CreateProfile(1_000, blackLevel: 10, whiteLevel: 60_000);
        var green = CreateProfile(2_000, blackLevel: 20, whiteLevel: 61_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = red,
                ["Green"] = green
            }, "Red"),
            previewSample: sample,
            connected: true);

        InvokeApplyScanFrame(harness.ViewModel);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));

        await harness.SelectChannelAsync("Green");

        Assert.False(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));
        Assert.False(harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.CanExecute(null));
        await harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        await harness.FlushAsync();
        Assert.Empty(harness.Repository.SavedProfileRoles);

        InvokeApplyScanFrame(harness.ViewModel);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));
        await harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal(["Green"], harness.Repository.SavedProfileRoles);
        Assert.Equal(sample, harness.Repository.Snapshot.Profiles["Green"].BlackLevel);
        Assert.Equal(red, harness.Repository.Snapshot.Profiles["Red"]);
    }

    [Fact]
    public async Task Todo19_ReferenceSample_CurrentDraftReplacementInvalidatesSameChannelCache()
    {
        const ushort sample = 123;
        var red = CreateProfile(1_000, blackLevel: 10, whiteLevel: 60_000);
        var harness = await CreateAttachedHarnessAsync(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red },
            CreateDraft(new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red }, "Red"),
            previewSample: sample,
            connected: true);

        InvokeApplyScanFrame(harness.ViewModel);
        await harness.FlushAsync();
        Assert.True(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));

        harness.Workspace.SetCurrentDraft(CreateDraft(
            new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase) { ["Red"] = red },
            "Red"));
        await harness.FlushAsync();

        Assert.False(harness.ViewModel.SaveColumnSampleAsBlackLevelCommand.CanExecute(null));
        Assert.False(harness.ViewModel.SaveColumnSampleAsWhiteLevelCommand.CanExecute(null));
        Assert.Empty(harness.Repository.SavedProfileRoles);
    }

    private static ScanChannelCalibrationProfile CreateProfile(
        ushort exposureTicks,
        ScanCalibrationRoiSettings? roiSettings = null,
        ushort? blackLevel = 14,
        ushort? whiteLevel = 64_000)
        => new(
            new ScanParameterSnapshot(exposureTicks, -10, 12, 8, 15, 48_000),
            roiSettings ?? ScanCalibrationRoiSettings.CreateDefault().Normalize(),
            blackLevel,
            whiteLevel);

    private static ScanFilmProfileValidationIssue CreateFilmProfileIssue(
        ScanFilmProfileValidationCode code,
        string fieldPath,
        string messageKey,
        ScanFilmProfileValidationSeverity severity = ScanFilmProfileValidationSeverity.Error,
        IEnumerable<string>? arguments = null)
        => new(code, fieldPath, severity, messageKey, arguments);

    private static ScanCalibrationRoiSettings CreateRoi(
        int effectiveStart,
        int effectiveEnd,
        int shieldStart,
        int shieldEnd,
        int focusLeftStart,
        int focusLeftEnd,
        int focusRightStart,
        int focusRightEnd,
        int focusOverallStart,
        int focusOverallEnd)
        => new ScanCalibrationRoiSettings(
            new ScanColumnRange(effectiveStart, effectiveEnd),
            new ScanColumnRange(shieldStart, shieldEnd),
            new ScanColumnRange(focusLeftStart, focusLeftEnd),
            new ScanColumnRange(focusRightStart, focusRightEnd),
            new ScanColumnRange(focusOverallStart, focusOverallEnd)).Normalize();

    private static IReadOnlyList<ScanMotorState> CreateIdleMotionStates()
        =>
        [
            new ScanMotorState(0, true, false, false, 0, ScanDebugConstants.MotionDefaultIntervalNs, 0),
            new ScanMotorState(1, true, false, false, 0, ScanDebugConstants.MotionDefaultIntervalNs, 0),
            new ScanMotorState(2, true, false, false, 0, ScanDebugConstants.MotionDefaultIntervalNs, 0)
        ];

    [Fact]
    public async Task Todo18MotionCommands_FaultGateRefreshAndGlobalStopUseActualViewModelCommands()
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates())
        {
            MoveAndWaitFailure = new IOException("injected move fault")
        };
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true)
        {
            GlobalStopResult = new ScanOperationResult(true, "Motor stop commands completed for IDs 0, 1, and 2. Hardware motion state was not verified.")
        };
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));

        await harness.ViewModel.MoveMotorCommand.ExecuteAsync("1");

        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.False(harness.ViewModel.MoveMotorCommand.CanExecute("1"));
        Assert.False(harness.ViewModel.EnableMotorCommand.CanExecute("1"));
        Assert.True(harness.ViewModel.DisableMotorCommand.CanExecute("1"));
        Assert.True(harness.ViewModel.RefreshMotionCommand.CanExecute(null));

        await harness.ViewModel.RefreshMotionCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsMotionStateReadRequired);
        Assert.Equal(string.Empty, harness.ViewModel.MotionStateReadRequiredText);
        var notifications = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        harness.ViewModel.FocusMappingLeftMotor = "Motor2";
        harness.ViewModel.FocusMappingRightMotor = "Motor3";
        await harness.ViewModel.SaveFocusMappingCommand.ExecuteAsync(null);
        Assert.Equal("Motor2: left focus motor; film transport motor", harness.ViewModel.Motor2RoleText);
        Assert.Equal("Motor3: right focus motor", harness.ViewModel.Motor3RoleText);
        Assert.Contains("Applies the device motor configuration to Motor2, then reads motion state", harness.ViewModel.Motor2ApplyConfigEffectText, StringComparison.Ordinal);

        harness.ViewModel.Motor1MoveValue = "not-a-number";
        Assert.Contains("requested move is invalid", harness.ViewModel.Motor1MoveSummaryText, StringComparison.Ordinal);
        harness.ViewModel.Motor1MoveValue = "200";
        Assert.DoesNotContain("requested move is invalid", harness.ViewModel.Motor1MoveSummaryText, StringComparison.Ordinal);
        Assert.Contains("unmapped", harness.ViewModel.Motor1MoveSummaryText, StringComparison.Ordinal);
        Assert.Contains("Motor1: no configured semantic role", harness.ViewModel.Motor1MoveSummaryText, StringComparison.Ordinal);
        Assert.Contains(nameof(harness.ViewModel.Motor1MoveSummaryText), notifications);
        await harness.ViewModel.StopAllMotorsCommand.ExecuteAsync(null);
        Assert.Equal(1, coordinator.StopAllMotionCallCount);
        Assert.Equal("All motor stop commands were dispatched for Motor1, Motor2, and Motor3. Hardware motion state was not verified; read motion state before resuming motion.", harness.ViewModel.StatusText);
    }

    [Theory]
    [InlineData(false, "No connected scanner session is available for a global motor stop.", "Stop all motors failed:")]
    [InlineData(false, "Motor stop commands completed with failures: motor 0: IOException: boom. Hardware motion state was not verified.", "Stop all motors completed with failures:")]
    [InlineData(false, "Global motor stop timed out with an unknown outcome. The current command transaction remains quarantined. Unattempted motors: 1, 2. Hardware motion state was not verified.", "Global motor stop outcome is unknown.")]
    public async Task Todo18StopAllMotors_StatusProjectionLocalizesManagerResults(bool success, string managerMessage, string expectedStatusPrefix)
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true)
        {
            GlobalStopResult = new ScanOperationResult(success, managerMessage)
        };
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator);

        await harness.ViewModel.StopAllMotorsCommand.ExecuteAsync(null);

        Assert.Equal(1, coordinator.StopAllMotionCallCount);
        Assert.DoesNotContain(managerMessage, harness.ViewModel.StatusText, StringComparison.Ordinal);
        Assert.StartsWith(expectedStatusPrefix, harness.ViewModel.StatusText, StringComparison.Ordinal);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.Equal("Motion state is unknown. Read device motion state before starting another motion command.", harness.ViewModel.MotionStateReadRequiredText);
    }

    [Fact]
    public async Task Todo18StaleReadAfterSuccessfulGlobalStop_DoesNotOverwriteActualViewModelMotionState()
    {
        var readEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleStates = CreateIdleMotionStates().Select(state => state with { Running = true }).ToArray();
        var session = new StatusSession(
            isConnected: true,
            motionStateReader: async _ =>
            {
                readEntered.TrySetResult(null);
                await releaseRead.Task;
                return staleStates;
            });
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true)
        {
            GlobalStopResult = new ScanOperationResult(true, "manager stop dispatched")
        };
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator);
        var before = harness.ViewModel.MotionSummaryText;

        var refresh = harness.ViewModel.RefreshMotionCommand.ExecuteAsync(null);
        await readEntered.Task;
        await harness.ViewModel.StopAllMotorsCommand.ExecuteAsync(null);
        releaseRead.TrySetResult(null);
        await refresh;

        Assert.Equal(before, harness.ViewModel.MotionSummaryText);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.Equal(1, coordinator.StopAllMotionCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Todo18EnableAndConfigFailure_LatchUntilExplicitCompleteRefresh(bool applyConfig)
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator);

        if (applyConfig)
            await harness.ViewModel.ApplyMotorConfigCommand.ExecuteAsync("1");
        else
            await harness.ViewModel.EnableMotorCommand.ExecuteAsync("1");

        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.False(harness.ViewModel.EnableMotorCommand.CanExecute("1"));
        Assert.False(harness.ViewModel.ApplyMotorConfigCommand.CanExecute("1"));
        Assert.Equal(0, session.MotionStateCallCount);
        Assert.Equal(applyConfig ? 0 : 1, session.SetMotorEnabledCallCount);
        Assert.Equal(applyConfig ? 1 : 0, session.ApplyMotorConfigCallCount);

        await harness.ViewModel.RefreshMotionCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsMotionStateReadRequired);
        Assert.True(harness.ViewModel.EnableMotorCommand.CanExecute("1"));
        Assert.True(harness.ViewModel.ApplyMotorConfigCommand.CanExecute("1"));
        Assert.Equal(1, session.MotionStateCallCount);
    }

    [Theory]
    [InlineData(ScanDebugRuntimeCommandKind.MoveMotor)]
    [InlineData(ScanDebugRuntimeCommandKind.EnableMotor)]
    [InlineData(ScanDebugRuntimeCommandKind.ApplyMotorConfig)]
    public async Task Todo18ProducerCancellationAfterLastCheck_PreventsCallbackMotorIo(ScanDebugRuntimeCommandKind command)
    {
        var callbackEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true)
        {
            GlobalStopResult = new ScanOperationResult(true, "global stop dispatched"),
            BeforeCallbackAdmission = async () =>
            {
                callbackEntered.TrySetResult(null);
                await releaseCallback.Task;
            }
        };
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, null, connected: true, scanSession: session, sessionCoordinator: coordinator);
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));

        var commandTask = command switch
        {
            ScanDebugRuntimeCommandKind.MoveMotor => harness.ViewModel.MoveMotorCommand.ExecuteAsync("1"),
            ScanDebugRuntimeCommandKind.EnableMotor => harness.ViewModel.EnableMotorCommand.ExecuteAsync("1"),
            ScanDebugRuntimeCommandKind.ApplyMotorConfig => harness.ViewModel.ApplyMotorConfigCommand.ExecuteAsync("1"),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        await callbackEntered.Task;
        await harness.ViewModel.StopAllMotorsCommand.ExecuteAsync(null);
        releaseCallback.TrySetResult(null);
        await commandTask;

        Assert.Empty(session.MoveAndWaitRequests);
        Assert.Empty(session.SetMotorEnabledRequests);
        Assert.Equal(0, session.ApplyMotorConfigCallCount);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        var claims = ReadPrivateField<ScanDebugRuntimeOperationClaims>(harness.ViewModel, "_runtimeOperationClaims")!;
        Assert.True(claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(true), command, out var nextClaim, out _));
        nextClaim!.Dispose();
    }

    [Theory]
    [InlineData(ScanDebugRuntimeCommandKind.AutoFocus, false)]
    [InlineData(ScanDebugRuntimeCommandKind.StartManualFocus, false)]
    [InlineData(ScanDebugRuntimeCommandKind.StartScan, false)]
    [InlineData(ScanDebugRuntimeCommandKind.AutoBlackAdjust, false)]
    [InlineData(ScanDebugRuntimeCommandKind.AutoWhiteAdjust, false)]
    [InlineData(ScanDebugRuntimeCommandKind.AutoCalibrate, false)]
    [InlineData(ScanDebugRuntimeCommandKind.MoveMotor, false)]
    [InlineData(ScanDebugRuntimeCommandKind.MoveMotor, true)]
    [InlineData(ScanDebugRuntimeCommandKind.EnableMotor, false)]
    [InlineData(ScanDebugRuntimeCommandKind.ApplyMotorConfig, false)]
    public async Task Todo18ClaimChanged_StopAllSynchronouslyInvalidatesEveryProducerBeforeRuntimeSubmission(ScanDebugRuntimeCommandKind command, bool testLeftFocusMapping)
    {
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates());
        var coordinator = new StatusSessionCoordinator(session, useConnectedSession: true)
        {
            GlobalStopResult = new ScanOperationResult(true, "global stop dispatched")
        };
        var autoFocus = new StatusAutoFocus((_, request, _, _, _) => Task.FromResult(new ScanAutofocusResult(request.SampleRows, 0, 0, 1, 1, 1, 0)));
        var autoCalibration = new StatusAutoCalibration(autoBlackResult: Todo15InputSnapshot);
        var harness = await StatusHarness.CreateAsync(
            CreateKnownRoleProfiles(),
            null,
            attachRuntimeBindings: true,
            autoCalibration: autoCalibration,
            connected: true,
            scanSession: session,
            sessionCoordinator: coordinator,
            autoFocus: autoFocus,
            deviceSettings: new StatusDeviceSettings());
        await ConfigureDeviceChannelRolesAsync(harness, "Red", "Green", "Blue", "IR");
        await SetValidParameterInputsAsync(harness, "125");
        InvokeSetDeviceTimingState(harness.ViewModel, new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));

        var claims = ReadPrivateField<ScanDebugRuntimeOperationClaims>(harness.ViewModel, "_runtimeOperationClaims")!;
        var useConnectedSessionCalls = coordinator.UseConnectedSessionCallCount;
        var runConnectedSessionStateCalls = coordinator.RunConnectedSessionStateCallCount;
        var changed = 0;
        EventHandler? stopOnFirstClaim = null;
        stopOnFirstClaim = (_, _) =>
        {
            claims.Changed -= stopOnFirstClaim;
            changed++;
            harness.ViewModel.StopAllMotorsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        };
        claims.Changed += stopOnFirstClaim;
        try
        {
            switch (command)
            {
                case ScanDebugRuntimeCommandKind.AutoFocus:
                    await harness.ViewModel.AutoFocusCommand.ExecuteAsync(null);
                    break;
                case ScanDebugRuntimeCommandKind.StartManualFocus:
                    harness.ViewModel.BeginManualFocusHold(positive: true);
                    break;
                case ScanDebugRuntimeCommandKind.StartScan:
                    await harness.ViewModel.StartScanCommand.ExecuteAsync(null);
                    break;
                case ScanDebugRuntimeCommandKind.AutoBlackAdjust:
                    await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
                    break;
                case ScanDebugRuntimeCommandKind.AutoWhiteAdjust:
                    await harness.ViewModel.AutoWhiteAdjustCommand.ExecuteAsync(null);
                    break;
                case ScanDebugRuntimeCommandKind.AutoCalibrate:
                    await harness.ViewModel.AutoCalibrateCommand.ExecuteAsync(null);
                    break;
                case ScanDebugRuntimeCommandKind.MoveMotor:
                    if (testLeftFocusMapping)
                        await harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);
                    else
                        await harness.ViewModel.MoveMotorCommand.ExecuteAsync("1");
                    break;
                case ScanDebugRuntimeCommandKind.EnableMotor:
                    await harness.ViewModel.EnableMotorCommand.ExecuteAsync("1");
                    break;
                case ScanDebugRuntimeCommandKind.ApplyMotorConfig:
                    await harness.ViewModel.ApplyMotorConfigCommand.ExecuteAsync("1");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }
        finally
        {
            claims.Changed -= stopOnFirstClaim;
        }

        await harness.FlushAsync();

        Assert.Equal(1, changed);
        Assert.Equal(1, coordinator.StopAllMotionCallCount);
        Assert.Equal(useConnectedSessionCalls, coordinator.UseConnectedSessionCallCount);
        Assert.Equal(runConnectedSessionStateCalls, coordinator.RunConnectedSessionStateCallCount);
        Assert.Empty(session.MoveAndWaitRequests);
        Assert.Empty(session.SetMotorEnabledRequests);
        Assert.Equal(0, autoFocus.CallCount);
        Assert.Null(autoCalibration.LastAutoBlackInput);
        Assert.False(harness.ViewModel.IsAutoFocusing);
        Assert.False(harness.ViewModel.IsAutoCalibrating);
        Assert.False(harness.ViewModel.IsManualFocusing);
        Assert.False(harness.ViewModel.IsApplyingMotion);
        Assert.False(harness.ViewModel.IsRunning);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);

        Assert.True(claims.TryClaim(
            new ScanDebugRuntimeOperationSnapshot(true),
            ScanDebugRuntimeCommandKind.MoveMotor,
            out var nextClaim,
            out _));
        nextClaim!.Dispose();

        await harness.ViewModel.RefreshMotionCommand.ExecuteAsync(null);
        Assert.False(harness.ViewModel.IsMotionStateReadRequired);
        Assert.True(harness.ViewModel.MoveMotorCommand.CanExecute("1"));

        await harness.ViewModel.MoveMotorCommand.ExecuteAsync("1");
        Assert.Single(session.MoveAndWaitRequests);
    }

    [Fact]
    public async Task Todo18StopMotor_GivenRawStopFailure_ThenLatchesMotionStateUntilExplicitRefresh()
    {
        var motion = new FocusMotionProbe { StopFailureMotorId = 0 };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion);
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        await harness.ViewModel.StopMotorCommand.ExecuteAsync("1");

        Assert.Single(session.StopMotorRequests);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.Equal(
            "ScanDebug_Runtime_StatusMotorStopFailed".GetLocalizedFormat("Motor1", "injected stop failure"),
            harness.ViewModel.StatusText);

        await harness.ViewModel.RefreshMotionCommand.ExecuteAsync(null);
        Assert.False(harness.ViewModel.IsMotionStateReadRequired);
    }

    [Fact]
    public async Task Todo18FocusMapping_GivenReverseAndCleanupFailures_ThenLatchesMotionStateAfterActualCommand()
    {
        var motion = new FocusMotionProbe { StopFailureMotorId = 0 };
        var session = new StatusSession(isConnected: true, motionStates: CreateIdleMotionStates(), focusMotion: motion)
        {
            MoveAndWaitFailure = new IOException("injected reverse failure"),
            MoveAndWaitFailureOnCall = 2
        };
        var harness = await StatusHarness.CreateAsync(
            EmptyProfiles,
            null,
            connected: true,
            scanSession: session,
            sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));

        await harness.ViewModel.TestLeftFocusMappingCommand.ExecuteAsync(null);

        Assert.Equal(2, session.MoveAndWaitRequests.Count);
        Assert.NotEqual(session.MoveAndWaitRequests[0].Direction, session.MoveAndWaitRequests[1].Direction);
        Assert.Single(session.StopMotorRequests);
        Assert.True(harness.ViewModel.IsMotionStateReadRequired);
        Assert.Contains("injected reverse failure", harness.ViewModel.FocusMappingStatusText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StatusHarness
    {
        private StatusHarness(
            ScanDebugViewModel viewModel,
            RecordingCalibrationProfileRepository repository,
            ScanFilmProfileWorkspace workspace,
            StatusFilmProfileFiles filmProfileFiles,
            RecordingUiDispatcher dispatcher)
        {
            ViewModel = viewModel;
            Repository = repository;
            Workspace = workspace;
            FilmProfileFiles = filmProfileFiles;
            Dispatcher = dispatcher;
        }

        public ScanDebugViewModel ViewModel { get; }
        public RecordingCalibrationProfileRepository Repository { get; }
        public ScanFilmProfileWorkspace Workspace { get; }
        public StatusFilmProfileFiles FilmProfileFiles { get; }
        public RecordingUiDispatcher Dispatcher { get; }

        public static async Task<StatusHarness> CreateAsync(
            IReadOnlyDictionary<string, ScanChannelCalibrationProfile> persistedProfiles,
            ScanFilmProfileDraft? currentDraft,
            bool attachRuntimeBindings = false,
            StatusAutoCalibration? autoCalibration = null,
            ushort? previewSample = null,
            bool connected = false,
            StatusWorkflow? workflow = null,
            StatusChannelImages? channelImages = null,
            StatusPreviewPresenter? previewPresenter = null,
            IScanSessionService? scanSession = null,
            IScanDebugSessionCoordinator? sessionCoordinator = null,
            IScanParameterService? parameters = null,
            IScanIlluminationService? illumination = null,
            IScanAutoFocusService? autoFocus = null,
            IScanDeviceSettingsService? deviceSettings = null)
        {
            var repository = new RecordingCalibrationProfileRepository
            {
                PersistedSnapshot = new ScanCalibrationProfileRepositorySnapshot(persistedProfiles, null)
            };
            await repository.InitializeAsync(CancellationToken.None);

            var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
            if (currentDraft is not null)
                workspace.SetCurrentDraft(currentDraft);

            var dispatcher = new RecordingUiDispatcher(autoDrain: false);
            var filmProfileFiles = new StatusFilmProfileFiles();
            var session = scanSession ?? new StatusSession(connected);
            var viewModel = new ScanDebugViewModel(
                session,
                parameters ?? new StatusParameters(),
                new StatusImageDecoder(previewSample),
                previewPresenter ?? new StatusPreviewPresenter(),
                channelImages ?? new StatusChannelImages(),
                autoCalibration ?? new StatusAutoCalibration(),
                autoFocus ?? new StatusAutoFocus(),
                illumination ?? new StatusIllumination(),
                new StatusTransferSettings(),
                workflow ?? new StatusWorkflow(),
                deviceSettings ?? new StatusDeviceSettings(),
                repository,
                workspace,
                filmProfileFiles,
                new StatusDebugOutputMirror(),
                sessionCoordinator ?? new StatusSessionCoordinator(connected ? session : null),
                dispatcher);
            if (attachRuntimeBindings)
            {
                viewModel.FilmProfileDiscardConfirmationRequested += (_, request) => request.CompletionSource.TrySetResult(true);
                viewModel.AttachRuntimeBindings();
            }
            var harness = new StatusHarness(viewModel, repository, workspace, filmProfileFiles, dispatcher);
            await harness.FlushAsync();
            return harness;
        }

        public async Task SelectChannelAsync(string role)
        {
            ViewModel.SelectedCalibrationChannel = role;
            await FlushAsync();
        }

        public async Task FlushAsync()
        {
            Dispatcher.DrainAll();
            await Task.Yield();
            Dispatcher.DrainAll();
        }
    }

    private sealed class StatusParameters : IScanParameterService
    {
        private readonly TaskCompletionSource<object?>? _applyEntered;
        private readonly TaskCompletionSource<object?>? _releaseApply;
        private readonly TaskCompletionSource<object?>? _globalClockApplied;
        private readonly TaskCompletionSource<object?>? _releaseGlobalClockApply;
        private readonly bool _cancelGlobalClockApply;
        private readonly Exception? _globalClockApplyFailure;
        private readonly bool _cancelChannelApply;
        private readonly Exception? _channelApplyFailure;
        private readonly int? _failOnChannelApplyNumber;
        private readonly int? _blockOnChannelApplyNumber;
        private readonly ScanParameterSnapshot _loadedSnapshot;

        public StatusParameters(
            TaskCompletionSource<object?>? applyEntered = null,
            TaskCompletionSource<object?>? releaseApply = null,
            TaskCompletionSource<object?>? globalClockApplied = null,
            TaskCompletionSource<object?>? releaseGlobalClockApply = null,
            bool cancelGlobalClockApply = false,
            Exception? globalClockApplyFailure = null,
            bool cancelChannelApply = false,
            Exception? channelApplyFailure = null,
            int? failOnChannelApplyNumber = null,
            int? blockOnChannelApplyNumber = null,
            ScanParameterSnapshot? loadedSnapshot = null)
        {
            _applyEntered = applyEntered;
            _releaseApply = releaseApply;
            _globalClockApplied = globalClockApplied;
            _releaseGlobalClockApply = releaseGlobalClockApply;
            _cancelGlobalClockApply = cancelGlobalClockApply;
            _globalClockApplyFailure = globalClockApplyFailure;
            _cancelChannelApply = cancelChannelApply;
            _channelApplyFailure = channelApplyFailure;
            _failOnChannelApplyNumber = failOnChannelApplyNumber;
            _blockOnChannelApplyNumber = blockOnChannelApplyNumber ?? (applyEntered is null ? null : 1);
            _loadedSnapshot = loadedSnapshot ?? Todo15InputSnapshot;
        }

        public int GlobalClockApplyCount { get; private set; }

        public int ChannelApplyCount { get; private set; }

        public List<ScanParameterSnapshot> AppliedSnapshots { get; } = [];

        public List<uint> AppliedGlobalClocks { get; } = [];

        public List<bool> ApplyTokensCanBeCanceled { get; } = [];

        public int LoadCount { get; private set; }

        public List<bool> LoadTokensCanBeCanceled { get; } = [];

        public IReadOnlyList<ScanParameterDefinition> Definitions { get; } = [];

        public bool TryParseInput(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz, out ScanParameterSnapshot snapshot, out string error)
        {
            if (TryParseExposureMicroseconds(exposureMicroseconds, sysClockMhz, out var exposureTicks, out error)
                && int.TryParse(adc1Offset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset1)
                && ushort.TryParse(adc1Gain, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gain1)
                && int.TryParse(adc2Offset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset2)
                && ushort.TryParse(adc2Gain, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gain2)
                && TryParseSysClockMhz(sysClockMhz, out var clock))
            {
                snapshot = new ScanParameterSnapshot(exposureTicks, offset1, gain1, offset2, gain2, clock);
                error = string.Empty;
                return true;
            }

            snapshot = new ScanParameterSnapshot(0, 0, 0, 0, 0, 0);
            error = string.IsNullOrEmpty(error) ? "invalid status test inputs" : error;
            return false;
        }

        private static bool TryParseExposureMicroseconds(string text, string sysClockMhzText, out ushort exposureTicks, out string error)
        {
            exposureTicks = 0;
            if (!TryParseSysClockMhz(sysClockMhzText, out var sysClockKhz))
            {
                error = "System Clock must be a decimal in [30, 200] MHz with integral kHz precision.";
                return false;
            }

            var trimmed = text?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)
                || !double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var exposureMicroseconds)
                || !ScanTimingMath.TryConvertMicrosecondsToExposureTicks(exposureMicroseconds, sysClockKhz, out exposureTicks))
            {
                error = "Exposure must be a finite exposure in microseconds that maps to a valid tick value.";
                exposureTicks = 0;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryParseSysClockMhz(string text, out uint sysClockKhz)
        {
            sysClockKhz = 0;
            var trimmed = text?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)
                || GetFractionalDigits(trimmed).Length > 3
                || !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var sysClockMhz)
                || sysClockMhz < 30m
                || sysClockMhz > 200m)
            {
                return false;
            }

            var khz = sysClockMhz * 1000m;
            if (decimal.Truncate(khz) != khz
                || khz < ScanDebugConstants.MinSysClockKhz
                || khz > ScanDebugConstants.MaxSysClockKhz)
            {
                return false;
            }

            sysClockKhz = (uint)khz;
            return true;
        }

        private static string GetFractionalDigits(string text)
        {
            var decimalSeparatorIndex = text.IndexOf('.');
            if (decimalSeparatorIndex < 0 || decimalSeparatorIndex == text.Length - 1)
                return string.Empty;

            return text[(decimalSeparatorIndex + 1)..];
        }

        public ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz)
            => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public string FormatOffsetForInput(int offset)
            => offset.ToString(CultureInfo.InvariantCulture);

        public Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
        {
            LoadCount++;
            LoadTokensCanBeCanceled.Add(ct.CanBeCanceled);
            return Task.FromResult(_loadedSnapshot);
        }

        public async Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct)
        {
            GlobalClockApplyCount++;
            AppliedGlobalClocks.Add(sysClockKhz);
            _globalClockApplied?.TrySetResult(null);
            if (_releaseGlobalClockApply is not null)
                await _releaseGlobalClockApply.Task.WaitAsync(ct);
            if (_cancelGlobalClockApply)
                throw new OperationCanceledException(ct);
            if (_globalClockApplyFailure is not null)
                throw _globalClockApplyFailure;
        }

        public async Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
        {
            ChannelApplyCount++;
            AppliedSnapshots.Add(snapshot);
            ApplyTokensCanBeCanceled.Add(ct.CanBeCanceled);
            if (_cancelChannelApply)
                throw new OperationCanceledException(ct);

            if (_channelApplyFailure is not null
                && (_failOnChannelApplyNumber is null || _failOnChannelApplyNumber == ChannelApplyCount))
            {
                throw _channelApplyFailure;
            }

            if (_blockOnChannelApplyNumber == ChannelApplyCount)
            {
                _applyEntered?.TrySetResult(null);
                await _releaseApply!.Task.WaitAsync(ct);
            }
        }
    }

    private sealed class StatusImageDecoder : IScanImageDecoder
    {
        private readonly ushort? _sample;

        public StatusImageDecoder(ushort? sample = null)
        {
            _sample = sample;
        }

        public int GetDecodedPixelsPerLine() => ScanDebugConstants.DecodedPixelsPerLine;

        public (int Start, int EndInclusive) GetEffectivePixelRange()
            => (0, ScanDebugConstants.DecodedPixelsPerLine - 1);

        public void DecodeToBgra(byte[] lineBuffer, int rows, Stream destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeToBgra(byte[] lineBuffer, int rows, Span<byte> destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public void DecodeWaterfallStripToBgra(byte[] lineBuffer, int rows, byte[] destination, bool applyGammaCorrection, double gamma, bool applyWhiteLevel, ushort whiteLevel)
            => throw new NotSupportedException();

        public bool TryGetSample16(byte[] lineBuffer, int rows, int x, int y, out ushort sample)
        {
            sample = _sample.GetValueOrDefault();
            return _sample is not null;
        }
    }

    private sealed class StatusPreviewPresenter : IScanPreviewPresenter
    {
        private readonly TaskCompletionSource<object?>? _renderEntered;
        private readonly TaskCompletionSource<object?>? _releaseRender;

        public StatusPreviewPresenter(
            TaskCompletionSource<object?>? renderEntered = null,
            TaskCompletionSource<object?>? releaseRender = null)
        {
            _renderEntered = renderEntered;
            _releaseRender = releaseRender;
        }

        public int RenderCallCount { get; private set; }
        public List<byte[]> RenderedBuffers { get; } = [];

        public bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, ScanPreviewFrame? reusableFrame, out ScanPreviewFrame? frame, out string error)
        {
            RenderCallCount++;
            RenderedBuffers.Add(lineBuffer);
            _renderEntered?.TrySetResult(null);
            _releaseRender?.Task.GetAwaiter().GetResult();
            frame = null;
            error = "Preview rendering is not part of status ownership tests.";
            return false;
        }

        public bool TryRender(byte[] lineBuffer, int rows, ScanPreviewRenderOptions options, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
        {
            RenderCallCount++;
            RenderedBuffers.Add(lineBuffer);
            _renderEntered?.TrySetResult(null);
            _releaseRender?.Task.GetAwaiter().GetResult();
            bitmap = null;
            error = "Preview rendering is not part of status ownership tests.";
            return false;
        }

        public void Reset()
        {
        }
    }

    private sealed class StatusChannelImages : IScanChannelImageService
    {
        public List<ScanWorkflowResult> PartialResults { get; } = [];
        public List<IReadOnlyDictionary<int, int>> PartialCompletedRows { get; } = [];

        public bool TryBuildRawPreview(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, int channelIndex, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error)
        {
            bitmap = null;
            error = "Channel image rendering is not part of status ownership tests.";
            return false;
        }

        public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
        {
            frame = null;
            error = "Channel image rendering is not part of status ownership tests.";
            return false;
        }

        public bool TryBuildPartialRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, IReadOnlyDictionary<int, int> completedRowsByPassIndex, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false)
        {
            PartialResults.Add(result);
            PartialCompletedRows.Add(new Dictionary<int, int>(completedRowsByPassIndex));
            frame = null;
            error = "Channel image rendering is not part of status ownership tests.";
            return false;
        }

        public Task<ScanCompositePixelBuffer> BuildRgbCompositeBufferAsync(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, bool applyWhiteLevel = false, CancellationToken cancellationToken = default)
            => Task.FromException<ScanCompositePixelBuffer>(new NotSupportedException());

        public Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName)
            => Task.FromResult<StorageFile?>(null);

        public Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public Task SaveRgbImageAsync(StorageFile file, ScanCompositePixelBuffer buffer, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public Task<StorageFolder?> PickDngExportFolderAsync()
            => Task.FromResult<StorageFolder?>(null);

        public Task<ScanDngExportResult> ExportDngChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, ScanDngExportMode exportMode = ScanDngExportMode.LinearRaw4, IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles = null, CancellationToken cancellationToken = default)
            => Task.FromException<ScanDngExportResult>(new NotSupportedException());

        public Task ExportMonochromeDngAsync(StorageFolder folder, byte[] pixelData, int rows, ushort exposureTicks, uint sysClockKhz, string channelLabel, ScanChannelCalibrationProfile? profile = null, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public bool TryComputeAlignedChannelColumnAverage(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, string channelRole, ScanColumnRange range, out ushort average, out string error)
        {
            average = 0;
            error = "Channel image rendering is not part of status ownership tests.";
            return false;
        }
    }

    private sealed class StatusAutoCalibration : IScanAutoCalibrationService
    {
        private readonly ScanParameterSnapshot? _autoBlackResult;
        private readonly bool _captureFrame;
        private readonly TaskCompletionSource<object?>? _autoBlackEntered;
        private readonly TaskCompletionSource<object?>? _autoBlackProgressRelease;
        private readonly TaskCompletionSource<object?>? _autoBlackProgressPublished;
        private readonly TaskCompletionSource<object?>? _autoBlackCompletionRelease;
        private readonly ScanParameterSnapshot? _autoBlackProgressSnapshot;
        private readonly string? _autoBlackProgressStatus;
        private readonly Exception? _autoBlackFailure;
        private readonly ScanCalibrationMetrics _beforeMetrics;
        private readonly ScanCalibrationMetrics _afterMetrics;
        private readonly ScanCalibrationCandidateValidation _validation;

        public StatusAutoCalibration(
            ScanParameterSnapshot? autoBlackResult = null,
            bool captureFrame = false,
            TaskCompletionSource<object?>? autoBlackEntered = null,
            TaskCompletionSource<object?>? autoBlackRelease = null,
            ScanParameterSnapshot? autoBlackProgressSnapshot = null,
            string? autoBlackProgressStatus = null,
            TaskCompletionSource<object?>? autoBlackProgressRelease = null,
            TaskCompletionSource<object?>? autoBlackProgressPublished = null,
            TaskCompletionSource<object?>? autoBlackCompletionRelease = null,
            Exception? autoBlackFailure = null,
            ScanCalibrationMetrics? beforeMetrics = null,
            ScanCalibrationMetrics? afterMetrics = null,
            ScanCalibrationCandidateValidation? validation = null)
        {
            _autoBlackResult = autoBlackResult;
            _captureFrame = captureFrame;
            _autoBlackEntered = autoBlackEntered;
            _autoBlackProgressRelease = autoBlackProgressRelease;
            _autoBlackProgressPublished = autoBlackProgressPublished;
            _autoBlackCompletionRelease = autoBlackCompletionRelease ?? autoBlackRelease;
            _autoBlackProgressSnapshot = autoBlackProgressSnapshot;
            _autoBlackProgressStatus = autoBlackProgressStatus;
            _autoBlackFailure = autoBlackFailure;
            _beforeMetrics = beforeMetrics ?? new ScanCalibrationMetrics(20m, 80m, 2m, 4m);
            _afterMetrics = afterMetrics ?? new ScanCalibrationMetrics(5m, 20m, 0.5m, 1m);
            _validation = validation ?? ScanCalibrationCandidateValidation.Valid;
        }

        public ScanCalibrationRoiSettings? LastAutoBlackRoiSettings { get; private set; }

        public ScanParameterSnapshot? LastAutoBlackInput { get; private set; }

        public async Task<ScanParameterSnapshot> AutoBlackAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
        {
            if (_autoBlackResult is null)
                throw new NotSupportedException();

            LastAutoBlackRoiSettings = roiSettings;
            LastAutoBlackInput = currentSnapshot;
            _autoBlackEntered?.TrySetResult(null);
            if (_autoBlackProgressRelease is not null)
                await _autoBlackProgressRelease.Task.WaitAsync(ct);

            if (_autoBlackProgressStatus is not null)
                onStatus?.Invoke(_autoBlackProgressStatus);
            if (_autoBlackProgressSnapshot is not null)
                onSnapshotApplied?.Invoke(_autoBlackProgressSnapshot);
            _autoBlackProgressPublished?.TrySetResult(null);

            if (_autoBlackCompletionRelease is not null)
                await _autoBlackCompletionRelease.Task.WaitAsync(ct);

            if (_captureFrame)
                onFrameCaptured?.Invoke([0], 1, "status-test");

            if (_autoBlackFailure is not null)
                throw _autoBlackFailure;

            return _autoBlackResult;
        }

        public Task<ScanParameterSnapshot> AutoWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
            => Task.FromException<ScanParameterSnapshot>(new NotSupportedException());

        public Task<ScanParameterSnapshot> AutoCalibrateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
            => Task.FromException<ScanParameterSnapshot>(new NotSupportedException());

        public async Task<ScanAutoCalibrationCandidateResult> AutoBlackAdjustCandidateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
            => CreateCandidate(currentSnapshot, await AutoBlackAdjustAsync(session, currentSnapshot, roiSettings, promptAsync, onStatus, onSnapshotApplied, onFrameCaptured, ct));

        public Task<ScanAutoCalibrationCandidateResult> AutoWhiteAdjustCandidateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
            => Task.FromException<ScanAutoCalibrationCandidateResult>(new NotSupportedException());

        public Task<ScanAutoCalibrationCandidateResult> AutoCalibrateCandidateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, ScanCalibrationRoiSettings roiSettings, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
            => Task.FromException<ScanAutoCalibrationCandidateResult>(new NotSupportedException());

        private ScanAutoCalibrationCandidateResult CreateCandidate(ScanParameterSnapshot original, ScanParameterSnapshot candidate)
            => new(original, candidate, _beforeMetrics, _afterMetrics, _validation);
    }

    private sealed class StatusAutoFocus : IScanAutoFocusService
    {
        private readonly Func<IScanSessionService, ScanAutofocusRequest, Action<string>?, Action<byte[], int, string>?, CancellationToken, Task<ScanAutofocusResult>>? _execute;

        public StatusAutoFocus(Func<IScanSessionService, ScanAutofocusRequest, Action<string>?, Action<byte[], int, string>?, CancellationToken, Task<ScanAutofocusResult>>? execute = null)
        {
            _execute = execute;
        }

        public int CallCount { get; private set; }

        public List<ScanAutofocusRequest> Requests { get; } = [];

        public Task<ScanAutofocusResult> AutoFocusAsync(IScanSessionService session, ScanAutofocusRequest request, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct)
        {
            CallCount++;
            Requests.Add(request);
            return _execute is null
                ? Task.FromException<ScanAutofocusResult>(new NotSupportedException())
                : _execute(session, request, onStatus, onFrameCaptured, ct);
        }
    }

    private sealed class StatusIllumination : IScanIlluminationService
    {
        private readonly ScanIlluminationState _state;

        public StatusIllumination(ScanIlluminationState? state = null)
        {
            _state = state ?? new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public List<ScanIlluminationState> RestoredStates { get; } = [];

        public List<bool> RestoreTokensCanBeCanceled { get; } = [];

        public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromResult(_state);

        public Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
        {
            RestoredStates.Add(state);
            RestoreTokensCanBeCanceled.Add(ct.CanBeCanceled);
            return Task.CompletedTask;
        }
    }

    private sealed class StatusTransferSettings : IScanTransferSettingsService
    {
        public event EventHandler? BulkInReadModeChanged;

        public ScanBulkInReadMode BulkInReadMode => ScanBulkInReadMode.SingleRequest;

        public ScanBulkInTransferOptions DefaultSettings { get; } = new(ScanBulkInReadMode.SingleRequest, 16 * 1024, 1, ScanDebugConstants.ImageReadTimeoutMs, false);

        public ScanBulkInTransferOptions Settings => DefaultSettings;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _ = BulkInReadModeChanged;
            return Task.CompletedTask;
        }

        public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StatusWorkflow : IScanWorkflowService
    {
        private readonly Func<ScanWorkflowRequest, ScanWorkflowRowsAvailableHandler?, CancellationToken, Task<ScanWorkflowResult>>? _execute;

        public StatusWorkflow(Func<ScanWorkflowRequest, ScanWorkflowRowsAvailableHandler?, CancellationToken, Task<ScanWorkflowResult>>? execute = null)
        {
            _execute = execute;
        }

        public int ExecuteCount { get; private set; }

        public Task<ScanWorkflowResult> ExecuteAsync(IScanSessionService session, ScanWorkflowRequest request, CancellationToken ct, Action<ScanWorkflowProgress>? onProgress = null, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onByteProgress = null, ScanWorkflowRowsAvailableHandler? onRowsAvailable = null)
        {
            ExecuteCount++;
            return _execute is null
                ? Task.FromException<ScanWorkflowResult>(new NotSupportedException())
                : _execute(request, onRowsAvailable, ct);
        }
    }

    private sealed class StatusDeviceSettings : IScanDeviceSettingsService
    {
        private readonly ScanDeviceSettings _initializedSettings;
        private readonly TaskCompletionSource<object?>? _initializeEntered;
        private readonly TaskCompletionSource<object?>? _releaseInitialize;
        private bool _isInitialized;

        public StatusDeviceSettings(
            ScanDeviceSettings? initializedSettings = null,
            TaskCompletionSource<object?>? initializeEntered = null,
            TaskCompletionSource<object?>? releaseInitialize = null)
        {
            _initializedSettings = (initializedSettings ?? ScanDeviceSettings.CreateDefault()).Normalize();
            _initializeEntered = initializeEntered;
            _releaseInitialize = releaseInitialize;
        }

        public ScanDeviceSettings DefaultSettings => ScanDeviceSettings.CreateDefault();

        public ScanDeviceSettings Settings { get; private set; } = ScanDeviceSettings.CreateDefault();

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _initializeEntered?.TrySetResult(null);
            if (_releaseInitialize is not null)
                await _releaseInitialize.Task;
            else
                await Task.Yield();
            Settings = _initializedSettings;
            _isInitialized = true;
        }

        public Task SetSettingsAsync(ScanDeviceSettings settings)
        {
            Settings = settings.Normalize();
            return Task.CompletedTask;
        }
    }

    private sealed class StatusFilmProfileFiles : IScanFilmProfileFileCoordinator
    {
        public bool ExportSucceeds { get; set; }
        public ScanFilmParameterProfileSet? ExportedProfile { get; private set; }
        public ScanFilmParameterProfileSet? ImportedProfile { get; set; }
        public ScanFilmProfileFileImportResult? ImportResult { get; set; }

        public Task<bool> ExportAsync(ScanFilmParameterProfileSet profileSet, CancellationToken ct)
        {
            ExportedProfile = profileSet;
            return Task.FromResult(ExportSucceeds);
        }

        public Task<ScanFilmProfileFileImportResult> ImportAsync(CancellationToken ct)
            => Task.FromResult(ImportResult ?? new ScanFilmProfileFileImportResult(false, ImportedProfile, null));
    }

    private sealed class StatusDebugOutputMirror : IDebugOutputMirrorService
    {
        public event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;

        public IReadOnlyList<DebugOutputMirrorEntry> RecentEntries { get; } = [];

        public void Mirror(string source, string message)
            => EntryMirrored?.Invoke(this, new DebugOutputMirrorEntry(DateTimeOffset.UnixEpoch, source, message, message));

        public Task FlushAsync() => Task.CompletedTask;

        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void ClearRecentEntries()
        {
        }
    }

    private sealed class StatusSessionCoordinator : IScanDebugSessionCoordinator
    {
        private readonly IScanSessionService? _connectedSession;
        private readonly Exception? _failureAfterAction;
        private readonly bool _useConnectedSession;

        public StatusSessionCoordinator(
            IScanSessionService? connectedSession = null,
            Exception? failureAfterAction = null,
            bool useConnectedSession = false)
        {
            _connectedSession = connectedSession;
            _failureAfterAction = failureAfterAction;
            _useConnectedSession = useConnectedSession;
            Snapshot = connectedSession is null
                ? ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UnixEpoch)
                : new ScannerDeviceSessionSnapshot(
                    ScannerSessionState.Connected,
                    null,
                    null,
                    null,
                    ScannerReconnectPromptState.None,
                    DateTimeOffset.UnixEpoch);
        }

        public event EventHandler<ScannerDeviceSessionSnapshot>? SnapshotChanged;

        public ScannerDeviceSessionSnapshot Snapshot { get; private set; }

        public bool HasConnectedSession => _connectedSession is not null;

        public IScanSessionService? ConnectedSession => _connectedSession;

        public bool IsConnectBlockedByUsbDebug() => false;

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
            => Task.FromException<ScanOperationResult>(new NotSupportedException());

        public Task<ScanOperationResult> DisconnectAsync(CancellationToken ct)
            => Task.FromException<ScanOperationResult>(new NotSupportedException());

        public Task<ScanOperationResult> SetWarmUpAsync(bool enabled, CancellationToken ct)
        {
            WarmUpRequests.Add(enabled);
            return _connectedSession is null
                ? Task.FromResult(new ScanOperationResult(false, "Scanner not connected."))
                : _connectedSession.SetWarmUpEnabledAsync(enabled, ct);
        }

        public List<bool> WarmUpRequests { get; } = [];

        public ScanOperationResult GlobalStopResult { get; set; } = new(false, "Global stop not configured.");

        public int StopAllMotionCallCount { get; private set; }

        public Task<ScanOperationResult> StopAllMotionAsync(CancellationToken ct)
        {
            StopAllMotionCallCount++;
            return Task.FromResult(GlobalStopResult);
        }

        public int RunConnectedSessionStateCallCount { get; private set; }

        public int UseConnectedSessionCallCount { get; private set; }

        public Func<Task>? BeforeCallbackAdmission { get; init; }

        public async Task<TResult> UseConnectedSessionAsync<TResult>(Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct)
        {
            if (!_useConnectedSession || _connectedSession is null)
                throw new NotSupportedException();

            UseConnectedSessionCallCount++;
            if (BeforeCallbackAdmission is not null)
                await BeforeCallbackAdmission();
            var result = await action(_connectedSession, ct);
            if (_failureAfterAction is not null)
                throw _failureAfterAction;

            return result;
        }

        public async Task<TResult> RunConnectedSessionStateAsync<TResult>(ScannerSessionState state, Func<IScanSessionService, CancellationToken, Task<TResult>> action, CancellationToken ct, bool waitForAvailability = true)
        {
            if (_connectedSession is null)
                throw new NotSupportedException();

            RunConnectedSessionStateCallCount++;
            if (BeforeCallbackAdmission is not null)
                await BeforeCallbackAdmission();
            var result = await action(_connectedSession, ct);
            if (_failureAfterAction is not null)
                throw _failureAfterAction;

            return result;
        }

        public void PublishSnapshot(ScannerDeviceSessionSnapshot snapshot)
        {
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }

        public void NotifySnapshotChanged()
            => PublishSnapshot(Snapshot);
    }

    private sealed class StatusSession : IScanSessionService
    {
        private readonly bool _isConnected;
        private readonly Func<int, CancellationToken, ScanRowsAvailableHandler?, Task<ScanStartResult>>? _startScan;
        private readonly IReadOnlyList<ScanMotorState>? _motionStates;
        private readonly Func<CancellationToken, Task<IReadOnlyList<ScanMotorState>>>? _motionStateReader;
        private readonly FocusMotionProbe? _focusMotion;

        public StatusSession(
            bool isConnected = false,
            Func<int, CancellationToken, ScanRowsAvailableHandler?, Task<ScanStartResult>>? startScan = null,
            IReadOnlyList<ScanMotorState>? motionStates = null,
            FocusMotionProbe? focusMotion = null,
            Func<CancellationToken, Task<IReadOnlyList<ScanMotorState>>>? motionStateReader = null)
        {
            _isConnected = isConnected;
            _startScan = startScan;
            _motionStates = motionStates;
            _focusMotion = focusMotion;
            _motionStateReader = motionStateReader;
        }

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets { get; } = new(false, null, null);

        public bool IsConnected => _isConnected;

        public int SingleTransferMaxRows => ScanDebugConstants.CalibrationSampleRows;

        public CancellationToken ConnectionToken => _focusMotion?.ConnectionToken ?? CancellationToken.None;

        public int SetMotorEnabledCallCount { get; private set; }

        public int ApplyMotorConfigCallCount { get; private set; }

        public int MotionStateCallCount { get; private set; }

        public Exception? MoveAndWaitFailure { get; init; }

        public int? MoveAndWaitFailureOnCall { get; init; }

        public List<(byte MotorId, bool Enabled)> SetMotorEnabledRequests { get; } = [];

        public List<(byte MotorId, bool Direction, uint Steps, uint IntervalNs)> MoveAndWaitRequests { get; } = [];

        public List<(byte MotorId, bool TokenCanBeCanceled)> StopMotorRequests { get; } = [];

        public List<bool> WarmUpEnabledRequests { get; } = [];

        public void RefreshTargets()
            => TargetsChanged?.Invoke(this, EventArgs.Empty);

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
            => Task.FromException<ScanOperationResult>(new NotSupportedException());

        public Task DisconnectAsync()
            => Task.FromException(new NotSupportedException());

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromException<ScanIlluminationState>(new NotSupportedException());

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
        {
            MotionStateCallCount++;
            return _motionStateReader is not null
                ? _motionStateReader(ct)
                : _motionStates is null
                    ? Task.FromException<IReadOnlyList<ScanMotorState>>(new NotSupportedException())
                    : Task.FromResult(_motionStates);
        }

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
        {
            SetMotorEnabledCallCount++;
            SetMotorEnabledRequests.Add((motorId, enabled));
            return Task.FromException(new NotSupportedException());
        }

        public async Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            if (_focusMotion is null)
                throw new NotSupportedException();

            _focusMotion.ManualMoveRequests.Add((motorId, direction, steps, intervalNs, ct.CanBeCanceled));
            if (_focusMotion.BlockFirstManualMove && _focusMotion.ManualMoveRequests.Count == 1)
            {
                _focusMotion.FirstManualMoveEntered.TrySetResult(null);
                await _focusMotion.ReleaseFirstManualMove.Task;
            }
        }

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromException(new NotSupportedException());

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
        {
            if (_focusMotion is null)
                return Task.FromException<ScanMotorState>(new NotSupportedException());

            _focusMotion.WaitRequests.Add((motorId, ct.CanBeCanceled));
            return Task.FromResult(new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0));
        }

        public async Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
        {
            MoveAndWaitRequests.Add((motorId, direction, steps, intervalNs));
            if (MoveAndWaitFailure is not null
                && (MoveAndWaitFailureOnCall is null || MoveAndWaitFailureOnCall == MoveAndWaitRequests.Count))
                throw MoveAndWaitFailure;

            if (_focusMotion is not null)
            {
                _focusMotion.MappingMoveRequests.Add((motorId, direction, ct.CanBeCanceled));
                if (_focusMotion.BlockMappingForwardCompletion && _focusMotion.MappingMoveRequests.Count == 1)
                {
                    _focusMotion.MappingForwardCompleted.TrySetResult(null);
                    await _focusMotion.ReleaseMappingForwardCompletion.Task;
                }
            }

            return new ScanMotorState(motorId, true, false, false, 0, intervalNs, 0);
        }

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
        {
            StopMotorRequests.Add((motorId, ct.CanBeCanceled));
            if (_focusMotion?.StopFailureMotorId == motorId)
                return Task.FromException(new IOException("injected stop failure"));

            return Task.CompletedTask;
        }

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
        {
            ApplyMotorConfigCallCount++;
            return Task.FromException(new NotSupportedException());
        }

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
        {
            WarmUpEnabledRequests.Add(enabled);
            return Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));
        }

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
        {
            if (_focusMotion is not null)
            {
                _focusMotion.ScanRequests.Add((rows, ct.CanBeCanceled));
                return Task.FromResult(new ScanStartResult(false, "injected focus scan failure", null));
            }

            return _startScan?.Invoke(rows, ct, onRowsAvailable)
                ?? Task.FromException<ScanStartResult>(new NotSupportedException());
        }

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null, uint? expectedLineTimeUs = null)
            => StartScanAsync(totalRows, ct, onStatus, onDiagnostic, onProgress, onRowsAvailable, expectedLineTimeUs);

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromException<ScanStopResult>(new NotSupportedException());

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromException<ScanControlFrame>(new NotSupportedException());

        public void Dispose()
        {
            _ = MotionEventReceived;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FocusMotionProbe
    {
        public FocusMotionProbe(CancellationToken connectionToken = default)
        {
            ConnectionToken = connectionToken;
        }

        public CancellationToken ConnectionToken { get; }

        public bool BlockFirstManualMove { get; init; }

        public bool BlockMappingForwardCompletion { get; init; }

        public byte? StopFailureMotorId { get; init; }

        public TaskCompletionSource<object?> FirstManualMoveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> ReleaseFirstManualMove { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> MappingForwardCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> ReleaseMappingForwardCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(byte MotorId, bool Direction, uint Steps, uint IntervalNs, bool TokenCanBeCanceled)> ManualMoveRequests { get; } = [];

        public List<(byte MotorId, bool TokenCanBeCanceled)> WaitRequests { get; } = [];

        public List<(byte MotorId, bool Direction, bool TokenCanBeCanceled)> MappingMoveRequests { get; } = [];

        public List<(int Rows, bool TokenCanBeCanceled)> ScanRequests { get; } = [];
    }
}
