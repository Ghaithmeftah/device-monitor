using DeviceMonitor.Web.Models;
using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers.Api;

[ApiController]
[Route("api/devices")]
public class DevicesController(IDeviceService devices) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DeviceDto>> GetAll(CancellationToken ct) => devices.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceDto>> Get(int id, CancellationToken ct) =>
        await devices.GetAsync(id, ct) is { } device ? device : NotFound();

    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create(CreateDeviceRequest request, CancellationToken ct)
    {
        var device = await devices.CreateAsync(request, ct);
        if (device is null)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A device with this serial number already exists.");
        return CreatedAtAction(nameof(Get), new { id = device.Id }, device);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await devices.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Called by a device (or the dashboard's simulate button) to report that it is alive.</summary>
    [HttpPost("{id:int}/heartbeat")]
    public async Task<ActionResult<DeviceDto>> Heartbeat(int id, CancellationToken ct) =>
        await devices.RecordHeartbeatAsync(id, ct) is { } device ? device : NotFound();
}
