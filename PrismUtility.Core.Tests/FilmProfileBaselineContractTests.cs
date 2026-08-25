using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileBaselineContractTests
{
    [Fact]
    public async Task OpenLikeFileAndWorkspaceFlow_CancelStagesAndMalformedResultsAreExplicit()
    {
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        var initial = workspace.Snapshot.CurrentDraft;
        var canceled = await new ScanFilmProfileFileCoordinator(
            new FakeGateway(new ScanFilmProfileFileReadResult(true, null)),
            new ScanFilmProfileDocumentService()).ImportAsync(CancellationToken.None);
        var staged = workspace.StageImport(ReadFixture("full-v5.json"));
        var malformed = await new ScanFilmProfileFileCoordinator(
            new FakeGateway(new ScanFilmProfileFileReadResult(false, "not json")),
            new ScanFilmProfileDocumentService()).ImportAsync(CancellationToken.None);

        Assert.True(canceled.WasCanceled);
        Assert.True(workspace.Snapshot.CurrentDraft.HasSameContentAs(initial));
        Assert.True(staged.Staged);
        Assert.Equal(0, repository.ReplaceCount);
        Assert.NotNull(workspace.Snapshot.StagedImport);
        Assert.False(malformed.WasCanceled);
        Assert.Null(malformed.Profile);
        Assert.Contains(malformed.Validation!.Issues, issue => issue.Code == ScanFilmProfileValidationCode.MalformedJson);
    }

    private static string ReadFixture(string fileName)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName));

    private sealed class FakeGateway(ScanFilmProfileFileReadResult read) : IScanFilmProfileFileGateway
    {
        public Task<ScanFilmProfileFileReadResult> ReadJsonAsync() => Task.FromResult(read);

        public Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, string text)
            => Task.FromResult(new ScanFilmProfileFileWriteResult(false));
    }
}
