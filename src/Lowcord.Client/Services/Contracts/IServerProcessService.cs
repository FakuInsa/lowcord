using System;
using System.Threading.Tasks;

namespace Lowcord.Client.Services.Contracts;

public interface IServerProcessService : IDisposable
{
    bool IsRunning { get; }
    string? CurrentRoom { get; }
    Task<bool> StartServerAsync(string initialRoom);
    Task StopServerAsync();
    Task<bool> IsServerHealthyAsync();
    string? FindServerExecutable();
    event Action<string>? OnLog;
}
