using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class GitHubUpdateService : IUpdateService
{
    private static readonly HttpClient _httpClient = new();
    public string? PendingVersion { get; private set; }
    public string? PendingDownloadUrl { get; private set; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFile(string lpFileName);

    private static void RemoveZoneIdentifier(string filePath)
    {
        try { DeleteFile(filePath + ":Zone.Identifier"); } catch { }
    }

    public async Task<string?> CheckForUpdatesAsync(string currentVersion)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/fakuinsa/lowcord/releases/latest");
            req.Headers.Add("User-Agent", "Lowcord-Client-Updater");
            using var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("tag_name", out var tagProp))
            {
                var latestTag = tagProp.GetString() ?? "";
                if (IsNewerVersion(latestTag, currentVersion))
                {
                    PendingVersion = latestTag;
                    if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assetsProp.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? "";
                            if (name.Equals("Lowcord-Client.exe", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("Lowcord.Client.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                PendingDownloadUrl = asset.GetProperty("browser_download_url").GetString();
                                break;
                            }
                        }
                    }
                    return PendingVersion;
                }
            }
        }
        catch { }

        return null;
    }

    public async Task<bool> ExecuteUpdateAsync(Action<int, string>? onProgress = null)
    {
        if (string.IsNullOrEmpty(PendingDownloadUrl)) return false;

        try
        {
            onProgress?.Invoke(0, "Iniciando descarga de actualización...");

            var currentExe = Environment.ProcessPath ?? Application.ExecutablePath;
            var dir = Path.GetDirectoryName(currentExe)!;
            var tempDownloadFile = Path.Combine(dir, "update_download.tmp");

            using (var req = new HttpRequestMessage(HttpMethod.Get, PendingDownloadUrl))
            {
                req.Headers.Add("User-Agent", "Lowcord-Client-Updater");
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return false;

                var totalBytes = resp.Content.Headers.ContentLength ?? -1L;
                await using var srcStream = await resp.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempDownloadFile, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                int lastReportedPercent = -1;

                while ((read = await srcStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        int percent = (int)((totalRead * 100) / totalBytes);
                        if (percent != lastReportedPercent && percent % 10 == 0)
                        {
                            lastReportedPercent = percent;
                            onProgress?.Invoke(percent, $"Descargando actualización ({percent}%)...");
                        }
                    }
                }
            }

            onProgress?.Invoke(100, "Instalando nueva versión...");

            string newExePath = tempDownloadFile;
            if (PendingDownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempExtractDir = Path.Combine(dir, "update_extracted");
                if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
                ZipFile.ExtractToDirectory(tempDownloadFile, tempExtractDir);

                var foundExe = Directory.GetFiles(tempExtractDir, "Lowcord*.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (foundExe != null) newExePath = foundExe;
                try { File.Delete(tempDownloadFile); } catch { }
            }

            RemoveZoneIdentifier(newExePath);
            RemoveZoneIdentifier(currentExe);

            var oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);

            File.Move(currentExe, oldExe);
            File.Move(newExePath, currentExe);

            RemoveZoneIdentifier(currentExe);

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                WorkingDirectory = dir,
                UseShellExecute = true
            });

            Application.Exit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNewerVersion(string remote, string local)
    {
        remote = remote.TrimStart('v', 'V').Trim();
        local = local.TrimStart('v', 'V').Trim();
        if (Version.TryParse(remote, out var vRemote) && Version.TryParse(local, out var vLocal))
        {
            return vRemote > vLocal;
        }
        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }
}
