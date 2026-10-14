namespace Lowcord.Client.Models;

public class HostStatus
{
    public bool IsRunning { get; set; } = false;
    public string RoomName { get; set; } = "";
    public string? TunnelUrl { get; set; }
    public string Message { get; set; } = "";
}
