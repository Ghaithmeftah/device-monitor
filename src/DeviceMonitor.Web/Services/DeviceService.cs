using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Services;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceDto>> GetAllAsync(CancellationToken ct = default);
    Task<DeviceDto?> GetAsync(int id, CancellationToken ct = default);
    /// <returns>The new device, or null when the serial number is already registered.</returns>
    Task<DeviceDto?> CreateAsync(CreateDeviceRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    /// <returns>The updated device, or null when it does not exist.</returns>
    Task<DeviceDto?> RecordHeartbeatAsync(int id, CancellationToken ct = default);
    /// <returns>How many devices were switched to offline.</returns>
    Task<int> MarkStaleDevicesOfflineAsync(TimeSpan timeout, CancellationToken ct = default);
}

public class DeviceService(AppDbContext db, IDeviceNotifier notifier, TimeProvider clock) : IDeviceService
{
    public async Task<IReadOnlyList<DeviceDto>> GetAllAsync(CancellationToken ct = default) =>
        (await db.Devices.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct)).Select(DeviceDto.From).ToList();

    public async Task<DeviceDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return device is null ? null : DeviceDto.From(device);
    }

    public async Task<DeviceDto?> CreateAsync(CreateDeviceRequest request, CancellationToken ct = default)
    {
        var serial = request.SerialNumber.Trim().ToUpperInvariant();
        if (await db.Devices.AnyAsync(d => d.SerialNumber == serial, ct))
            return null;

        var device = new Device { Name = request.Name.Trim(), SerialNumber = serial, Type = request.Type };
        db.Devices.Add(device);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request registered the same serial between the check and the insert; the unique index caught it.
            return null;
        }

        var dto = DeviceDto.From(device);
        await notifier.DeviceUpdatedAsync(dto, ct);
        return dto;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var deleted = await db.Devices.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0) return false;
        await notifier.DeviceDeletedAsync(id, ct);
        return true;
    }

    public async Task<DeviceDto?> RecordHeartbeatAsync(int id, CancellationToken ct = default)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (device is null) return null;

        device.Status = DeviceStatus.Online;
        device.LastSeenUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var dto = DeviceDto.From(device);
        await notifier.DeviceUpdatedAsync(dto, ct);
        return dto;
    }

    public async Task<int> MarkStaleDevicesOfflineAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var limit = clock.GetUtcNow() - timeout;
        // The date filter runs in memory: SQLite (used by the tests) cannot compare DateTimeOffset in SQL.
        // Fine for a demo fleet; filter in SQL before scaling to many devices.
        var stale = (await db.Devices.Where(d => d.Status == DeviceStatus.Online).ToListAsync(ct))
            .Where(d => d.LastSeenUtc is null || d.LastSeenUtc < limit)
            .ToList();
        if (stale.Count == 0) return 0;

        foreach (var device in stale) device.Status = DeviceStatus.Offline;
        await db.SaveChangesAsync(ct);

        foreach (var device in stale) await notifier.DeviceUpdatedAsync(DeviceDto.From(device), ct);
        return stale.Count;
    }
}
