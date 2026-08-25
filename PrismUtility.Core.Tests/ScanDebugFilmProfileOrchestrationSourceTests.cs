using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanDebugFilmProfileOrchestrationSourceTests
{
    [Fact]
    public void LegacyFixtures_CharacterizePreTodo8ObservableCalls()
    {
        var save = ReadFixture("legacy-scan-debug-save-method.txt");
        var load = ReadFixture("legacy-scan-debug-load-method.txt");

        Assert.Contains("SaveSelectedCalibrationProfileAsync", save, StringComparison.Ordinal);
        Assert.Contains("ReplaceProfilesAsync", load, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveFilmProfileJson_UsesWorkspaceAndCoordinatorWithoutRepositorySave()
    {
        var save = ExtractMethod(ReadViewModelSource(), "SaveFilmProfileJson");

        Assert.DoesNotContain("SaveSelectedCalibrationProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceProfilesAsync", save, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", save, StringComparison.Ordinal);
        Assert.True(save.IndexOf("SynchronizeFilmProfileDraftFromInputs", StringComparison.Ordinal) < save.IndexOf("_filmProfileFiles.ExportAsync", StringComparison.Ordinal));
        Assert.True(save.IndexOf("_filmProfileFiles.ExportAsync", StringComparison.Ordinal) < save.IndexOf("_filmProfileWorkspace.MarkExported", StringComparison.Ordinal));
        Assert.Contains("_filmProfileWorkspace.MarkExported(export.Document.Document!)", save, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndApply_KeepPreviewPureAndApplyOwnsProjection()
    {
        var source = ReadViewModelSource();
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");

        Assert.DoesNotContain("ReplaceProfilesAsync", load, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSelectedCalibrationChannelAsync", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", load, StringComparison.Ordinal);
        Assert.Contains("ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftToFields", apply, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_InvalidCoordinatorResult_ReturnsBeforeStageImport()
    {
        var load = ExtractMethod(ReadViewModelSource(), "LoadFilmProfileJson");
        var importIndex = load.IndexOf("_filmProfileFiles.ImportAsync(CancellationToken.None)", StringComparison.Ordinal);
        var invalidIndex = load.IndexOf("if (imported.Profile is null)", StringComparison.Ordinal);
        var returnIndex = load.IndexOf("return;", invalidIndex, StringComparison.Ordinal);
        var stageIndex = load.IndexOf("_filmProfileWorkspace.StageImport", StringComparison.Ordinal);

        Assert.True(importIndex >= 0, "Load must import through the file coordinator.");
        Assert.True(invalidIndex > importIndex, "Invalid coordinator results must be handled after import.");
        Assert.True(returnIndex > invalidIndex, "The null-profile branch must return.");
        Assert.True(stageIndex > returnIndex, "Invalid coordinator results must return before staging.");
    }

    [Fact]
    public void ExtractMethod_IgnoresBracesInSupportedLexicalContexts()
    {
        const string source = """"
            private void Target()
            {
                if (true) { var nested = 1; }
                // } line comment {
                /* { block comment } */
                var character = '}';
                var normal = "{ normal }";
                var verbatim = @"{ verbatim ""quote"" }";
                var interpolated = $"{ interpolated }";
                var interpolatedVerbatim = $@"{ interpolated verbatim ""quote"" }";
                var raw = """{ raw }""";
                var interpolatedRaw = $"""{ interpolated raw }""";
            }
            private void After() { }
            """";

        var body = ExtractMethod(source, "Target");

        Assert.Contains("var nested = 1;", body, StringComparison.Ordinal);
        Assert.Contains("interpolated raw", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private void After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMethod_HandlesEscapedQuotesAndNestedBlocks()
    {
        const string source = """
            private async Task Target()
            {
                var escaped = "quote: \" and brace: }";
                for (var index = 0; index < 1; index++)
                {
                    if (index == 0) { continue; }
                }
            }
            [RelayCommand]
            private async Task After() { }
            """;

        var body = ExtractMethod(source, "Target");

        Assert.Contains("continue;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMethod_InterpretsVariableInterpolatedRawDelimiterBeforeGenericString()
    {
        var source = "private void Target()\n{\n" +
            "    var raw = $$$\"\"\"\"\n" +
            "    } } } method-like {\n" +
            "    \"\"\"\";\n" +
            "    var marker = 1;\n" +
            "}\nprivate void After() { }\n";

        var body = ExtractMethod(source, "Target");

        Assert.Contains("var marker = 1;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private void After", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9Baseline_RecordsPreMigrationDirtyAssignmentAndCallbackSurface()
    {
        var evidence = ReadTodo9AnalysisEvidence();

        Assert.Contains("HasUnsavedProfileChanges has 4 direct assignments", evidence, StringComparison.Ordinal);
        Assert.Contains("MarkProfileDirty has 16 current callback call sites", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9DesiredContract_UsesSingleSemanticDirtyProjection()
    {
        var source = ReadViewModelSource();
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");

        Assert.Equal(1, Count(source, "HasUnsavedProfileChanges ="));
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkProfileDirty", source, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput", source, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace", source, StringComparison.Ordinal);
    }

    public static TheoryData<string> PersistedFilmProfileCallbacks => new()
    {
        { "OnFilmProfileNameChanged" },
        { "OnSelectedCalibrationChannelChanged" },
        { "OnExposureTicksChanged" },
        { "OnAdc1OffsetChanged" },
        { "OnAdc1GainChanged" },
        { "OnAdc2OffsetChanged" },
        { "OnAdc2GainChanged" },
        { "OnSysClockKhzChanged" },
        { "OnIsMultiChannelScanEnabledChanged" },
        { "OnIsChannel1ReversedChanged" },
        { "OnIsChannel2ReversedChanged" },
        { "OnIsChannel3ReversedChanged" },
        { "OnIsChannel4ReversedChanged" },
        { "OnIsScanRecipeColorManagementEnabledChanged" },
        { "OnScanRecipeRedWavelengthNmChanged" },
        { "OnScanRecipeGreenWavelengthNmChanged" },
        { "OnScanRecipeBlueWavelengthNmChanged" },
        { "OnScanRecipeOutputGammaChanged" },
        { "OnSelectedScanRecipeTargetWhitePointModeChanged" },
        { "OnScanRecipeManualWhitePointColorTemperatureKChanged" },
        { "OnSelectedProfileAlignmentModeChanged" },
        { "OnSelectedProfileDngExportModeChanged" }
    };

    [Theory]
    [MemberData(nameof(PersistedFilmProfileCallbacks))]
    public void Todo9PersistedCallbacks_SynchronizeSemanticDraft(string callback)
        => Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractCallback(ReadViewModelSource(), callback), StringComparison.Ordinal);

    [Fact]
    public void Todo9PersistedInputMutators_UseSynchronizationAndGuardedProjections()
    {
        var source = ReadViewModelSource();
        var synchronization = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var apply = ExtractMethod(source, "ApplyDraftToFields");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");
        var acquisition = ExtractMethod(source, "TryBuildFilmAcquisitionSettings");

        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "NotifyCurrentCalibrationIlluminationInputChanged"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "OnMotorSpeedInputChanged"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "UpdateSelectedRoiRange"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "ResetSelectedRoi"), StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ExtractMethod(source, "ResetAllRois"), StringComparison.Ordinal);
        Assert.Contains("TryBuildCurrentFilmProfileDraft", synchronization, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput = true", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileFiles", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProfileAsync", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSelectedCalibrationChannelAsync", synchronization, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = true", apply, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingFilmProfileWorkspace = wasSynchronizing", apply, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
        Assert.Contains("existing.Led1ChannelColor", acquisition, StringComparison.Ordinal);
        Assert.Contains("clearUnusedInputs: false", acquisition, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9OperationTransitions_RefreshWithoutAdHocDirtyToggles()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var discard = ExtractMethod(source, "DiscardStagedFilmProfileImport");

        Assert.Contains("SynchronizeFilmProfileDraftFromInputs", save, StringComparison.Ordinal);
        Assert.True(save.IndexOf("_filmProfileWorkspace.MarkExported", StringComparison.Ordinal) < save.LastIndexOf("RefreshFilmProfileWorkspaceProjection", StringComparison.Ordinal));
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", load, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", apply, StringComparison.Ordinal);
        Assert.Contains("RefreshFilmProfileWorkspaceProjection", discard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Todo9WorkspaceTransitionBaseline_StageDiscardApplyAndExportAreSemantic()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new PRISM_Utility.Core.Services.ScanFilmProfileWorkspace(repository, new PRISM_Utility.Core.Services.ScanFilmProfileDocumentService());
        var json = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PrismUtility.Core.Tests", "Fixtures", "FilmProfile", "full-v5.json"));

        workspace.StageImport(json);
        workspace.DiscardStagedImport();
        Assert.False(workspace.Snapshot.IsDirty);
        workspace.StageImport(json);
        await workspace.ApplyStagedImportAsync(CancellationToken.None);
        Assert.False(workspace.Snapshot.IsDirty);
        var current = workspace.Snapshot.CurrentDraft;
        workspace.SetCurrentDraft(new PRISM_Utility.Core.Models.ScanFilmProfileDraft(
            "Changed",
            current.SavedAtUtc,
            current.ChannelProfiles,
            current.SelectedCalibrationChannel,
            current.AcquisitionSettings,
            current.ScanRecipeSettings));
        Assert.True(workspace.Snapshot.IsDirty);
        var exported = Assert.IsType<PRISM_Utility.Core.Models.ScanFilmParameterProfileSet>(workspace.BuildExportDocument().Document.Document);
        workspace.MarkExported(exported);
        Assert.False(workspace.Snapshot.IsDirty);
    }

    [Fact]
    public void Todo13Baseline_ScanDebugPreservesTodo8_9_12AuthoringAndSelectionBehavior()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");
        var projection = ExtractMethod(source, "RefreshFilmProfileWorkspaceProjection");
        var baseline = ReadTodo13BaselineEvidence();

        Assert.Contains("ScanDebugFacadeReferences=28", baseline, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ExportAsync", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.MarkExported", save, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", load, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftToFields", apply, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
        Assert.Contains("ScanFilmProfileDirtyState.IsDirty", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_ScanDebugUsesRepositorySnapshotsWithoutFacade()
    {
        var source = ReadViewModelSource();

        Assert.DoesNotContain("IScanChannelParameterProfileService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_channelProfiles", source, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanCalibrationProfileRepository _calibrationProfiles;", source, StringComparison.Ordinal);
        Assert.Contains("IScanCalibrationProfileRepository calibrationProfiles", source, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.InitializeAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.GetSelectedChannelAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_RuntimeMutationsAndUserSelectionUseRepository()
    {
        var source = ReadViewModelSource();
        var clear = ExtractMethod(source, "ClearChannelProfile");
        var loadSelected = ExtractMethod(source, "LoadSelectedCalibrationProfileAsync");
        var save = ExtractMethod(source, "SaveSelectedCalibrationProfileAsync");
        var saveLevels = ExtractMethod(source, "SaveCalibrationLevelsAsync");
        var selection = ExtractCallback(source, "OnSelectedCalibrationChannelChanged");

        Assert.Contains("_calibrationProfiles.ClearProfileAsync(SelectedCalibrationChannel, CancellationToken.None)", clear, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.SetSelectedChannelAsync(channelRole, CancellationToken.None);", loadSelected, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.SaveProfileAsync(", save, StringComparison.Ordinal);
        Assert.Contains("CancellationToken.None", save, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.SaveProfileAsync(", saveLevels, StringComparison.Ordinal);
        Assert.True(selection.IndexOf("if (_isSynchronizingFilmProfileWorkspace)", StringComparison.Ordinal) < selection.IndexOf("HandleSelectedCalibrationChannelChangedAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Todo13DesiredContract_WorkspaceAuthoringStaysPureAndMissingProfilesDoNotFallback()
    {
        var source = ReadViewModelSource();
        var save = ExtractMethod(source, "SaveFilmProfileJson");
        var load = ExtractMethod(source, "LoadFilmProfileJson");
        var apply = ExtractMethod(source, "ApplyStagedFilmProfileImport");
        var workflow = ExtractMethod(source, "TryBuildDebugWorkflowRequest");

        Assert.DoesNotContain("_calibrationProfiles.SaveProfileAsync", save, StringComparison.Ordinal);
        Assert.DoesNotContain("_calibrationProfiles.ReplaceAsync", load, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", apply, StringComparison.Ordinal);
        Assert.Contains("if (!_calibrationProfiles.TryGetProfile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("? profile.Parameters : fallbackSnapshot", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalValidationMessageContract_ScanDebugIssuesUseStableMessageKeys()
    {
        var source = ReadViewModelSource();
        var draftBuilder = ExtractMethod(source, "TryBuildCurrentFilmProfileDraft");
        var issueFactory = ExtractMethod(source, "CreateFilmProfileValidationIssue");

        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, \"ChannelProfiles.Selected.Parameters\", \"FilmProfile.Validation.ChannelParametersInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, \"AcquisitionSettings\", \"FilmProfile.Validation.AcquisitionInputInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidRoiInput, \"ChannelProfiles.Selected.RoiSettings\", \"FilmProfile.Validation.RoiInputInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidColorManagement, \"ScanRecipeSettings\", \"FilmProfile.Validation.ScanRecipeInputInvalid\")", draftBuilder, StringComparison.Ordinal);
        Assert.Contains("string messageKey", issueFactory, StringComparison.Ordinal);
        Assert.Contains("new(code, fieldPath, ScanFilmProfileValidationSeverity.Error, messageKey)", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("parameterError", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("acquisitionError", issueFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("recipeError", issueFactory, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugWorkspaceSnapshotProjection_DistinguishesExternalCurrentDraftFromLocalAndStagedOnlyChanges()
    {
        var source = ReadViewModelSource();
        var snapshotChanged = ExtractMethod(source, "OnFilmProfileWorkspaceSnapshotChanged");
        var synchronization = ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");

        Assert.Contains("_lastProjectedFilmProfileDraft", source, StringComparison.Ordinal);
        Assert.Contains("_lastProjectedStagedFilmProfileImport", source, StringComparison.Ordinal);
        Assert.Contains("ApplyExternalFilmProfileWorkspaceSnapshot(snapshot)", snapshotChanged, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDraftToFields", synchronization, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(snapshot.CurrentDraft, _lastProjectedFilmProfileDraft)", source, StringComparison.Ordinal);
        Assert.Contains("!ReferenceEquals(snapshot.StagedImport, _lastProjectedStagedFilmProfileImport)", source, StringComparison.Ordinal);
    }

    private static string ReadViewModelSource()
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PrismUtility.Core.Tests", "Fixtures", "FilmProfile", name));

    private static string ReadTodo9AnalysisEvidence()
        => File.ReadAllText(Path.Combine(Directory.GetParent(FindHostSoftwareRoot())!.FullName, ".omo", "evidence", "task-9-scan-profile-editor-architecture-analysis.txt"));

    private static string ReadTodo13BaselineEvidence()
        => File.ReadAllText(Path.Combine(Directory.GetParent(FindHostSoftwareRoot())!.FullName, ".omo", "evidence", "task-13-scandebug-baseline.txt"));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

    internal static string ExtractMethod(string source, string methodName)
    {
        var start = FindMethodStart(source, methodName);
        var openBrace = FindCodeOpenBrace(source, start, methodName);
        var closeBrace = FindMatchingCodeBrace(source, openBrace, methodName);
        return source[start..(closeBrace + 1)];
    }

    private static string ExtractCallback(string source, string methodName)
    {
        var start = FindMethodStart(source, methodName);
        var end = source.IndexOf("\n    partial ", start + 1, StringComparison.Ordinal);
        return source[start..(end >= 0 ? end : source.Length)];
    }

    private static int FindMethodStart(string source, string methodName)
    {
        var searchStart = 0;
        while (true)
        {
            var candidate = source.IndexOf($" {methodName}(", searchStart, StringComparison.Ordinal);
            Assert.True(candidate >= 0, $"Could not find {methodName}.");

            var lineStart = source.LastIndexOf('\n', candidate) + 1;
            var declaration = source[lineStart..candidate];
            if (declaration.Contains("private ", StringComparison.Ordinal)
                || declaration.Contains("public ", StringComparison.Ordinal)
                || declaration.Contains("partial ", StringComparison.Ordinal))
            {
                return candidate;
            }

            searchStart = candidate + methodName.Length + 1;
        }
    }

    private static int FindCodeOpenBrace(string source, int start, string methodName)
    {
        var state = LexicalState.Code;
        var rawDelimiterQuotes = 0;
        for (var index = start; index < source.Length; index++)
        {
            if (AdvanceLexicalState(source, ref index, ref state, ref rawDelimiterQuotes))
                continue;
            if (state == LexicalState.Code && source[index] == '{')
                return index;
        }

        throw new InvalidOperationException($"Could not find opening brace for '{methodName}'.");
    }

    private static int FindMatchingCodeBrace(string source, int openBrace, string methodName)
    {
        var depth = 0;
        var state = LexicalState.Code;
        var rawDelimiterQuotes = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (AdvanceLexicalState(source, ref index, ref state, ref rawDelimiterQuotes))
                continue;
            if (state != LexicalState.Code)
                continue;
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return index;
        }

        throw new InvalidOperationException($"Could not find closing brace for '{methodName}'.");
    }

    private static bool AdvanceLexicalState(string source, ref int index, ref LexicalState state, ref int rawDelimiterQuotes)
    {
        var current = source[index];
        var next = index + 1 < source.Length ? source[index + 1] : '\0';
        if (state == LexicalState.LineComment)
        {
            if (current is '\r' or '\n') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.BlockComment)
        {
            if (current == '*' && next == '/') { index++; state = LexicalState.Code; }
            return true;
        }
        if (state == LexicalState.Character)
        {
            if (current == '\\') index++;
            else if (current == '\'') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.String)
        {
            if (current == '\\') index++;
            else if (current == '"') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.VerbatimString)
        {
            if (current == '"' && next == '"') index++;
            else if (current == '"') state = LexicalState.Code;
            return true;
        }
        if (state == LexicalState.RawString)
        {
            if (current == '"' && CountRun(source, index, '"') >= rawDelimiterQuotes)
            {
                index += rawDelimiterQuotes - 1;
                rawDelimiterQuotes = 0;
                state = LexicalState.Code;
            }
            return true;
        }
        if (current == '/' && next == '/') { index++; state = LexicalState.LineComment; return true; }
        if (current == '/' && next == '*') { index++; state = LexicalState.BlockComment; return true; }
        if (current == '\'') { state = LexicalState.Character; return true; }
        var dollarCount = CountRun(source, index, '$');
        var quoteStart = index + dollarCount;
        var quoteCount = quoteStart < source.Length ? CountRun(source, quoteStart, '"') : 0;
        if (quoteCount >= 3)
        {
            index = quoteStart + quoteCount - 1;
            rawDelimiterQuotes = quoteCount;
            state = LexicalState.RawString;
            return true;
        }
        if (current == '"') { state = LexicalState.String; return true; }
        if (current == '@' && next == '"') { index++; state = LexicalState.VerbatimString; return true; }
        if (current == '$' && next == '@' && index + 2 < source.Length && source[index + 2] == '"') { index += 2; state = LexicalState.VerbatimString; return true; }
        if (current == '@' && next == '$' && index + 2 < source.Length && source[index + 2] == '"') { index += 2; state = LexicalState.VerbatimString; return true; }
        if (current == '$' && next == '"') { index++; state = LexicalState.String; return true; }
        return false;
    }

    private static int CountRun(string source, int start, char character)
    {
        var count = 0;
        while (start + count < source.Length && source[start + count] == character)
            count++;
        return count;
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var start = 0;
        while ((start = source.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }
        return count;
    }

    private enum LexicalState { Code, LineComment, BlockComment, Character, String, VerbatimString, RawString }
}
