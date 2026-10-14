using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class KestrelServerService : IServerProcessService
{
    private Process? _serverProcess;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;
    public string? CurrentRoom { get; private set; }

    public event Action<string>? OnLog;

    public string? FindServerExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "dist", "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "dist", "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lowcord.Server.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<bool> IsServerHealthyAsync()
    {
        try
        {
            var res = await _httpClient.GetStringAsync("http://127.0.0.1:8080/api/health");
            return res.Contains("healthy", StringComparison.OrdinalIgnoreCase) ||
                   res.Contains("OK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartServerAsync(string initialRoom)
    {
        if (await IsServerHealthyAsync())
        {
            CurrentRoom = initialRoom;
            OnLog?.Invoke($"[Server] Servidor local ya se encontraba activo en http://localhost:8080");
            return true;
        }

        var serverExe = FindServerExecutable();
        if (string.IsNullOrEmpty(serverExe) || !File.Exists(serverExe))
        {
            OnLog?.Invoke("[ERROR] No se encontró Lowcord.Server.exe en la carpeta Servidor.");
            return false;
        }

        CurrentRoom = initialRoom;

        // Limpiar procesos zombies previos
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName("Lowcord.Server"))
            {
                try { p.Kill(); } catch { }
            }
        });

        var srvDir = Path.GetDirectoryName(serverExe)!;
        var roomFile = Path.Combine(srvDir, "room_code.txt");
        try { File.WriteAllText(roomFile, initialRoom); } catch { }

        var psi = new ProcessStartInfo
        {
            FileName = serverExe,
            Arguments = $"--room \"{initialRoom}\"",
            WorkingDirectory = srvDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            _serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _serverProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) OnLog?.Invoke($"[Server] {e.Data}");
            };
            _serverProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) OnLog?.Invoke($"[Server-Err] {e.Data}");
            };
            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ERROR] Falló al iniciar Lowcord.Server: {ex.Message}");
            return false;
        }

        // Esperar a que el servidor responda
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(250);
            if (await IsServerHealthyAsync())
            {
                OnLog?.Invoke("[Server] ✓ Servidor Kestrel online en http://localhost:8080");
                return true;
            }
        }

        return false;
    }

    public async Task StopServerAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                }
                foreach (var p in Process.GetProcessesByName("Lowcord.Server"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
            finally
            {
                _serverProcess = null;
                CurrentRoom = null;
            }
        });
        OnLog?.Invoke("[Server] Servidor detenido.");
    }

    public void Dispose()
    {
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                _serverProcess.Dispose();
            }
        }
        catch { }
    }
}
