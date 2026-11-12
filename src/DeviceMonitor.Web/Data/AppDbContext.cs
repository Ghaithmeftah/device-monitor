using DeviceMonitor.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var device = modelBuilder.Entity<Device>();
        device.Property(d => d.Name).HasMaxLength(100);
        device.Property(d => d.SerialNumber).HasMaxLength(32);
        device.HasIndex(d => d.SerialNumber).IsUnique();
        device.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        device.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
    }
}
