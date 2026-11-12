using DeviceMonitor.Web.Models;

namespace DeviceMonitor.Web.Services;

/// <summary>Pushes device changes to connected clients. Kept behind an interface so services stay testable without SignalR.</summary>
public interface IDeviceNotifier
{
    Task DeviceUpdatedAsync(DeviceDto device, CancellationToken ct = default);
    Task DeviceDeletedAsync(int id, CancellationToken ct = default);
}
