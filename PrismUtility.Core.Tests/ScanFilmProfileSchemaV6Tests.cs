using System.Text.Json;
using System.Text.Json.Nodes;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileSchemaV6Tests
{
    private static readonly ScanFilmProfileDocumentService Service = new();

    [Fact]
    public void Parse_FullV5Fixture_MigratesEveryLegacyFieldToV6Defaults()
    {
        var rawV5 = JsonSerializer.Deserialize<ScanFilmParameterProfileSet>(ReadFixture("full-v5.json"));
        var result = Service.Parse(ReadFixture("full-v5.json"));

        var source = Assert.IsType<ScanFilmParameterProfileSet>(rawV5);
        var migrated = Assert.IsType<ScanFilmParameterProfileSet>(result.Document);
        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        var expected = source with
        {
            SchemaVersion = 6,
            AcquisitionSettings = source.AcquisitionSettings! with
            {
                Rows = defaults.Rows,
                ScanMotorId = defaults.ScanMotorId,
                TargetLinePitchMicrometers = defaults.TargetLinePitchMicrometers,
                StartingDirectionPositive = defaults.StartingDirectionPositive,
                WarmUpEnabled = defaults.WarmUpEnabled,
                TransportStrategy = defaults.TransportStrategy,
                AcquisitionChannelAssignment = defaults.AcquisitionChannelAssignment
            }
        };

        Assert.True(result.CanApply);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(expected, migrated);
    }

    [Theory]
    [InlineData("minimal-v5.json", "Blue")]
    [InlineData("null-optionals-v5.json", "Red")]
    public void Parse_V5MissingOrNullOptionals_UsesDocumentedDefaults(string fixtureName, string selectedChannel)
    {
        var result = Service.Parse(ReadFixture(fixtureName));

        var document = Assert.IsType<ScanFilmParameterProfileSet>(result.Document);
        Assert.True(result.CanApply);
        Assert.Equal(6, document.SchemaVersion);
        Assert.Equal(selectedChannel, document.SelectedCalibrationChannel);
        Assert.Equal(ScanFilmAcquisitionSettings.CreateDefault(), document.AcquisitionSettings);
        Assert.Null(document.ScanRecipeSettings);
    }

    [Theory]
    [InlineData("minimal-v5.json")]
    [InlineData("null-optionals-v5.json")]
    public void Parse_V6MissingOrNullOptionals_UsesDocumentedDefaults(string fixtureName)
    {
        var json = ReadFixture(fixtureName).Replace("\"SchemaVersion\": 5", "\"SchemaVersion\": 6", StringComparison.Ordinal);

        var result = Service.Parse(json);

        var document = Assert.IsType<ScanFilmParameterProfileSet>(result.Document);
        Assert.True(result.CanApply);
        Assert.Equal(ScanFilmAcquisitionSettings.CreateDefault(), document.AcquisitionSettings);
        Assert.Null(document.ScanRecipeSettings);
    }

    [Fact]
    public void Parse_V6MissingAndNullAcquisitionMembers_UsesDocumentedDefaults()
    {
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFixture("full-v6.json")));
        var acquisitionJson = Assert.IsType<JsonObject>(root["AcquisitionSettings"]);
        acquisitionJson["Rows"] = null;
        acquisitionJson.Remove("ScanMotorId");
        acquisitionJson["TargetLinePitchMicrometers"] = null;
        acquisitionJson["StartingDirectionPositive"] = null;
        acquisitionJson["WarmUpEnabled"] = null;
        acquisitionJson["TransportStrategy"] = null;
        acquisitionJson["AcquisitionChannelAssignment"] = null;

        var result = Service.Parse(root.ToJsonString());

        var acquisition = Assert.IsType<ScanFilmAcquisitionSettings>(result.Document?.AcquisitionSettings);
        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        Assert.True(result.CanApply);
        Assert.Equal(defaults.Rows, acquisition.Rows);
        Assert.Equal(defaults.ScanMotorId, acquisition.ScanMotorId);
        Assert.Equal(defaults.TargetLinePitchMicrometers, acquisition.TargetLinePitchMicrometers);
        Assert.Equal(defaults.StartingDirectionPositive, acquisition.StartingDirectionPositive);
        Assert.Equal(defaults.WarmUpEnabled, acquisition.WarmUpEnabled);
        Assert.Equal(defaults.TransportStrategy, acquisition.TransportStrategy);
        Assert.Equal(new[] { "IR", "Blue", "White", "Red" }, acquisition.AcquisitionChannelAssignment?.Roles);
    }

    [Fact]
    public void Parse_V5IgnoresV6OnlyMembersAndUsesMigrationDefaults()
    {
        var json = ReadFixture("full-v5.json").Replace(
            "\"Led4ChannelColor\": \"Green\"",
            "\"Led4ChannelColor\": \"Green\", \"Rows\": { \"legacyExtension\": 999 }, \"ScanMotorId\": 2, \"TargetLinePitchMicrometers\": 77.0, \"StartingDirectionPositive\": false, \"WarmUpEnabled\": true, \"TransportStrategy\": 0",
            StringComparison.Ordinal);

        var acquisition = Assert.IsType<ScanFilmAcquisitionSettings>(Service.Parse(json).Document?.AcquisitionSettings);

        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        Assert.Equal(defaults.Rows, acquisition.Rows);
        Assert.Equal(defaults.ScanMotorId, acquisition.ScanMotorId);
        Assert.Equal(defaults.TargetLinePitchMicrometers, acquisition.TargetLinePitchMicrometers);
        Assert.Equal(defaults.StartingDirectionPositive, acquisition.StartingDirectionPositive);
        Assert.Equal(defaults.WarmUpEnabled, acquisition.WarmUpEnabled);
        Assert.Equal(defaults.TransportStrategy, acquisition.TransportStrategy);
        Assert.Equal(defaults.AcquisitionChannelAssignment, acquisition.AcquisitionChannelAssignment);
    }

    [Fact]
    public void Parse_FullV6Fixture_PreservesEveryFieldAndRoundTripsDeterministically()
    {
        var result = Service.Parse(ReadFixture("full-v6.json"));
        var document = Assert.IsType<ScanFilmParameterProfileSet>(result.Document);
        var acquisition = Assert.IsType<ScanFilmAcquisitionSettings>(document.AcquisitionSettings);

        Assert.True(result.CanApply);
        Assert.Equal(6, document.SchemaVersion);
        Assert.Equal(256, acquisition.Rows);
        Assert.Equal((byte)2, acquisition.ScanMotorId);
        Assert.Equal(12.5, acquisition.TargetLinePitchMicrometers);
        Assert.False(acquisition.StartingDirectionPositive);
        Assert.True(acquisition.WarmUpEnabled);
        Assert.Equal(ScanFilmTransportStrategy.ReturnToStart, acquisition.TransportStrategy);
        Assert.Equal(new[] { "IR", "Blue", "Unused", "Red" }, acquisition.AcquisitionChannelAssignment?.Roles);

        var first = Service.Serialize(document);
        var second = Service.Serialize(document);
        var reparsed = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(first).Document);
        Assert.Equal(first, second);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(document, reparsed);
    }

    [Fact]
    public void Todo10_Baseline_FullV6DocumentModelAndDraftRoundTripPreserveAcquisitionOwnership()
    {
        var document = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(ReadFixture("full-v6.json")).Document);
        var draft = ScanFilmProfileDraft.FromDocument(document).Draft;
        var rebuilt = Assert.IsType<ScanFilmParameterProfileSet>(Service.Build(draft).Document);
        var reparsed = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(Service.Serialize(document)).Document);

        FilmProfileRoundTripAssertions.EqualCompleteDocument(document, rebuilt);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(document, reparsed);
        Assert.Equal(document.AcquisitionSettings, rebuilt.AcquisitionSettings);
        Assert.Equal(document.AcquisitionSettings, reparsed.AcquisitionSettings);
    }

    [Fact]
    public void BuildAndSerialize_EmitV6InDeterministicPropertyOrder()
    {
        var source = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(ReadFixture("full-v6.json")).Document);
        var draft = ScanFilmProfileDraft.FromDocument(source).Draft;

        var built = Service.Build(draft);
        var document = Assert.IsType<ScanFilmParameterProfileSet>(built.Document);
        var json = Service.Serialize(document);
        var reparsed = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(json).Document);

        Assert.True(built.CanApply);
        Assert.Equal(6, document.SchemaVersion);
        Assert.Equal(6, reparsed.SchemaVersion);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(document, reparsed);
        using var parsedJson = JsonDocument.Parse(json);
        Assert.Equal(
            new[] { "SchemaVersion", "ProfileName", "SavedAtUtc", "ChannelProfiles", "SelectedCalibrationChannel", "AcquisitionSettings", "ScanRecipeSettings" },
            parsedJson.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            new[]
            {
                "Led1Level", "Led2Level", "Led3Level", "Led4Level", "SteadyMask", "SyncMask",
                "Led1PulseClock", "Led2PulseClock", "Led3PulseClock", "Led4PulseClock", "MotorIntervalNs",
                "Led1ChannelColor", "Led2ChannelColor", "Led3ChannelColor", "Led4ChannelColor", "Rows",
                "ScanMotorId", "TargetLinePitchMicrometers", "StartingDirectionPositive", "WarmUpEnabled",
                "TransportStrategy", "AcquisitionChannelAssignment"
            },
            parsedJson.RootElement.GetProperty("AcquisitionSettings").EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public void Serialize_CurrentV6Document_DoesNotEmitComputedChannelAssignmentArrays()
    {
        var source = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(ReadFixture("full-v6.json")).Document);

        var json = Service.Serialize(source);
        using var parsedJson = JsonDocument.Parse(json);
        var acquisitionAssignment = parsedJson.RootElement.GetProperty("AcquisitionSettings").GetProperty("AcquisitionChannelAssignment");
        var recipeAssignment = parsedJson.RootElement.GetProperty("ScanRecipeSettings").GetProperty("ChannelAssignment");

        Assert.Equal(
            new[] { "Channel1Role", "Channel2Role", "Channel3Role", "Channel4Role", "Channel1Reversed", "Channel2Reversed", "Channel3Reversed", "Channel4Reversed" },
            acquisitionAssignment.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            new[] { "Channel1Role", "Channel2Role", "Channel3Role", "Channel4Role", "Channel1Reversed", "Channel2Reversed", "Channel3Reversed", "Channel4Reversed" },
            recipeAssignment.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("Roles", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ReversedFlags", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_TypedV5Document_EmitsMigratedV6()
    {
        var source = JsonSerializer.Deserialize<ScanFilmParameterProfileSet>(ReadFixture("full-v5.json"));

        var json = Service.Serialize(Assert.IsType<ScanFilmParameterProfileSet>(source));
        var reparsed = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(json).Document);

        Assert.Equal(6, reparsed.SchemaVersion);
        Assert.Equal(ScanFilmAcquisitionSettings.CreateDefault().Rows, reparsed.AcquisitionSettings?.Rows);
    }

    [Theory]
    [InlineData("future-v7.json", "7")]
    [InlineData(null, "4")]
    public void Parse_UnsupportedSchema_PreservesActualAndSupportedVersionDetail(string? fixtureName, string actual)
    {
        var json = fixtureName is null
            ? "{\"SchemaVersion\":4}"
            : ReadFixture(fixtureName);

        var result = Service.Parse(json);

        Assert.Null(result.Document);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.UnsupportedSchemaVersion, issue.Code);
        Assert.Equal("schemaVersion", issue.FieldPath);
        Assert.Equal(new[] { actual, "5", "6" }, issue.MessageArguments);
    }

    [Theory]
    [InlineData("", ScanFilmProfileValidationCode.MalformedJson, "document")]
    [InlineData("not json", ScanFilmProfileValidationCode.MalformedJson, "document")]
    [InlineData("{}", ScanFilmProfileValidationCode.MissingRequiredProperty, "schemaVersion")]
    [InlineData("{\"SchemaVersion\":null}", ScanFilmProfileValidationCode.MissingRequiredProperty, "schemaVersion")]
    [InlineData("{\"SchemaVersion\":\"6\"}", ScanFilmProfileValidationCode.MissingRequiredProperty, "schemaVersion")]
    public void Parse_MalformedOrInvalidSchema_UsesCanonicalPath(string json, ScanFilmProfileValidationCode code, string fieldPath)
    {
        var result = Service.Parse(json);

        Assert.Null(result.Document);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(code, issue.Code);
        Assert.Equal(fieldPath, issue.FieldPath);
    }

    [Fact]
    public void Parse_InvalidV6Acquisition_ReportsEveryTypedFieldPath()
    {
        var result = Service.Parse(ReadFixture("invalid-v6-acquisition.json"));

        Assert.NotNull(result.Document);
        Assert.False(result.CanApply);
        Assert.All(result.Validation.Issues, issue => Assert.Equal(ScanFilmProfileValidationCode.InvalidAcquisitionInput, issue.Code));
        Assert.Equal(
            new[]
            {
                "acquisitionSettings.acquisitionChannelAssignment",
                "acquisitionSettings.rows",
                "acquisitionSettings.scanMotorId",
                "acquisitionSettings.targetLinePitchMicrometers",
                "acquisitionSettings.transportStrategy"
            },
            result.Validation.Issues.Select(issue => issue.FieldPath));
    }

    public static IEnumerable<object[]> InvalidHostRows()
    {
        yield return [0];
        yield return [-1];
        yield return [282_459];
        yield return [checked(ScanRowCountValidation.MaxHostRows + 1)];
        yield return [int.MaxValue];
    }

    [Theory]
    [MemberData(nameof(InvalidHostRows))]
    public void ParseAndBuild_V6RowsOutsideHostBufferBoundary_ReportCanonicalAcquisitionPath(int rows)
    {
        var parsed = Service.Parse(MutateV6Acquisition(acquisition => acquisition["Rows"] = rows));

        Assert.NotNull(parsed.Document);
        Assert.False(parsed.CanApply);
        var parseIssue = Assert.Single(parsed.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.InvalidAcquisitionInput, parseIssue.Code);
        Assert.Equal("acquisitionSettings.rows", parseIssue.FieldPath);

        var validDocument = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(ReadFixture("full-v6.json")).Document);
        var sourceDraft = ScanFilmProfileDraft.FromDocument(validDocument).Draft;
        var draft = new ScanFilmProfileDraft(
            sourceDraft.ProfileName,
            sourceDraft.SavedAtUtc,
            sourceDraft.ChannelProfiles,
            sourceDraft.SelectedCalibrationChannel,
            validDocument.AcquisitionSettings! with { Rows = rows },
            sourceDraft.ScanRecipeSettings);
        var built = Service.Build(draft);

        Assert.Null(built.Document);
        Assert.False(built.CanApply);
        var buildIssue = Assert.Single(built.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.InvalidAcquisitionInput, buildIssue.Code);
        Assert.Equal("acquisitionSettings.rows", buildIssue.FieldPath);
    }

    [Fact]
    public void Parse_MalformedNestedV6Type_ReportsNewtonsoftCanonicalPath()
    {
        var result = Service.Parse(ReadFixture("malformed-v6-acquisition-type.json"));

        Assert.Null(result.Document);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, issue.Code);
        Assert.Equal("acquisitionSettings.rows", issue.FieldPath);
    }

    [Theory]
    [InlineData("Rows", "acquisitionSettings.rows")]
    [InlineData("ScanMotorId", "acquisitionSettings.scanMotorId")]
    [InlineData("TargetLinePitchMicrometers", "acquisitionSettings.targetLinePitchMicrometers")]
    [InlineData("StartingDirectionPositive", "acquisitionSettings.startingDirectionPositive")]
    [InlineData("WarmUpEnabled", "acquisitionSettings.warmUpEnabled")]
    [InlineData("TransportStrategy", "acquisitionSettings.transportStrategy")]
    [InlineData("AcquisitionChannelAssignment", "acquisitionSettings.acquisitionChannelAssignment")]
    [InlineData("transportStrategy", "acquisitionSettings.transportStrategy")]
    [InlineData("acquisitionChannelAssignment", "acquisitionSettings.acquisitionChannelAssignment")]
    public void Parse_ExplicitMalformedV6AcquisitionMember_RejectsAtCanonicalMemberPath(
        string memberName,
        string expectedPath)
    {
        var json = MutateV6Acquisition(
            acquisition => SetAcquisitionMember(acquisition, memberName, new JsonObject { ["unexpected"] = 1 }));

        var result = Service.Parse(json);

        Assert.Null(result.Document);
        Assert.False(result.CanApply);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, issue.Code);
        Assert.Equal(expectedPath, issue.FieldPath);
    }

    [Theory]
    [InlineData("Rows", "acquisitionSettings.rows")]
    [InlineData("ScanMotorId", "acquisitionSettings.scanMotorId")]
    [InlineData("TargetLinePitchMicrometers", "acquisitionSettings.targetLinePitchMicrometers")]
    [InlineData("StartingDirectionPositive", "acquisitionSettings.startingDirectionPositive")]
    [InlineData("WarmUpEnabled", "acquisitionSettings.warmUpEnabled")]
    [InlineData("TransportStrategy", "acquisitionSettings.transportStrategy")]
    [InlineData("AcquisitionChannelAssignment", "acquisitionSettings.acquisitionChannelAssignment")]
    [InlineData("transportStrategy", "acquisitionSettings.transportStrategy")]
    [InlineData("acquisitionChannelAssignment", "acquisitionSettings.acquisitionChannelAssignment")]
    public void Workspace_ExplicitMalformedV6AcquisitionMember_PreservesEveryStateDomain(
        string memberName,
        string expectedPath)
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new ScanFilmProfileWorkspace(repository, Service);
        Assert.True(workspace.StageImport(ReadFixture("full-v6.json")).Staged);
        var staged = workspace.Snapshot;
        workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            "Dirty current draft",
            staged.CurrentDraft.SavedAtUtc,
            staged.CurrentDraft.ChannelProfiles,
            staged.CurrentDraft.SelectedCalibrationChannel,
            staged.CurrentDraft.AcquisitionSettings,
            staged.CurrentDraft.ScanRecipeSettings));
        var before = workspace.Snapshot;
        var json = MutateV6Acquisition(
            acquisition => SetAcquisitionMember(acquisition, memberName, new JsonObject { ["unexpected"] = 1 }));

        var failed = workspace.StageImport(json);
        var after = workspace.Snapshot;

        Assert.False(failed.Staged);
        Assert.Same(before.CurrentDraft, after.CurrentDraft);
        Assert.Same(before.BaselineDraft, after.BaselineDraft);
        Assert.Same(before.StagedImport, after.StagedImport);
        Assert.Same(before.StagedImport?.Validation, after.StagedImport?.Validation);
        Assert.True(before.IsDirty);
        Assert.Equal(before.IsDirty, after.IsDirty);
        Assert.Equal(ScanFilmProfileImportResultState.Error, after.ImportResult.State);
        Assert.Same(failed.Validation, after.ImportResult.Validation);
        var issue = Assert.Single(failed.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, issue.Code);
        Assert.Equal(expectedPath, issue.FieldPath);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Theory]
    [InlineData("Rows")]
    [InlineData("ScanMotorId")]
    [InlineData("TargetLinePitchMicrometers")]
    [InlineData("StartingDirectionPositive")]
    [InlineData("WarmUpEnabled")]
    [InlineData("TransportStrategy")]
    [InlineData("AcquisitionChannelAssignment")]
    public void Parse_MissingOrNullV6AcquisitionMember_RetainsDocumentedDefault(string memberName)
    {
        var source = Assert.IsType<ScanFilmAcquisitionSettings>(
            Service.Parse(ReadFixture("full-v6.json")).Document?.AcquisitionSettings);
        var expected = WithDocumentedDefault(source, memberName);
        var missing = Service.Parse(MutateV6Acquisition(acquisition => acquisition.Remove(memberName)));
        var explicitNull = Service.Parse(MutateV6Acquisition(acquisition => acquisition[memberName] = null));

        Assert.True(missing.CanApply);
        Assert.True(explicitNull.CanApply);
        Assert.Equal(expected, missing.Document?.AcquisitionSettings);
        Assert.Equal(expected, explicitNull.Document?.AcquisitionSettings);
    }

    [Fact]
    public void Parse_UnknownMembersOutsideKnownMalformedShapes_RemainIgnoredAndOmitted()
    {
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFixture("full-v6.json")));
        root["UnknownTop"] = new JsonObject { ["unexpected"] = 1 };
        var acquisition = Assert.IsType<JsonObject>(root["AcquisitionSettings"]);
        acquisition["UnknownAcquisition"] = new JsonObject { ["unexpected"] = 1 };

        var result = Service.Parse(root.ToJsonString());
        var serialized = Service.Serialize(Assert.IsType<ScanFilmParameterProfileSet>(result.Document));

        Assert.True(result.CanApply);
        Assert.DoesNotContain("UnknownTop", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnknownAcquisition", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ValidLowerCamelV6AcquisitionMembers_PreservesEverySuppliedValue()
    {
        var json = MutateV6Acquisition(acquisition =>
        {
            SetAcquisitionMember(acquisition, "rows", 333);
            SetAcquisitionMember(acquisition, "scanMotorId", 2);
            SetAcquisitionMember(acquisition, "targetLinePitchMicrometers", 7.25);
            SetAcquisitionMember(acquisition, "startingDirectionPositive", false);
            SetAcquisitionMember(acquisition, "warmUpEnabled", true);
            SetAcquisitionMember(acquisition, "transportStrategy", 0);
            SetAcquisitionMember(
                acquisition,
                "acquisitionChannelAssignment",
                CreateAssignmentJson("IR", "Blue", "Unused", "Red", true, false, false, true));
        });

        var result = Service.Parse(json);

        var settings = Assert.IsType<ScanFilmAcquisitionSettings>(result.Document?.AcquisitionSettings);
        Assert.True(result.CanApply);
        Assert.Equal(333, settings.Rows);
        Assert.Equal((byte)2, settings.ScanMotorId);
        Assert.Equal(7.25, settings.TargetLinePitchMicrometers);
        Assert.False(settings.StartingDirectionPositive);
        Assert.True(settings.WarmUpEnabled);
        Assert.Equal(ScanFilmTransportStrategy.ReturnToStart, settings.TransportStrategy);
        Assert.Equal(new[] { "IR", "Blue", "Unused", "Red" }, settings.AcquisitionChannelAssignment?.Roles);
        Assert.Equal(new[] { true, false, false, true }, settings.AcquisitionChannelAssignment?.ReversedFlags);
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("scanMotorId")]
    [InlineData("targetLinePitchMicrometers")]
    [InlineData("startingDirectionPositive")]
    [InlineData("warmUpEnabled")]
    [InlineData("transportStrategy")]
    [InlineData("acquisitionChannelAssignment")]
    public void Parse_V5MalformedLowerCamelV6Extension_IsStrippedBeforeMigration(string lowerCamelName)
    {
        var expected = Service.Parse(ReadFixture("full-v5.json"));
        var json = MutateAcquisition(
            "full-v5.json",
            acquisition => acquisition[lowerCamelName] = new JsonObject { ["unexpected"] = 1 });

        var result = Service.Parse(json);

        Assert.True(result.CanApply);
        Assert.Equal(expected.Document?.AcquisitionSettings, result.Document?.AcquisitionSettings);
    }

    [Theory]
    [InlineData("Rows", "rows", "acquisitionSettings.rows")]
    [InlineData("ScanMotorId", "scanMotorId", "acquisitionSettings.scanMotorId")]
    [InlineData("TargetLinePitchMicrometers", "targetLinePitchMicrometers", "acquisitionSettings.targetLinePitchMicrometers")]
    [InlineData("StartingDirectionPositive", "startingDirectionPositive", "acquisitionSettings.startingDirectionPositive")]
    [InlineData("WarmUpEnabled", "warmUpEnabled", "acquisitionSettings.warmUpEnabled")]
    [InlineData("TransportStrategy", "transportStrategy", "acquisitionSettings.transportStrategy")]
    [InlineData("AcquisitionChannelAssignment", "acquisitionChannelAssignment", "acquisitionSettings.acquisitionChannelAssignment")]
    public void Parse_CaseCollidingV6AcquisitionMembers_RejectAtCanonicalMemberPath(
        string memberName,
        string lowerCamelName,
        string expectedPath)
    {
        var json = MutateV6Acquisition(
            acquisition => acquisition[lowerCamelName] = CreateAcquisitionCollisionValue(memberName));

        var result = Service.Parse(json);

        AssertMalformedAt(result, expectedPath);
    }

    [Theory]
    [InlineData("Channel1Role", "channel1Role")]
    [InlineData("Channel2Role", "channel2Role")]
    [InlineData("Channel3Role", "channel3Role")]
    [InlineData("Channel4Role", "channel4Role")]
    [InlineData("Channel1Reversed", "channel1Reversed")]
    [InlineData("Channel2Reversed", "channel2Reversed")]
    [InlineData("Channel3Reversed", "channel3Reversed")]
    [InlineData("Channel4Reversed", "channel4Reversed")]
    public void Parse_CaseCollidingAssignmentFields_RejectAtAssignmentPath(
        string fieldName,
        string lowerCamelName)
    {
        var json = MutateV6Acquisition(acquisition =>
        {
            var assignment = Assert.IsType<JsonObject>(acquisition["AcquisitionChannelAssignment"]);
            assignment[lowerCamelName] = CreateAssignmentCollisionValue(fieldName);
        });

        var result = Service.Parse(json);

        AssertMalformedAt(result, "acquisitionSettings.acquisitionChannelAssignment");
    }

    [Fact]
    public void Workspace_CaseCollidingAcquisitionMember_PreservesEveryStateDomain()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new ScanFilmProfileWorkspace(repository, Service);
        Assert.True(workspace.StageImport(ReadFixture("full-v6.json")).Staged);
        var staged = workspace.Snapshot;
        workspace.SetCurrentDraft(new ScanFilmProfileDraft(
            "Dirty current draft",
            staged.CurrentDraft.SavedAtUtc,
            staged.CurrentDraft.ChannelProfiles,
            staged.CurrentDraft.SelectedCalibrationChannel,
            staged.CurrentDraft.AcquisitionSettings,
            staged.CurrentDraft.ScanRecipeSettings));
        var before = workspace.Snapshot;
        var json = MutateV6Acquisition(acquisition => acquisition["rows"] = 777);

        var failed = workspace.StageImport(json);
        var after = workspace.Snapshot;

        Assert.False(failed.Staged);
        Assert.Same(before.CurrentDraft, after.CurrentDraft);
        Assert.Same(before.BaselineDraft, after.BaselineDraft);
        Assert.Same(before.StagedImport, after.StagedImport);
        Assert.Same(before.StagedImport?.Validation, after.StagedImport?.Validation);
        Assert.True(before.IsDirty);
        Assert.True(after.IsDirty);
        Assert.Equal(ScanFilmProfileImportResultState.Error, after.ImportResult.State);
        Assert.Same(failed.Validation, after.ImportResult.Validation);
        AssertMalformedAt(new ScanFilmProfileDocumentParseResult(null, failed.Validation), "acquisitionSettings.rows");
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public void Serialize_V6_ExcludesPreviewOnlyStateButKeepsRecipeOutputGamma()
    {
        var document = Assert.IsType<ScanFilmParameterProfileSet>(Service.Parse(ReadFixture("full-v6.json")).Document);

        var json = Service.Serialize(document);

        Assert.Contains("\"OutputGamma\"", json, StringComparison.Ordinal);
        foreach (var previewToken in new[]
                 {
                     "IsPreviewEnabled", "IsWaterfallEnabled", "IsWaterfallCompressedEnabled",
                     "IsGammaCorrectionEnabled", "PreviewGamma", "IsWhiteLevelPreviewEnabled",
                     "ZoomScale", "ColumnSampleOverlayVersion", "RoiOverlayVersion",
                     "IsBwActiveRoiOverlayVisible", "IsBwShieldRoiOverlayVisible",
                     "IsFocusOverallRoiOverlayVisible", "IsFocusLeftRoiOverlayVisible", "IsFocusRightRoiOverlayVisible"
                 })
        {
            Assert.DoesNotContain($"\"{previewToken}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ViewModel_Todo10Synchronization_CarriesImportedV6MembersThroughUiState()
    {
        var source = FilmProfileContractSource.ReadAppText("ViewModels", "ScanDebugViewModel.cs");
        var build = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildFilmAcquisitionSettings");
        var apply = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyProfileAcquisitionSettings");

        Assert.Contains("TryParseRequestedRows(out var rows)", build, StringComparison.Ordinal);
        Assert.Contains("rows,", build, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var motorId", build, StringComparison.Ordinal);
        Assert.Contains("BuildDebugChannelAssignment()", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Rows", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.ScanMotorId", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TargetLinePitchMicrometers", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.StartingDirectionPositive", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.WarmUpEnabled", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TransportStrategy", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.AcquisitionChannelAssignment", build, StringComparison.Ordinal);

        Assert.Contains("SelectedRows = normalized.Rows.ToString(CultureInfo.InvariantCulture);", apply, StringComparison.Ordinal);
        Assert.Contains("SelectedScanMotor = MotorOptions[normalized.ScanMotorId];", apply, StringComparison.Ordinal);
        Assert.Contains("SelectedStartingDirection = normalized.StartingDirectionPositive ? ForwardDirection : ReverseDirection;", apply, StringComparison.Ordinal);
        Assert.Contains("IsWarmUpEnabled = normalized.WarmUpEnabled;", apply, StringComparison.Ordinal);
        Assert.Contains("IsAlternateMotorDirectionEnabled = normalized.TransportStrategy == ScanFilmTransportStrategy.AlternateDirection;", apply, StringComparison.Ordinal);
        Assert.Contains("MotorDistancePerLineValue = targetLinePitchDisplayValue;", apply, StringComparison.Ordinal);
        Assert.Contains("NotifyAcquisitionPlanChanged();", apply, StringComparison.Ordinal);
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string MutateV6Acquisition(Action<JsonObject> mutation)
        => MutateAcquisition("full-v6.json", mutation);

    private static string MutateAcquisition(string fixtureName, Action<JsonObject> mutation)
    {
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFixture(fixtureName)));
        var acquisition = Assert.IsType<JsonObject>(root["AcquisitionSettings"]);
        mutation(acquisition);
        return root.ToJsonString();
    }

    private static void SetAcquisitionMember(JsonObject acquisition, string memberName, JsonNode? value)
    {
        var canonicalName = $"{char.ToUpperInvariant(memberName[0])}{memberName[1..]}";
        acquisition.Remove(canonicalName);
        acquisition[memberName] = value;
    }

    private static void AssertMalformedAt(ScanFilmProfileDocumentParseResult result, string expectedPath)
    {
        Assert.Null(result.Document);
        Assert.False(result.CanApply);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.MalformedJson, issue.Code);
        Assert.Equal(expectedPath, issue.FieldPath);
    }

    private static JsonNode CreateAcquisitionCollisionValue(string memberName)
        => memberName switch
        {
            "Rows" => JsonValue.Create(777)!,
            "ScanMotorId" => JsonValue.Create(1)!,
            "TargetLinePitchMicrometers" => JsonValue.Create(8.75)!,
            "StartingDirectionPositive" => JsonValue.Create(true)!,
            "WarmUpEnabled" => JsonValue.Create(false)!,
            "TransportStrategy" => JsonValue.Create(1)!,
            "AcquisitionChannelAssignment" => CreateAssignmentJson("Blue", "Red", "Green", "White", false, true, true, false),
            _ => throw new ArgumentOutOfRangeException(nameof(memberName), memberName, null)
        };

    private static JsonNode CreateAssignmentCollisionValue(string fieldName)
        => fieldName switch
        {
            "Channel1Role" => JsonValue.Create("Blue")!,
            "Channel2Role" => JsonValue.Create("Red")!,
            "Channel3Role" => JsonValue.Create("Green")!,
            "Channel4Role" => JsonValue.Create("White")!,
            "Channel1Reversed" => JsonValue.Create(false)!,
            "Channel2Reversed" => JsonValue.Create(true)!,
            "Channel3Reversed" => JsonValue.Create(true)!,
            "Channel4Reversed" => JsonValue.Create(false)!,
            _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null)
        };

    private static JsonObject CreateAssignmentJson(
        string role1,
        string role2,
        string role3,
        string role4,
        bool reversed1,
        bool reversed2,
        bool reversed3,
        bool reversed4)
        => new()
        {
            ["Channel1Role"] = role1,
            ["Channel2Role"] = role2,
            ["Channel3Role"] = role3,
            ["Channel4Role"] = role4,
            ["Channel1Reversed"] = reversed1,
            ["Channel2Reversed"] = reversed2,
            ["Channel3Reversed"] = reversed3,
            ["Channel4Reversed"] = reversed4
        };

    private static ScanFilmAcquisitionSettings WithDocumentedDefault(
        ScanFilmAcquisitionSettings source,
        string memberName)
    {
        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        return memberName switch
        {
            "Rows" => source with { Rows = defaults.Rows },
            "ScanMotorId" => source with { ScanMotorId = defaults.ScanMotorId },
            "TargetLinePitchMicrometers" => source with { TargetLinePitchMicrometers = defaults.TargetLinePitchMicrometers },
            "StartingDirectionPositive" => source with { StartingDirectionPositive = defaults.StartingDirectionPositive },
            "WarmUpEnabled" => source with { WarmUpEnabled = defaults.WarmUpEnabled },
            "TransportStrategy" => source with { TransportStrategy = defaults.TransportStrategy },
            "AcquisitionChannelAssignment" => source with
            {
                AcquisitionChannelAssignment = new ScanChannelAssignment(
                    source.Led1ChannelColor,
                    source.Led2ChannelColor,
                    source.Led3ChannelColor,
                    source.Led4ChannelColor,
                    false,
                    false,
                    false,
                    false)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(memberName), memberName, null)
        };
    }
}
