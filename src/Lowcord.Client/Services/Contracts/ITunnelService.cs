using System;
using System.Threading.Tasks;

namespace Lowcord.Client.Services.Contracts;

public interface ITunnelService : IDisposable
{
    bool IsRunning { get; }
    string? CurrentTunnelUrl { get; }
    Task<string?> StartTunnelAsync(int localPort);
    Task StopTunnelAsync();
    event Action<string>? OnLog;
}
