using System.Collections.Generic;

namespace Lowcord.Client.Models;

public class RoomInfo
{
    public string RoomId { get; set; } = "";
    public bool HasPassword { get; set; } = false;
    public string HostUser { get; set; } = "";
    public int UserCount { get; set; } = 0;
    public List<string> Users { get; set; } = new();
    public string? TunnelUrl { get; set; }
}
