using DeviceMonitor.Web.Models;
using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace DeviceMonitor.Web.Hubs;

/// <summary>Clients only listen on this hub; all changes go through the REST API.</summary>
public class DeviceHub : Hub
{
    public const string Path = "/hubs/devices";
}

public class SignalRDeviceNotifier(IHubContext<DeviceHub> hub) : IDeviceNotifier
{
    public Task DeviceUpdatedAsync(DeviceDto device, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("DeviceUpdated", device, ct);

    public Task DeviceDeletedAsync(int id, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("DeviceDeleted", id, ct);
}
