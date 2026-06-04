using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScannerAccessCoordinator : IScannerAccessCoordinator, IDisposable
{
    private readonly IScannerDeviceSessionManager _sessionManager;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScanWorkflowSessionCoordinator _scanWorkflow;
    private readonly IScanDebugSessionCoordinator _scanDebug;

    public ScannerAccessCoordinator(
        IScannerDeviceSessionManager sessionManager,
        IUsbUsageCoordinator usbUsageCoordinator,
        IScanWorkflowSessionCoordinator scanWorkflow,
        IScanDebugSessionCoordinator scanDebug)
    {
        _sessionManager = sessionManager;
        _usbUsageCoordinator = usbUsageCoordinator;
        _scanWorkflow = scanWorkflow;
        _scanDebug = scanDebug;

        _sessionManager.SnapshotChanged += OnScannerSnapshotChanged;
        _sessionManager.TargetsChanged += OnScannerTargetsChanged;
        _usbUsageCoordinator.ActiveLeaseChanged += OnUsbActiveLeaseChanged;
    }

    public event EventHandler<ScannerAccessSnapshot>? SnapshotChanged;

    public ScannerAccessSnapshot Snapshot => BuildSnapshot();

    public bool CanActivate(ScannerAccessMode mode)
        => GetActivationBlockedReason(mode, Snapshot) is null;

    public bool CanDeactivate(ScannerAccessMode mode)
        => GetDeactivationBlockedReason(mode, Snapshot) is null;

    public Task<ScanOperationResult> ActivateAsync(ScannerAccessMode mode, CancellationToken ct)
    {
        var blockedReason = GetActivationBlockedReason(mode, Snapshot);
        if (blockedReason is not null)
            return Task.FromResult(new ScanOperationResult(false, blockedReason));

        return mode switch
        {
            ScannerAccessMode.ScanWorkflow => _scanWorkflow.ConnectAsync(ct),
            ScannerAccessMode.ScanDebug => _scanDebug.ConnectAsync(ct),
            ScannerAccessMode.UsbDebugRaw => Task.FromResult(new ScanOperationResult(false, "USB Debug raw access is started from the USB Debug page.")),
            _ => Task.FromResult(new ScanOperationResult(false, "Select a scanner access mode before connecting."))
        };
    }

    public Task<ScanOperationResult> DeactivateAsync(ScannerAccessMode mode, CancellationToken ct)
    {
        var blockedReason = GetDeactivationBlockedReason(mode, Snapshot);
        if (blockedReason is not null)
            return Task.FromResult(new ScanOperationResult(false, blockedReason));

        return mode switch
        {
            ScannerAccessMode.ScanWorkflow => _scanWorkflow.DisconnectAsync(ct),
            ScannerAccessMode.ScanDebug => _scanDebug.DisconnectAsync(ct),
            ScannerAccessMode.UsbDebugRaw => Task.FromResult(new ScanOperationResult(false, "USB Debug raw access is stopped from the USB Debug page.")),
            _ => Task.FromResult(new ScanOperationResult(false, "Select a scanner access mode before disconnecting."))
        };
    }

    public void Dispose()
    {
        _sessionManager.SnapshotChanged -= OnScannerSnapshotChanged;
        _sessionManager.TargetsChanged -= OnScannerTargetsChanged;
        _usbUsageCoordinator.ActiveLeaseChanged -= OnUsbActiveLeaseChanged;
    }

    private void OnScannerSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
        => PublishSnapshotChanged();

    private void OnScannerTargetsChanged(object? sender, EventArgs e)
        => PublishSnapshotChanged();

    private void OnUsbActiveLeaseChanged(object? sender, UsbUsageLeaseSnapshot? snapshot)
        => PublishSnapshotChanged();

    private void PublishSnapshotChanged()
        => SnapshotChanged?.Invoke(this, Snapshot);

    private ScannerAccessSnapshot BuildSnapshot()
    {
        var scannerSnapshot = _sessionManager.Snapshot;
        var usbLease = _usbUsageCoordinator.ActiveLease;
        var targets = _sessionManager.Targets;
        var activeMode = ResolveActiveMode(scannerSnapshot, usbLease);
        var availability = ResolveAvailability(scannerSnapshot, usbLease, targets, activeMode);
        var blockedReason = BuildBlockedReason(scannerSnapshot, usbLease, targets, activeMode, availability);

        return new ScannerAccessSnapshot(activeMode, availability, scannerSnapshot, usbLease, targets, blockedReason);
    }

    private static ScannerAccessMode ResolveActiveMode(ScannerDeviceSessionSnapshot scannerSnapshot, UsbUsageLeaseSnapshot? usbLease)
    {
        if (scannerSnapshot.ActiveOwner?.OwnerType == ScannerSessionOwnerType.ScanWorkflow)
            return ScannerAccessMode.ScanWorkflow;

        if (scannerSnapshot.ActiveOwner?.OwnerType == ScannerSessionOwnerType.ScanDebug)
            return ScannerAccessMode.ScanDebug;

        if (usbLease?.OwnerType == UsbUsageOwnerType.RawUsb)
            return ScannerAccessMode.UsbDebugRaw;

        if (scannerSnapshot.ActiveOwner is not null || usbLease is not null)
            return ScannerAccessMode.Unknown;

        return ScannerAccessMode.None;
    }

    private static ScannerAccessAvailability ResolveAvailability(
        ScannerDeviceSessionSnapshot scannerSnapshot,
        UsbUsageLeaseSnapshot? usbLease,
        ScanTargetState targets,
        ScannerAccessMode activeMode)
    {
        if (scannerSnapshot.State == ScannerSessionState.Connecting)
            return ScannerAccessAvailability.Connecting;

        if (scannerSnapshot.State == ScannerSessionState.Running)
            return ScannerAccessAvailability.Running;

        if (scannerSnapshot.State == ScannerSessionState.ReconnectPrompt)
            return ScannerAccessAvailability.ReconnectRequired;

        if (scannerSnapshot.State == ScannerSessionState.Faulted)
            return ScannerAccessAvailability.Faulted;

        if (scannerSnapshot.State == ScannerSessionState.Connected)
            return ScannerAccessAvailability.Active;

        if (usbLease?.OwnerType == UsbUsageOwnerType.RawUsb)
            return ScannerAccessAvailability.BlockedByUsbDebug;

        if (activeMode == ScannerAccessMode.Unknown)
            return ScannerAccessAvailability.BlockedByUnknownOwner;

        return targets.IsDevicesPresent
            ? ScannerAccessAvailability.Available
            : ScannerAccessAvailability.NoDevice;
    }

    private static string BuildBlockedReason(
        ScannerDeviceSessionSnapshot scannerSnapshot,
        UsbUsageLeaseSnapshot? usbLease,
        ScanTargetState targets,
        ScannerAccessMode activeMode,
        ScannerAccessAvailability availability)
    {
        if (usbLease?.OwnerType == UsbUsageOwnerType.RawUsb)
            return $"USB Debug owns scanner USB access for '{usbLease.Operation}'.";

        if (activeMode == ScannerAccessMode.ScanWorkflow)
            return "Scan workflow owns the scanner session.";

        if (activeMode == ScannerAccessMode.ScanDebug)
            return "Scan Debug owns the scanner session.";

        if (activeMode == ScannerAccessMode.Unknown)
            return scannerSnapshot.ActiveOwner is null
                ? "Scanner USB access is owned by another workflow."
                : $"Scanner session is owned by '{scannerSnapshot.ActiveOwner.OwnerId}' for '{scannerSnapshot.ActiveOwner.Operation}'.";

        if (!targets.IsDevicesPresent)
            return "No PRISM scanner USB endpoints are detected.";

        return availability switch
        {
            ScannerAccessAvailability.Connecting => "Scanner connection is already in progress.",
            ScannerAccessAvailability.Running => "Scanner workflow is running.",
            ScannerAccessAvailability.ReconnectRequired => "Scanner reconnect confirmation is required.",
            ScannerAccessAvailability.Faulted => scannerSnapshot.Fault?.Message ?? "Scanner is faulted.",
            _ => string.Empty
        };
    }

    private static string? GetActivationBlockedReason(ScannerAccessMode mode, ScannerAccessSnapshot snapshot)
    {
        if (mode is ScannerAccessMode.None or ScannerAccessMode.Unknown)
            return "Select a scanner access mode before connecting.";

        if (mode == ScannerAccessMode.UsbDebugRaw)
            return "USB Debug raw access is started from the USB Debug page.";

        if (snapshot.UsbLease?.OwnerType == UsbUsageOwnerType.RawUsb)
            return snapshot.BlockedReason;

        if (!snapshot.Targets.IsDevicesPresent)
            return "Connect a PRISM scanner before starting scanner access.";

        if (snapshot.ScannerSession.State is ScannerSessionState.Connecting or ScannerSessionState.Connected or ScannerSessionState.Running)
            return snapshot.BlockedReason;

        if (snapshot.ActiveMode is ScannerAccessMode.ScanWorkflow or ScannerAccessMode.ScanDebug or ScannerAccessMode.Unknown)
            return snapshot.BlockedReason;

        return null;
    }

    private string? GetDeactivationBlockedReason(ScannerAccessMode mode, ScannerAccessSnapshot snapshot)
    {
        if (mode == ScannerAccessMode.ScanWorkflow)
        {
            if (!_scanWorkflow.HasConnectedSession || snapshot.ActiveMode != ScannerAccessMode.ScanWorkflow)
                return "Scan workflow does not own a connected scanner session.";

            return snapshot.ScannerSession.State == ScannerSessionState.Connected
                ? null
                : "Scan workflow can disconnect only when the scanner is connected and idle.";
        }

        if (mode == ScannerAccessMode.ScanDebug)
        {
            if (!_scanDebug.HasConnectedSession || snapshot.ActiveMode != ScannerAccessMode.ScanDebug)
                return "Scan Debug does not own a connected scanner session.";

            return snapshot.ScannerSession.State == ScannerSessionState.Connected
                ? null
                : "Scan Debug can disconnect only when the scanner is connected and idle.";
        }

        if (mode == ScannerAccessMode.UsbDebugRaw)
            return snapshot.ActiveMode == ScannerAccessMode.UsbDebugRaw
                ? "USB Debug raw access is stopped from the USB Debug page."
                : "USB Debug raw access is not active.";

        return "Select a scanner access mode before disconnecting.";
    }
}
