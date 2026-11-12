namespace DeviceMonitor.Web.Services;

/// <summary>Switches devices to offline when no heartbeat arrived within the configured timeout.</summary>
public class OfflineDetector(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfflineDetector> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeout = TimeSpan.FromSeconds(config.GetValue("Monitoring:HeartbeatTimeoutSeconds", 30));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.GetValue("Monitoring:CheckIntervalSeconds", 5)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                var count = await service.MarkStaleDevicesOfflineAsync(timeout, stoppingToken);
                if (count > 0) logger.LogInformation("{Count} device(s) marked offline", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the loop alive: a database blip must not stop monitoring for good.
                logger.LogError(ex, "Offline detection failed");
            }
        }
    }
}
