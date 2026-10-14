using System;
using System.Threading.Tasks;

namespace Lowcord.Client.Services.Contracts;

public interface IUpdateService
{
    string? PendingVersion { get; }
    string? PendingDownloadUrl { get; }
    Task<string?> CheckForUpdatesAsync(string currentVersion);
    Task<bool> ExecuteUpdateAsync(Action<int, string>? onProgress = null);
}
