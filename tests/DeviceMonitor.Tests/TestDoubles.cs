using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Models;
using DeviceMonitor.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Tests;

public class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public class RecordingNotifier : IDeviceNotifier
{
    public List<DeviceDto> Updated { get; } = [];
    public List<int> Deleted { get; } = [];

    public Task DeviceUpdatedAsync(DeviceDto device, CancellationToken ct = default)
    {
        Updated.Add(device);
        return Task.CompletedTask;
    }

    public Task DeviceDeletedAsync(int id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        return Task.CompletedTask;
    }
}

/// <summary>A real relational database (SQLite in memory), so unique indexes and ExecuteDelete behave like in production.</summary>
public sealed class SqliteDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteDb()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
