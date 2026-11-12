using System.Text.Json.Serialization;
using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Hubs;
using DeviceMonitor.Web.Models;
using DeviceMonitor.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddSingleton<IDeviceNotifier, SignalRDeviceNotifier>();
builder.Services.AddHostedService<OfflineDetector>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql()) db.Database.Migrate();
    else db.Database.EnsureCreated();

    if (app.Environment.IsDevelopment() && !db.Devices.Any())
    {
        db.Devices.AddRange(
            new Device { Name = "Living room gateway", SerialNumber = "GW-0001", Type = DeviceType.Gateway },
            new Device { Name = "4K set-top box", SerialNumber = "STB-0042", Type = DeviceType.SetTopBox },
            new Device { Name = "Building A meter", SerialNumber = "SM-1007", Type = DeviceType.SmartMeter });
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStatusCodePages();
app.UseRouting();

app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.MapHub<DeviceHub>(DeviceHub.Path);

app.Run();

public partial class Program;
