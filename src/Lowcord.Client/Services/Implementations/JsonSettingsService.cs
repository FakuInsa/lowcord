using System;
using System.IO;
using System.Text.Json;
using Lowcord.Client.Models;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class JsonSettingsService : ISettingsService
{
    private readonly string _configFilePath;
    public ClientSettings Settings { get; private set; } = new();

    public JsonSettingsService(string? customPath = null)
    {
        _configFilePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_config.json");
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<ClientSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch { }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch { }
    }
}
