namespace PRISM_Utility.Core.Models;

[Flags]
public enum CalibrationCandidateStaleness
{
    None = 0,
    Channel = 1,
    Workspace = 2,
    Device = 4
}

public enum PendingCalibrationResultState
{
    Pending,
    Invalidated,
    Accept,
    AcceptAndSave,
    Restored,
    Cancelled,
    Failed
}

public enum PendingCalibrationResultIntent
{
    None,
    RestoreOriginal,
    AcceptCandidate,
    AcceptAndSaveCandidate
}

public enum ScanCalibrationCandidateValidationSeverity
{
    Warning,
    Error
}

public sealed record CalibrationCandidateContext
{
    public CalibrationCandidateContext(string selectedChannel, string workspaceIdentity, string deviceIdentity)
    {
        SelectedChannel = Normalize(selectedChannel, nameof(selectedChannel));
        WorkspaceIdentity = Normalize(workspaceIdentity, nameof(workspaceIdentity));
        DeviceIdentity = Normalize(deviceIdentity, nameof(deviceIdentity));
    }

    public string SelectedChannel { get; }

    public string WorkspaceIdentity { get; }

    public string DeviceIdentity { get; }

    public CalibrationCandidateStaleness GetStaleness(CalibrationCandidateContext currentContext)
    {
        ArgumentNullException.ThrowIfNull(currentContext);

        var staleness = CalibrationCandidateStaleness.None;
        if (!string.Equals(SelectedChannel, currentContext.SelectedChannel, StringComparison.Ordinal))
            staleness |= CalibrationCandidateStaleness.Channel;
        if (!string.Equals(WorkspaceIdentity, currentContext.WorkspaceIdentity, StringComparison.Ordinal))
            staleness |= CalibrationCandidateStaleness.Workspace;
        if (!string.Equals(DeviceIdentity, currentContext.DeviceIdentity, StringComparison.Ordinal))
            staleness |= CalibrationCandidateStaleness.Device;

        return staleness;
    }

    private static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim().Normalize(System.Text.NormalizationForm.FormKC).ToUpperInvariant();
    }
}

public sealed record ScanCalibrationMetrics
{
    public ScanCalibrationMetrics(
        decimal adcOutputDifferencePercent,
        decimal blackLevelDeviation,
        decimal saturatedPixelPercent,
        decimal noiseStandardDeviation)
    {
        if (adcOutputDifferencePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(adcOutputDifferencePercent));
        if (blackLevelDeviation < 0m)
            throw new ArgumentOutOfRangeException(nameof(blackLevelDeviation));
        if (saturatedPixelPercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(saturatedPixelPercent));
        if (noiseStandardDeviation < 0m)
            throw new ArgumentOutOfRangeException(nameof(noiseStandardDeviation));

        AdcOutputDifferencePercent = adcOutputDifferencePercent;
        BlackLevelDeviation = blackLevelDeviation;
        SaturatedPixelPercent = saturatedPixelPercent;
        NoiseStandardDeviation = noiseStandardDeviation;
    }

    public decimal AdcOutputDifferencePercent { get; }

    public decimal BlackLevelDeviation { get; }

    public decimal SaturatedPixelPercent { get; }

    public decimal NoiseStandardDeviation { get; }
}

public sealed record ScanCalibrationCandidateValidationIssue(
    ScanCalibrationCandidateValidationSeverity Severity,
    string Code,
    string Detail);

public sealed record ScanCalibrationCandidateValidation
{
    public static ScanCalibrationCandidateValidation Valid { get; } = new();

    public ScanCalibrationCandidateValidation(IEnumerable<ScanCalibrationCandidateValidationIssue>? issues = null)
    {
        Issues = Array.AsReadOnly((issues ?? [])
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Severity)
            .ThenBy(issue => issue.Detail, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<ScanCalibrationCandidateValidationIssue> Issues { get; }

    public bool IsValid => Issues.All(issue => issue.Severity != ScanCalibrationCandidateValidationSeverity.Error);
}

public sealed record ScanCalibrationCandidateFailure(string Code, string Detail);

public sealed record PendingCalibrationResult
{
    private PendingCalibrationResult(
        ScanParameterSnapshot originalSnapshot,
        ScanParameterSnapshot candidateSnapshot,
        ScanCalibrationMetrics beforeMetrics,
        ScanCalibrationMetrics afterMetrics,
        CalibrationCandidateContext context,
        ScanCalibrationCandidateValidation validation,
        ScanCalibrationRoiSettings roiSettings,
        ushort? blackLevel,
        ushort? whiteLevel,
        PendingCalibrationResultState state,
        PendingCalibrationResultIntent intent,
        CalibrationCandidateStaleness staleness,
        ScanCalibrationCandidateFailure? failure)
    {
        OriginalSnapshot = originalSnapshot;
        CandidateSnapshot = candidateSnapshot;
        BeforeMetrics = beforeMetrics;
        AfterMetrics = afterMetrics;
        Context = context;
        Validation = validation;
        RoiSettings = roiSettings;
        BlackLevel = blackLevel;
        WhiteLevel = whiteLevel;
        State = state;
        Intent = intent;
        Staleness = staleness;
        Failure = failure;
    }

    public ScanParameterSnapshot OriginalSnapshot { get; }

    public ScanParameterSnapshot CandidateSnapshot { get; }

    public ScanCalibrationMetrics BeforeMetrics { get; }

    public ScanCalibrationMetrics AfterMetrics { get; }

    public CalibrationCandidateContext Context { get; }

    public string SelectedChannel => Context.SelectedChannel;

    public ScanCalibrationCandidateValidation Validation { get; }

    public ScanCalibrationRoiSettings RoiSettings { get; }

    public ushort? BlackLevel { get; }

    public ushort? WhiteLevel { get; }

    public PendingCalibrationResultState State { get; }

    public PendingCalibrationResultIntent Intent { get; }

    public CalibrationCandidateStaleness Staleness { get; }

    public ScanCalibrationCandidateFailure? Failure { get; }

    public bool CanAccept => State == PendingCalibrationResultState.Pending && Validation.IsValid;

    public bool IsTerminal => State != PendingCalibrationResultState.Pending;

    public ScanParameterSnapshot? RequestedSnapshot => Intent switch
    {
        PendingCalibrationResultIntent.RestoreOriginal => OriginalSnapshot,
        PendingCalibrationResultIntent.AcceptCandidate or PendingCalibrationResultIntent.AcceptAndSaveCandidate => CandidateSnapshot,
        _ => null
    };

    public static PendingCalibrationResult Create(
        ScanParameterSnapshot originalSnapshot,
        ScanParameterSnapshot candidateSnapshot,
        ScanCalibrationMetrics beforeMetrics,
        ScanCalibrationMetrics afterMetrics,
        CalibrationCandidateContext context,
        ScanCalibrationCandidateValidation? validation = null,
        ScanCalibrationRoiSettings? roiSettings = null,
        ushort? blackLevel = null,
        ushort? whiteLevel = null)
    {
        ArgumentNullException.ThrowIfNull(originalSnapshot);
        ArgumentNullException.ThrowIfNull(candidateSnapshot);
        ArgumentNullException.ThrowIfNull(beforeMetrics);
        ArgumentNullException.ThrowIfNull(afterMetrics);
        ArgumentNullException.ThrowIfNull(context);

        return new(
            originalSnapshot,
            candidateSnapshot,
            beforeMetrics,
            afterMetrics,
            context,
            validation ?? ScanCalibrationCandidateValidation.Valid,
            (roiSettings ?? ScanCalibrationRoiSettings.CreateDefault()).Normalize(),
            blackLevel,
            whiteLevel,
            PendingCalibrationResultState.Pending,
            PendingCalibrationResultIntent.None,
            CalibrationCandidateStaleness.None,
            null);
    }

    public PendingCalibrationResult InvalidateIfStale(CalibrationCandidateContext currentContext)
    {
        EnsurePending();
        ArgumentNullException.ThrowIfNull(currentContext);

        var staleness = Context.GetStaleness(currentContext);
        return staleness == CalibrationCandidateStaleness.None
            ? this
            : Transition(
                PendingCalibrationResultState.Invalidated,
                PendingCalibrationResultIntent.None,
                staleness,
                null);
    }

    public PendingCalibrationResult Accept(CalibrationCandidateContext currentContext)
        => Request(
            currentContext,
            PendingCalibrationResultState.Accept,
            PendingCalibrationResultIntent.AcceptCandidate,
            requiresValidCandidate: true);

    public PendingCalibrationResult AcceptAndSave(CalibrationCandidateContext currentContext)
        => Request(
            currentContext,
            PendingCalibrationResultState.AcceptAndSave,
            PendingCalibrationResultIntent.AcceptAndSaveCandidate,
            requiresValidCandidate: true);

    public PendingCalibrationResult Restore(CalibrationCandidateContext currentContext)
        => Request(
            currentContext,
            PendingCalibrationResultState.Restored,
            PendingCalibrationResultIntent.RestoreOriginal,
            requiresValidCandidate: false);

    public PendingCalibrationResult Cancel()
    {
        EnsurePending();
        return Transition(
            PendingCalibrationResultState.Cancelled,
            PendingCalibrationResultIntent.None,
            CalibrationCandidateStaleness.None,
            null);
    }

    public PendingCalibrationResult Fail(ScanCalibrationCandidateFailure failure)
    {
        EnsurePending();
        ArgumentNullException.ThrowIfNull(failure);

        return Transition(
            PendingCalibrationResultState.Failed,
            PendingCalibrationResultIntent.None,
            CalibrationCandidateStaleness.None,
            failure);
    }

    private PendingCalibrationResult Request(
        CalibrationCandidateContext currentContext,
        PendingCalibrationResultState requestedState,
        PendingCalibrationResultIntent requestedIntent,
        bool requiresValidCandidate)
    {
        var current = InvalidateIfStale(currentContext);
        if (current.State == PendingCalibrationResultState.Invalidated)
            return current;
        if (requiresValidCandidate && !Validation.IsValid)
            throw new InvalidOperationException("An invalid calibration candidate cannot be accepted.");

        return Transition(
            requestedState,
            requestedIntent,
            CalibrationCandidateStaleness.None,
            null);
    }

    private PendingCalibrationResult Transition(
        PendingCalibrationResultState state,
        PendingCalibrationResultIntent intent,
        CalibrationCandidateStaleness staleness,
        ScanCalibrationCandidateFailure? failure)
        => new(
            OriginalSnapshot,
            CandidateSnapshot,
            BeforeMetrics,
            AfterMetrics,
            Context,
            Validation,
            RoiSettings,
            BlackLevel,
            WhiteLevel,
            state,
            intent,
            staleness,
            failure);

    private void EnsurePending()
    {
        if (State != PendingCalibrationResultState.Pending)
            throw new InvalidOperationException($"Cannot transition a calibration candidate from {State}.");
    }
}
