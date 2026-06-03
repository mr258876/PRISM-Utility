using LibUsbDotNet.Main;
using PRISM_Utility.Core.Contracts.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UsbCatalog")]
public sealed class UsbDeviceCatalogTests
{
    [Fact]
    public void GetDevices_ReturnsCachedSnapshotWithoutRefreshing()
    {
        var refreshCount = 0;
        using var catalog = new UsbDeviceCatalog(() =>
        {
            refreshCount++;
            return CreateSnapshot(new UsbDeviceDto("scanner-in", 0x1D50, 0x619C, 0x0001, "Scanner IN"));
        });

        Assert.Empty(catalog.GetDevices());
        Assert.Equal(0, refreshCount);
    }

    [Fact]
    public async Task RequestRefresh_UpdatesCacheBeforeCallback()
    {
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var catalog = new UsbDeviceCatalog(() =>
            CreateSnapshot(new UsbDeviceDto("scanner-out", 0x1D50, 0x619D, 0x0001, "Scanner OUT")));

        catalog.RequestRefresh(() => refreshed.SetResult());

        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var devices = catalog.GetDevices();

        var device = Assert.Single(devices);
        Assert.Equal("scanner-out", device.Id);
        Assert.Equal(0x619D, device.Pid);
    }

    [Fact]
    public async Task RequestRefresh_CoalescesRequestsQueuedDuringActiveRefresh()
    {
        var firstRefreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacksCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;
        var callbackCount = 0;

        using var catalog = new UsbDeviceCatalog(() =>
        {
            var count = Interlocked.Increment(ref refreshCount);
            if (count == 1)
            {
                firstRefreshEntered.SetResult();
                releaseFirstRefresh.Task.Wait(TimeSpan.FromSeconds(5));
            }

            return CreateSnapshot(new UsbDeviceDto($"scanner-{count}", 0x1D50, 0x619C, 0x0001, $"Scanner {count}"));
        });

        void OnRefreshed()
        {
            if (Interlocked.Increment(ref callbackCount) == 2)
                callbacksCompleted.SetResult();
        }

        catalog.RequestRefresh();
        await firstRefreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        catalog.RequestRefresh(OnRefreshed);
        catalog.RequestRefresh(OnRefreshed);

        releaseFirstRefresh.SetResult();
        await callbacksCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, refreshCount);
        Assert.Equal(2, callbackCount);
        Assert.Equal("scanner-2", Assert.Single(catalog.GetDevices()).Id);
    }

    private static UsbDeviceCatalogSnapshot CreateSnapshot(params UsbDeviceDto[] devices)
        => new(new Dictionary<string, UsbRegistry>(), devices);
}
