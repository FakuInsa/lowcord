using System;

namespace Lowcord.Client.Services.Contracts;

public interface IHotkeyService : IDisposable
{
    uint TargetVk { get; }
    string HotkeyCode { get; }
    void Start();
    void Stop();
    void SetHotkey(string code, uint vk = 0);
    event Action<bool>? OnKeyToggled; // true = KeyDown, false = KeyUp
}
