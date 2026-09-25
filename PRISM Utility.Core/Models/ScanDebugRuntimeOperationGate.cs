namespace PRISM_Utility.Core.Models;

public enum ScanDebugRuntimeOperation
{
    None,
    Connection,
    Scan,
    OutputCapture,
    ParameterApplication,
    AutoCalibration,
    AutoFocus,
    ManualFocus,
    Motor,
    Illumination,
    ProfileLifecycle,
    ProfileExport,
    ProfileImport,
    CalibrationRepository,
    DeviceGlobal,
    RawHardware
}

public enum ScanDebugRuntimeCommandKind
{
    ConnectDevices,
    DisconnectDevices,
    StartScan,
    StopScan,
    ExportDng,
    ApplyDeviceClock,
    ApplyParameters,
    RefreshIllumination,
    ApplyIllumination,
    RefreshMotion,
    EnableMotor,
    DisableMotor,
    MoveMotor,
    StopMotor,
    ApplyMotorConfig,
    AutoBlackAdjust,
    AutoWhiteAdjust,
    AutoCalibrate,
    AutoFocus,
    StartManualFocus,
    StopManualFocus,
    StopAllFocus,
    StopAllMotors,
    UpdateFocusMapping,
    SaveChannelProfile,
    ClearChannelProfile,
    SaveColumnSampleAsBlackLevel,
    SaveColumnSampleAsWhiteLevel,
    SaveFilmProfileJson,
    LoadFilmProfileJson,
    ApplyStagedFilmProfileImport,
    DiscardStagedFilmProfileImport,
    NewFilmProfile,
    ValidateFilmProfile,
    ResetSelectedRoi,
    ApplySelectedRoiInputs,
    ResetAllRois,
    DeviceGlobalWrite,
    CalibrationLibraryReplace,
    RawHardwareCommand,
    PreviewOnlyEdit
    ,RestoreCalibrationCandidate
    ,AcceptCalibrationCandidate
    ,AcceptAndSaveCalibrationCandidate
    ,ApplyManualReferenceLevels
}

public enum ScanDebugRuntimeOperationGateReason
{
    Allowed,
    DeviceDisconnected,
    RuntimeOperationActive
}

public sealed record ScanDebugRuntimeOperationSnapshot
{
    public ScanDebugRuntimeOperationSnapshot(bool isDeviceConnected, IEnumerable<ScanDebugRuntimeOperation>? activeOperations = null)
    {
        IsDeviceConnected = isDeviceConnected;
        ActiveOperations = (activeOperations ?? Array.Empty<ScanDebugRuntimeOperation>())
            .Where(operation => operation != ScanDebugRuntimeOperation.None)
            .ToHashSet();
    }

    public bool IsDeviceConnected { get; }

    public IReadOnlySet<ScanDebugRuntimeOperation> ActiveOperations { get; }

    public bool HasActiveOperation => ActiveOperations.Count > 0;
}

public readonly record struct ScanDebugRuntimeOperationGateResult(
    bool CanExecute,
    ScanDebugRuntimeOperationGateReason ReasonCode);

public sealed record ScanDebugRuntimeCommandPolicy(
    bool RequiresDeviceConnection,
    ScanDebugRuntimeOperation ClaimedOperation,
    IReadOnlySet<ScanDebugRuntimeOperation> ConflictingActiveOperations);

public static class ScanDebugRuntimeOperationGate
{
    public static ScanDebugRuntimeOperationGateResult Evaluate(
        ScanDebugRuntimeOperationSnapshot snapshot,
        ScanDebugRuntimeCommandKind command)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var policy = GetPolicy(command);
        if (policy.RequiresDeviceConnection && !snapshot.IsDeviceConnected)
            return new(false, ScanDebugRuntimeOperationGateReason.DeviceDisconnected);

        return snapshot.ActiveOperations.Any(policy.ConflictingActiveOperations.Contains)
            ? new(false, ScanDebugRuntimeOperationGateReason.RuntimeOperationActive)
            : new(true, ScanDebugRuntimeOperationGateReason.Allowed);
    }

    public static bool RequiresDeviceConnection(ScanDebugRuntimeCommandKind command)
        => GetPolicy(command).RequiresDeviceConnection;

    public static bool IsStopOrCancellation(ScanDebugRuntimeCommandKind command)
        => command is ScanDebugRuntimeCommandKind.DisconnectDevices
            or ScanDebugRuntimeCommandKind.StopScan
            or ScanDebugRuntimeCommandKind.StopMotor
            or ScanDebugRuntimeCommandKind.StopManualFocus
            or ScanDebugRuntimeCommandKind.StopAllFocus
            or ScanDebugRuntimeCommandKind.StopAllMotors;

    public static ScanDebugRuntimeOperation ClaimedOperationFor(ScanDebugRuntimeCommandKind command)
        => GetPolicy(command).ClaimedOperation;

    public static bool IsDeviceBoundOperation(ScanDebugRuntimeOperation operation)
        => Enum.GetValues<ScanDebugRuntimeCommandKind>()
            .Any(command => RequiresDeviceConnection(command) && ClaimedOperationFor(command) == operation);

    public static ScanDebugRuntimeOperation ProjectSessionOperation(ScannerDeviceSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State != ScannerSessionState.Running || snapshot.ActiveOwner is null)
            return ScanDebugRuntimeOperation.None;

        return snapshot.ActiveOwner.Operation switch
        {
            ScannerSessionOperation.None => ScanDebugRuntimeOperation.None,
            ScannerSessionOperation.Connect or
            ScannerSessionOperation.Disconnect or
            ScannerSessionOperation.Reconnect or
            ScannerSessionOperation.Shutdown => ScanDebugRuntimeOperation.Connection,
            ScannerSessionOperation.Scan => ScanDebugRuntimeOperation.Scan,
            ScannerSessionOperation.Calibration => ScanDebugRuntimeOperation.AutoCalibration,
            ScannerSessionOperation.AutoFocus => ScanDebugRuntimeOperation.AutoFocus,
            ScannerSessionOperation.WarmUp => ScanDebugRuntimeOperation.DeviceGlobal,
            ScannerSessionOperation.Diagnostics => ScanDebugRuntimeOperation.RawHardware,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.ActiveOwner.Operation, "Every scanner session operation must map to a runtime gate operation.")
        };
    }

    public static ScanDebugRuntimeCommandPolicy GetPolicy(ScanDebugRuntimeCommandKind command)
        => command switch
        {
            ScanDebugRuntimeCommandKind.ConnectDevices or
            ScanDebugRuntimeCommandKind.DisconnectDevices => Policy(
                requiresDeviceConnection: command == ScanDebugRuntimeCommandKind.DisconnectDevices,
                claimedOperation: ScanDebugRuntimeOperation.Connection,
                ConnectionConflicts),

            ScanDebugRuntimeCommandKind.StartScan => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.Scan,
                ScanStartConflicts),
            ScanDebugRuntimeCommandKind.StopScan => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.None),
            ScanDebugRuntimeCommandKind.ExportDng => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.OutputCapture,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.ApplyDeviceClock => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.DeviceGlobal,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.ApplyParameters => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.ParameterApplication,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.RefreshIllumination or
            ScanDebugRuntimeCommandKind.ApplyIllumination => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.Illumination,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.RefreshMotion or
            ScanDebugRuntimeCommandKind.EnableMotor or
            ScanDebugRuntimeCommandKind.DisableMotor or
            ScanDebugRuntimeCommandKind.MoveMotor or
            ScanDebugRuntimeCommandKind.ApplyMotorConfig => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.Motor,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.StopMotor => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.None),
            ScanDebugRuntimeCommandKind.AutoBlackAdjust or
            ScanDebugRuntimeCommandKind.AutoWhiteAdjust or
            ScanDebugRuntimeCommandKind.AutoCalibrate => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.AutoCalibration,
                 RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.RestoreCalibrationCandidate or
            ScanDebugRuntimeCommandKind.AcceptCalibrationCandidate or
            ScanDebugRuntimeCommandKind.AcceptAndSaveCalibrationCandidate => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.ParameterApplication,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.AutoFocus => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.AutoFocus,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.StartManualFocus => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.ManualFocus,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.StopManualFocus => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.None),
            ScanDebugRuntimeCommandKind.StopAllFocus => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.None),
            ScanDebugRuntimeCommandKind.StopAllMotors => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.None),
            ScanDebugRuntimeCommandKind.UpdateFocusMapping => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.DeviceGlobal,
                RuntimeMutationConflicts),

            ScanDebugRuntimeCommandKind.SaveChannelProfile or
            ScanDebugRuntimeCommandKind.ClearChannelProfile or
            ScanDebugRuntimeCommandKind.SaveColumnSampleAsBlackLevel or
            ScanDebugRuntimeCommandKind.SaveColumnSampleAsWhiteLevel or
            ScanDebugRuntimeCommandKind.CalibrationLibraryReplace => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.CalibrationRepository,
                RepositoryMutationConflicts),
            ScanDebugRuntimeCommandKind.SaveFilmProfileJson => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileExport,
                ProfileExportConflicts),
            ScanDebugRuntimeCommandKind.LoadFilmProfileJson => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileImport,
                ProfileImportConflicts),
            ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport or
            ScanDebugRuntimeCommandKind.NewFilmProfile => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileLifecycle,
                ProfileImportConflicts),
            ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileLifecycle,
                ManualReferenceLevelConflicts),
            ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileLifecycle,
                ProfileOnlyConflicts),
            ScanDebugRuntimeCommandKind.ValidateFilmProfile or
            ScanDebugRuntimeCommandKind.ResetSelectedRoi or
            ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs or
            ScanDebugRuntimeCommandKind.ResetAllRois => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.ProfileLifecycle,
                ProfileMutationConflicts),
            ScanDebugRuntimeCommandKind.DeviceGlobalWrite => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.DeviceGlobal,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.RawHardwareCommand => Policy(
                requiresDeviceConnection: true,
                claimedOperation: ScanDebugRuntimeOperation.RawHardware,
                RuntimeMutationConflicts),
            ScanDebugRuntimeCommandKind.PreviewOnlyEdit => Policy(
                requiresDeviceConnection: false,
                claimedOperation: ScanDebugRuntimeOperation.None),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Every Scan Debug runtime command must define a policy row.")
        };

    private static ScanDebugRuntimeCommandPolicy Policy(
        bool requiresDeviceConnection,
        ScanDebugRuntimeOperation claimedOperation,
        params ScanDebugRuntimeOperation[] conflictingActiveOperations)
    {
        var conflicts = conflictingActiveOperations.ToHashSet();
        if (claimedOperation != ScanDebugRuntimeOperation.None)
            conflicts.Add(claimedOperation);

        return new ScanDebugRuntimeCommandPolicy(requiresDeviceConnection, claimedOperation, conflicts);
    }

    private static readonly ScanDebugRuntimeOperation[] ConnectionConflicts =
    [
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ScanStartConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] RuntimeMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileExportConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository
    ];

    private static readonly ScanDebugRuntimeOperation[] RepositoryMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileImportConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ManualReferenceLevelConflicts =
    [
        ..ProfileImportConflicts,
        ScanDebugRuntimeOperation.ProfileImport
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.CalibrationRepository
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileOnlyConflicts =
    [
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport
    ];
}

public sealed class ScanDebugRuntimeOperationClaims
{
    private readonly object _gate = new();
    private readonly Dictionary<ScanDebugRuntimeOperation, Guid> _claimedOperationTokens = [];

    public event EventHandler? Changed;

    public ScanDebugRuntimeOperationSnapshot CreateSnapshot(ScanDebugRuntimeOperationSnapshot derivedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(derivedSnapshot);

        lock (_gate)
        {
            return new ScanDebugRuntimeOperationSnapshot(
                derivedSnapshot.IsDeviceConnected,
                derivedSnapshot.ActiveOperations.Concat(_claimedOperationTokens.Keys));
        }
    }

    public bool TryClaim(
        ScanDebugRuntimeOperationSnapshot derivedSnapshot,
        ScanDebugRuntimeCommandKind command,
        out ScanDebugRuntimeOperationLease? lease,
        out ScanDebugRuntimeOperationGateResult result)
    {
        ArgumentNullException.ThrowIfNull(derivedSnapshot);

        lock (_gate)
        {
            result = ScanDebugRuntimeOperationGate.Evaluate(CreateSnapshotCore(derivedSnapshot), command);
            if (!result.CanExecute)
            {
                lease = null;
                return false;
            }

            var operation = ScanDebugRuntimeOperationGate.ClaimedOperationFor(command);
            if (operation == ScanDebugRuntimeOperation.None)
            {
                lease = null;
                return true;
            }

            var claimToken = Guid.NewGuid();
            _claimedOperationTokens[operation] = claimToken;
            lease = new ScanDebugRuntimeOperationLease(this, operation, claimToken);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        var changed = false;
        lock (_gate)
        {
            changed = _claimedOperationTokens.Count > 0;
            _claimedOperationTokens.Clear();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDeviceBoundClaims()
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var operation in _claimedOperationTokens.Keys.Where(ScanDebugRuntimeOperationGate.IsDeviceBoundOperation).ToArray())
                changed |= _claimedOperationTokens.Remove(operation);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private ScanDebugRuntimeOperationSnapshot CreateSnapshotCore(ScanDebugRuntimeOperationSnapshot derivedSnapshot)
        => new(
            derivedSnapshot.IsDeviceConnected,
            derivedSnapshot.ActiveOperations.Concat(_claimedOperationTokens.Keys));

    private void Release(ScanDebugRuntimeOperation operation, Guid claimToken)
    {
        var changed = false;
        lock (_gate)
        {
            if (_claimedOperationTokens.TryGetValue(operation, out var currentToken)
                && currentToken == claimToken)
            {
                changed = _claimedOperationTokens.Remove(operation);
            }
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public sealed class ScanDebugRuntimeOperationLease : IDisposable
    {
        private ScanDebugRuntimeOperationClaims? _owner;
        private readonly ScanDebugRuntimeOperation _operation;
        private readonly Guid _claimToken;

        internal ScanDebugRuntimeOperationLease(ScanDebugRuntimeOperationClaims owner, ScanDebugRuntimeOperation operation, Guid claimToken)
        {
            _owner = owner;
            _operation = operation;
            _claimToken = claimToken;
        }

        public void Dispose()
            => Interlocked.Exchange(ref _owner, null)?.Release(_operation, _claimToken);
    }
}
