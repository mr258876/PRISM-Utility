using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class UsbUsageCoordinatorLeaseTests
{
    [Fact]
    public async Task Lease_AcquireRelease_PublishesOwnershipMetadataAndClearsOnRelease()
    {
        var coordinator = new UsbUsageCoordinator();

        var acquireResult = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Connect scanner");

        Assert.True(acquireResult.Success);
        Assert.NotNull(acquireResult.Lease);
        Assert.True(coordinator.IsScanDebugInUse);
        Assert.False(coordinator.IsUsbDebugInUse);
        Assert.NotNull(coordinator.ActiveLease);
        Assert.Equal("scan-owner", coordinator.ActiveLease!.OwnerId);
        Assert.Equal(UsbUsageOwnerType.Scanner, coordinator.ActiveLease.OwnerType);
        Assert.Equal("Connect scanner", coordinator.ActiveLease.Operation);
        Assert.Equal(acquireResult.Lease!.ReleaseToken, coordinator.ActiveLease.ReleaseToken);

        var releaseResult = await acquireResult.Lease.ReleaseAsync();

        Assert.True(releaseResult);
        Assert.False(coordinator.IsScanDebugInUse);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Lease_BlocksCompetingMutatingOwner()
    {
        var coordinator = new UsbUsageCoordinator();
        var firstLease = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");

        var secondLease = await coordinator.TryAcquireLeaseAsync("usb-owner", UsbUsageOwnerType.RawUsb, "Bulk IN");

        Assert.True(firstLease.Success);
        Assert.False(secondLease.Success);
        Assert.Null(secondLease.Lease);
        Assert.NotNull(secondLease.ActiveLease);
        Assert.Equal("scan-owner", secondLease.ActiveLease!.OwnerId);
        Assert.Equal(UsbUsageOwnerType.Scanner, secondLease.ActiveLease.OwnerType);
    }

    [Fact]
    public async Task Lease_ReadOnlyObserverCheck_IsAllowedAndDoesNotAcquireOwnership()
    {
        var coordinator = new UsbUsageCoordinator();
        var acquireResult = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");
        var activeLease = coordinator.ActiveLease;

        var canObserve = coordinator.CanObserveReadOnly("usb-observer", UsbUsageOwnerType.RawUsb);

        Assert.True(acquireResult.Success);
        Assert.True(canObserve);
        Assert.Equal(activeLease, coordinator.ActiveLease);
        Assert.True(coordinator.IsScanDebugInUse);
        Assert.False(coordinator.IsUsbDebugInUse);
    }

    [Fact]
    public async Task Lease_WrongTokenRelease_IsDenied()
    {
        var coordinator = new UsbUsageCoordinator();
        var acquireResult = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");

        var released = await coordinator.ReleaseAsync(Guid.NewGuid());

        Assert.True(acquireResult.Success);
        Assert.False(released);
        Assert.NotNull(coordinator.ActiveLease);
        Assert.Equal(acquireResult.Lease!.ReleaseToken, coordinator.ActiveLease!.ReleaseToken);
    }

    [Fact]
    public async Task Lease_DoubleRelease_ReturnsFalseWithoutThrowing()
    {
        var coordinator = new UsbUsageCoordinator();
        var acquireResult = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");

        var firstRelease = await acquireResult.Lease!.ReleaseAsync();
        var secondRelease = await acquireResult.Lease.ReleaseAsync();

        Assert.True(firstRelease);
        Assert.False(secondRelease);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Lease_ForceRelease_CancelsAndClearsMatchingOwner()
    {
        var coordinator = new UsbUsageCoordinator();
        var acquireResult = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");

        var forced = await coordinator.ForceReleaseAsync("scan-owner", UsbUsageOwnerType.Scanner);

        Assert.True(acquireResult.Success);
        Assert.True(forced);
        Assert.True(acquireResult.Lease!.CancellationToken.IsCancellationRequested);
        Assert.Null(coordinator.ActiveLease);
    }

    [Fact]
    public async Task Lease_ReleaseCore_DoesNotClearNewerSameOwnerLease()
    {
        var coordinator = new UsbUsageCoordinator();
        var firstLease = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan");
        var staleReleaseToken = firstLease.Lease!.ReleaseToken;

        await firstLease.Lease.ReleaseAsync();
        var secondLease = await coordinator.TryAcquireLeaseAsync("scan-owner", UsbUsageOwnerType.Scanner, "Scan again");

        var forced = await coordinator.ReleaseAsync(staleReleaseToken);

        Assert.True(secondLease.Success);
        Assert.False(forced);
        Assert.NotNull(coordinator.ActiveLease);
        Assert.Equal(secondLease.Lease!.ReleaseToken, coordinator.ActiveLease!.ReleaseToken);
        Assert.True(coordinator.IsScanDebugInUse);
    }
}
