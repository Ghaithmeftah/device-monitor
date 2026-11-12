using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeviceMonitor.Tests;

/// <summary>Runs the real app in memory with SQLite instead of PostgreSQL.</summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

public sealed class DevicesApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly AppFactory _factory = new();
    private readonly HttpClient _client;

    public DevicesApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostDevice(string name, string serial, string type = "Gateway") =>
        _client.PostAsJsonAsync("/api/devices", new { name, serialNumber = serial, type });

    [Fact]
    public async Task Created_device_can_be_read_back()
    {
        var response = await PostDevice("Gateway", "GW-0001");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DeviceDto>(Json);
        var fetched = await _client.GetFromJsonAsync<DeviceDto>(response.Headers.Location, Json);
        Assert.Equal(created, fetched);
    }

    [Theory]
    [InlineData("", "GW-0001")]
    [InlineData("Gateway", "no spaces allowed")]
    [InlineData("Gateway", "AB")]
    public async Task Invalid_device_is_rejected_with_400(string name, string serial)
    {
        var response = await PostDevice(name, serial);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_serial_returns_409()
    {
        await PostDevice("First", "GW-0001");

        var response = await PostDevice("Second", "GW-0001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_for_unknown_device_returns_404()
    {
        var response = await _client.PostAsync("/api/devices/12345/heartbeat", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_is_pushed_to_SignalR_clients()
    {
        var created = await (await PostDevice("Meter", "SM-0001", "SmartMeter")).Content.ReadFromJsonAsync<DeviceDto>(Json);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/devices"), o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        hub.On<JsonElement>("DeviceUpdated", d => received.TrySetResult(d));
        await hub.StartAsync();

        await _client.PostAsync($"/api/devices/{created!.Id}/heartbeat", null);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(created.Id, pushed.GetProperty("id").GetInt32());
        Assert.Equal("Online", pushed.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dashboard_page_loads()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("Register a device", html);
    }
}
