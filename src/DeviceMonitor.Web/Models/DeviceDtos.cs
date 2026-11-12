using System.ComponentModel.DataAnnotations;

namespace DeviceMonitor.Web.Models;

public record DeviceDto(int Id, string Name, string SerialNumber, DeviceType Type, DeviceStatus Status, DateTimeOffset? LastSeenUtc)
{
    public static DeviceDto From(Device d) => new(d.Id, d.Name, d.SerialNumber, d.Type, d.Status, d.LastSeenUtc);
}

public record CreateDeviceRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, RegularExpression("^[A-Za-z0-9-]{4,32}$", ErrorMessage = "Serial number must be 4-32 characters: letters, digits or '-'.")] string SerialNumber,
    [EnumDataType(typeof(DeviceType))] DeviceType Type);
