using Lowcord.Client.Models;

namespace Lowcord.Client.Services.Contracts;

public interface ISettingsService
{
    ClientSettings Settings { get; }
    void LoadSettings();
    void SaveSettings();
}
