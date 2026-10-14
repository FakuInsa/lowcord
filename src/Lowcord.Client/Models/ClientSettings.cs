namespace Lowcord.Client.Models;

public class ClientSettings
{
    public string Username { get; set; } = "";
    public string ServerCode { get; set; } = "facu";
    public string Channel { get; set; } = "general";
    public string Password { get; set; } = "";
    public bool IsHosting { get; set; } = false;
    public uint HotkeyVk { get; set; } = 0x4D; // Keys.M
    public string HotkeyCode { get; set; } = "KeyM";
}
