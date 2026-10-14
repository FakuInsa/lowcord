using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lowcord.Client.Models;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Services.Implementations;

public class KeyvalRoomRegistryService : IRoomRegistryService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(4) };

    public async Task<bool> RegisterRoomAsync(string roomName, string tunnelUrl, bool hasPassword, string hostUser)
    {
        roomName = roomName.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(tunnelUrl)) return false;

        try
        {
            var encodedUrl = Uri.EscapeDataString(tunnelUrl);
            var safeHost = string.IsNullOrWhiteSpace(hostUser) ? "Host" : Uri.EscapeDataString(hostUser);
            var passFlag = hasPassword ? "1" : "0";

            // 1. Clave estándar compatible con v2.2 y clientes web (directa)
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_{roomName}/{encodedUrl}");

            // 2. Clave con metadata simplificada segura en URL: URL;hasPass;Host
            var metaVal = Uri.EscapeDataString($"{tunnelUrl};{passFlag};{safeHost}");
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_meta_{roomName}/{metaVal}");

            // 3. Agregar a índice de salas
            await AddToRoomsIndexAsync(roomName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnregisterRoomAsync(string roomName)
    {
        roomName = roomName.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(roomName)) return false;

        try
        {
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_{roomName}/offline");
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_meta_{roomName}/offline");
            await RemoveFromRoomsIndexAsync(roomName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> ResolveRoomToUrlAsync(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Si ya es una URL completa
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        // Si es una IP local o puerto directo
        if (input.StartsWith("127.0.0.1") || (input.Contains(':') && !input.Contains(' ')))
        {
            return "http://" + input;
        }

        var code = input.ToLowerInvariant();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // Intentar leer clave directa de sala
            var res = await _httpClient.GetStringAsync($"https://api.keyval.org/get/lowcord_{code}", cts.Token);
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("val", out var valProp))
            {
                var val = valProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) &&
                    !val.Equals("offline", StringComparison.OrdinalIgnoreCase) &&
                    !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return val;
                    }
                }
            }
        }
        catch { }

        // Fallback: intentar leer lowcord_room_{code}
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var res = await _httpClient.GetStringAsync($"https://api.keyval.org/get/lowcord_room_{code}", cts.Token);
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("val", out var valProp))
            {
                var val = valProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) && !val.Equals("offline", StringComparison.OrdinalIgnoreCase))
                {
                    using var innerDoc = JsonDocument.Parse(val);
                    if (innerDoc.RootElement.TryGetProperty("url", out var urlProp))
                    {
                        var url = urlProp.GetString();
                        if (!string.IsNullOrEmpty(url)) return url;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public async Task<List<RoomInfo>> GetActiveRoomsAsync()
    {
        var result = new List<RoomInfo>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var res = await _httpClient.GetStringAsync("https://api.keyval.org/get/lowcord_rooms_index", cts.Token);
            using var doc = JsonDocument.Parse(res);
            if (!doc.RootElement.TryGetProperty("val", out var valProp)) return result;

            var val = valProp.GetString();
            if (string.IsNullOrWhiteSpace(val) || val.Equals("offline", StringComparison.OrdinalIgnoreCase)) return result;

            var roomNames = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var rName in roomNames)
            {
                try
                {
                    using var rCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    string? url = null;
                    bool hasPassword = false;
                    string host = "";

                    // 1. Intentar leer metadata estructurada
                    try
                    {
                        var mRes = await _httpClient.GetStringAsync($"https://api.keyval.org/get/lowcord_meta_{rName}", rCts.Token);
                        using var mDoc = JsonDocument.Parse(mRes);
                        if (mDoc.RootElement.TryGetProperty("val", out var mv) && !string.IsNullOrEmpty(mv.GetString()))
                        {
                            var parts = mv.GetString()!.Split(';');
                            if (parts.Length >= 1 && parts[0].StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                url = parts[0];
                                if (parts.Length >= 2) hasPassword = parts[1] == "1";
                                if (parts.Length >= 3) host = Uri.UnescapeDataString(parts[2]);
                            }
                        }
                    }
                    catch { }

                    // 2. Si no hay metadata, leer clave directa de la sala (compatibilidad total)
                    if (string.IsNullOrEmpty(url))
                    {
                        var dRes = await _httpClient.GetStringAsync($"https://api.keyval.org/get/lowcord_{rName}", rCts.Token);
                        using var dDoc = JsonDocument.Parse(dRes);
                        if (dDoc.RootElement.TryGetProperty("val", out var dv) && !string.IsNullOrEmpty(dv.GetString()))
                        {
                            var dVal = dv.GetString()!;
                            if (!dVal.Equals("offline", StringComparison.OrdinalIgnoreCase) &&
                                dVal.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                url = dVal;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(url) && !url.Equals("offline", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(new RoomInfo
                        {
                            RoomId = rName,
                            TunnelUrl = url,
                            HasPassword = hasPassword,
                            HostUser = string.IsNullOrEmpty(host) ? "Anfitrión" : host,
                            UserCount = 1
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private async Task AddToRoomsIndexAsync(string roomName)
    {
        try
        {
            var currentRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var res = await _httpClient.GetStringAsync("https://api.keyval.org/get/lowcord_rooms_index");
                using var doc = JsonDocument.Parse(res);
                if (doc.RootElement.TryGetProperty("val", out var v) && !string.IsNullOrEmpty(v.GetString()))
                {
                    var names = v.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var n in names) if (!n.Equals("offline", StringComparison.OrdinalIgnoreCase)) currentRooms.Add(n);
                }
            }
            catch { }

            currentRooms.Add(roomName);
            var updated = string.Join(",", currentRooms);
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_rooms_index/{Uri.EscapeDataString(updated)}");
        }
        catch { }
    }

    private async Task RemoveFromRoomsIndexAsync(string roomName)
    {
        try
        {
            var currentRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var res = await _httpClient.GetStringAsync("https://api.keyval.org/get/lowcord_rooms_index");
                using var doc = JsonDocument.Parse(res);
                if (doc.RootElement.TryGetProperty("val", out var v) && !string.IsNullOrEmpty(v.GetString()))
                {
                    var names = v.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var n in names) if (!n.Equals("offline", StringComparison.OrdinalIgnoreCase)) currentRooms.Add(n);
                }
            }
            catch { }

            currentRooms.Remove(roomName);
            var updated = currentRooms.Count > 0 ? string.Join(",", currentRooms) : "offline";
            await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_rooms_index/{Uri.EscapeDataString(updated)}");
        }
        catch { }
    }
}
