namespace DeviceMonitor.Web.Models;

public enum DeviceType { Gateway, SetTopBox, SmartMeter }

public enum DeviceStatus { Offline, Online }

public class Device
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string SerialNumber { get; set; }
    public DeviceType Type { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public DateTimeOffset? LastSeenUtc { get; set; }
}
