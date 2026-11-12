using DeviceMonitor.Web.Models;
using DeviceMonitor.Web.Services;

namespace DeviceMonitor.Tests;

public sealed class DeviceServiceTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private DeviceService CreateService() => new(_db.CreateContext(), _notifier, _clock);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_registers_device_as_offline_with_normalized_serial()
    {
        var device = await CreateService().CreateAsync(new CreateDeviceRequest(" Gateway ", "gw-0001", DeviceType.Gateway));

        Assert.NotNull(device);
        Assert.Equal("Gateway", device.Name);
        Assert.Equal("GW-0001", device.SerialNumber);
        Assert.Equal(DeviceStatus.Offline, device.Status);
        Assert.Null(device.LastSeenUtc);
        Assert.Single(_notifier.Updated);
    }

    [Fact]
    public async Task Create_rejects_duplicate_serial_number_regardless_of_case()
    {
        await CreateService().CreateAsync(new CreateDeviceRequest("First", "GW-0001", DeviceType.Gateway));

        var duplicate = await CreateService().CreateAsync(new CreateDeviceRequest("Second", "gw-0001", DeviceType.SmartMeter));

        Assert.Null(duplicate);
        Assert.Single(await CreateService().GetAllAsync());
    }

    [Fact]
    public async Task Heartbeat_marks_device_online_and_pushes_update()
    {
        var created = await CreateService().CreateAsync(new CreateDeviceRequest("Meter", "SM-1", DeviceType.SmartMeter));

        var updated = await CreateService().RecordHeartbeatAsync(created!.Id);

        Assert.Equal(DeviceStatus.Online, updated!.Status);
        Assert.Equal(_clock.Now, updated.LastSeenUtc);
        Assert.Equal(DeviceStatus.Online, _notifier.Updated.Last().Status);
    }

    [Fact]
    public async Task Heartbeat_for_unknown_device_returns_null()
    {
        Assert.Null(await CreateService().RecordHeartbeatAsync(999));
        Assert.Empty(_notifier.Updated);
    }

    [Fact]
    public async Task MarkStaleDevicesOffline_only_switches_devices_past_the_timeout()
    {
        var service = CreateService();
        var stale = await service.CreateAsync(new CreateDeviceRequest("Stale", "GW-1", DeviceType.Gateway));
        var fresh = await service.CreateAsync(new CreateDeviceRequest("Fresh", "GW-2", DeviceType.Gateway));
        await CreateService().RecordHeartbeatAsync(stale!.Id);
        _clock.Now = _clock.Now.AddSeconds(40);
        await CreateService().RecordHeartbeatAsync(fresh!.Id);
        _notifier.Updated.Clear();

        var count = await CreateService().MarkStaleDevicesOfflineAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, count);
        Assert.Equal(DeviceStatus.Offline, (await CreateService().GetAsync(stale.Id))!.Status);
        Assert.Equal(DeviceStatus.Online, (await CreateService().GetAsync(fresh.Id))!.Status);
        Assert.Equal(stale.Id, Assert.Single(_notifier.Updated).Id);
    }

    [Fact]
    public async Task Delete_removes_device_and_pushes_deletion()
    {
        var created = await CreateService().CreateAsync(new CreateDeviceRequest("Box", "STB-1", DeviceType.SetTopBox));

        Assert.True(await CreateService().DeleteAsync(created!.Id));
        Assert.False(await CreateService().DeleteAsync(created.Id));
        Assert.Null(await CreateService().GetAsync(created.Id));
        Assert.Equal([created.Id], _notifier.Deleted);
    }
}
