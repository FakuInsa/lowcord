using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class CloudflareTunnelService : ITunnelService
{
    private Process? _tunnelProcess;
    public bool IsRunning => _tunnelProcess != null && !_tunnelProcess.HasExited && !string.IsNullOrEmpty(CurrentTunnelUrl);
    public string? CurrentTunnelUrl { get; private set; }

    public event Action<string>? OnLog;

    public async Task<string?> StartTunnelAsync(int localPort)
    {
        await StopTunnelAsync();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var cloudflaredExe = Path.Combine(baseDir, "cloudflared.exe");
        if (!File.Exists(cloudflaredExe))
        {
            var fallback = Path.Combine(baseDir, "..", "dist", "cloudflared.exe");
            if (File.Exists(fallback)) cloudflaredExe = fallback;
        }

        var cfLog = Path.Combine(baseDir, "cloudflared.log");
        if (File.Exists(cfLog))
        {
            try { File.Delete(cfLog); } catch { }
        }

        // Descarga automática si no existe el binario
        if (!File.Exists(cloudflaredExe))
        {
            OnLog?.Invoke("[Cloudflare] Descargando binario oficial cloudflared...");
            try
            {
                cloudflaredExe = Path.Combine(baseDir, "cloudflared.exe");
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var bytes = await hc.GetByteArrayAsync("https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe");
                await File.WriteAllBytesAsync(cloudflaredExe, bytes);
                OnLog?.Invoke("[Cloudflare] Descarga completada con éxito.");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Cloudflare] Aviso descarga: {ex.Message}");
                return null;
            }
        }

        OnLog?.Invoke($"[Cloudflare] Iniciando túnel seguro HTTPS hacia puerto {localPort}...");

        try
        {
            var cfPsi = new ProcessStartInfo
            {
                FileName = cloudflaredExe,
                Arguments = $"tunnel --url http://127.0.0.1:{localPort} --logfile \"{cfLog}\"",
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _tunnelProcess = Process.Start(cfPsi);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[Cloudflare-Err] No se pudo lanzar cloudflared: {ex.Message}");
            return null;
        }

        // Buscar URL en el log cada 250ms durante 20 segundos
        CurrentTunnelUrl = null;
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(300);
            if (File.Exists(cfLog))
            {
                try
                {
                    using var stream = new FileStream(cfLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    var match = Regex.Match(content, @"(https://[a-zA-Z0-9-]+\.trycloudflare\.com)");
                    if (match.Success)
                    {
                        CurrentTunnelUrl = match.Groups[1].Value;
                        OnLog?.Invoke($"[Cloudflare] ✓ Túnel HTTPS listo: {CurrentTunnelUrl}");
                        return CurrentTunnelUrl;
                    }
                }
                catch { }
            }
        }

        OnLog?.Invoke("[Cloudflare] No se pudo obtener la URL del túnel tras 18 segundos.");
        return null;
    }

    public async Task StopTunnelAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (_tunnelProcess != null && !_tunnelProcess.HasExited)
                {
                    _tunnelProcess.Kill();
                }
                foreach (var p in Process.GetProcessesByName("cloudflared"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
            finally
            {
                _tunnelProcess = null;
                CurrentTunnelUrl = null;
            }
        });
        OnLog?.Invoke("[Cloudflare] Túnel detenido.");
    }

    public void Dispose()
    {
        try
        {
            if (_tunnelProcess != null && !_tunnelProcess.HasExited)
            {
                _tunnelProcess.Kill();
                _tunnelProcess.Dispose();
            }
        }
        catch { }
    }
}
