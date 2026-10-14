using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Lowcord.Client.Models;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Bridge;

public class WebUiBridge
{
    private readonly WebView2 _webView;
    private readonly ISettingsService _settingsService;
    private readonly IServerProcessService _serverService;
    private readonly ITunnelService _tunnelService;
    private readonly IRoomRegistryService _roomRegistry;
    private readonly IHotkeyService _hotkeyService;
    private readonly IUpdateService _updateService;
    private readonly Action<Action> _uiDispatcher;
    private readonly Action _openHostWindowAction;

    public HostStatus HostStatus { get; } = new();

    public WebUiBridge(
        WebView2 webView,
        ISettingsService settingsService,
        IServerProcessService serverService,
        ITunnelService tunnelService,
        IRoomRegistryService roomRegistry,
        IHotkeyService hotkeyService,
        IUpdateService updateService,
        Action<Action> uiDispatcher,
        Action openHostWindowAction)
    {
        _webView = webView;
        _settingsService = settingsService;
        _serverService = serverService;
        _tunnelService = tunnelService;
        _roomRegistry = roomRegistry;
        _hotkeyService = hotkeyService;
        _updateService = updateService;
        _uiDispatcher = uiDispatcher;
        _openHostWindowAction = openHostWindowAction;
    }

    public void RenderLobby()
    {
        _uiDispatcher(() =>
        {
            var html = LobbyHtmlBuilder.BuildLobbyHtml(
                appVersion: "v2.8",
                username: _settingsService.Settings.Username,
                hostStatus: HostStatus,
                hostDefaultRoom: _settingsService.Settings.Channel,
                hostDefaultPassword: _settingsService.Settings.Password,
                rooms: new List<RoomInfo>(),
                pendingUpdateVersion: _updateService.PendingVersion
            );
            _webView.NavigateToString(html);

            // Cargar salas activas en segundo plano de forma no bloqueante
            _ = Task.Run(async () =>
            {
                var rooms = await _roomRegistry.GetActiveRoomsAsync();
                _uiDispatcher(() =>
                {
                    try
                    {
                        var payload = JsonSerializer.Serialize(new { type = "ROOMS_DATA", rooms });
                        _webView.CoreWebView2?.PostWebMessageAsJson(payload);
                    }
                    catch { }
                });
            });
        });
    }

    public async Task HandleWebMessageAsync(string msgString)
    {
        if (string.IsNullOrWhiteSpace(msgString)) return;

        if (msgString == "SHOW_LOBBY" || msgString == "CHANGE_SERVER" || msgString == "CHANGE_CHANNEL" || msgString == "SHOW_CHANNELS")
        {
            RenderLobby();
            return;
        }

        if (msgString == "OPEN_HOST_WINDOW" || msgString == "START_HOST_WINDOW" || msgString == "CREATE_SERVER")
        {
            _uiDispatcher(() => _openHostWindowAction());
            return;
        }

        if (msgString == "FETCH_ROOMS")
        {
            var rooms = await _roomRegistry.GetActiveRoomsAsync();
            _uiDispatcher(() =>
            {
                var payload = JsonSerializer.Serialize(new { type = "ROOMS_DATA", rooms });
                _webView.CoreWebView2?.PostWebMessageAsJson(payload);
            });
            return;
        }

        if (msgString.StartsWith("SET_NAME:"))
        {
            var newName = msgString["SET_NAME:".Length..].Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _settingsService.Settings.Username = newName;
                _settingsService.SaveSettings();
                RenderLobby();
            }
            return;
        }

        if (msgString.StartsWith("START_HOST:"))
        {
            var json = msgString["START_HOST:".Length..];
            await HandleStartHostAsync(json);
            return;
        }

        if (msgString == "STOP_HOST")
        {
            await HandleStopHostAsync();
            return;
        }

        if (msgString.StartsWith("JOIN_ROOM:"))
        {
            var json = msgString["JOIN_ROOM:".Length..];
            await HandleJoinRoomAsync(json);
            return;
        }

        if (msgString.StartsWith("JOIN_DIRECT:"))
        {
            var json = msgString["JOIN_DIRECT:".Length..];
            await HandleJoinDirectAsync(json);
            return;
        }

        if (msgString == "EXECUTE_UPDATE" || msgString == "START_UPDATE")
        {
            _ = Task.Run(async () =>
            {
                await _updateService.ExecuteUpdateAsync((percent, msg) =>
                {
                    _uiDispatcher(() =>
                    {
                        var payload = JsonSerializer.Serialize(new { type = "UPDATE_PROGRESS", percent, msg });
                        _webView.CoreWebView2?.PostWebMessageAsJson(payload);
                    });
                });
            });
            return;
        }
    }

    public void HandleJsonMessage(JsonElement root)
    {
        if (root.TryGetProperty("type", out var typeProp))
        {
            var type = typeProp.GetString();
            if (type == "UPDATE_HOTKEY")
            {
                var code = root.TryGetProperty("code", out var c) ? c.GetString() : "KeyM";
                uint vk = root.TryGetProperty("vk", out var v) && v.GetInt32() > 0 ? (uint)v.GetInt32() : 0;
                _hotkeyService.SetHotkey(code ?? "KeyM", vk);
                _settingsService.Settings.HotkeyCode = code ?? "KeyM";
                _settingsService.Settings.HotkeyVk = _hotkeyService.TargetVk;
                _settingsService.SaveSettings();
            }
        }
    }

    private async Task HandleStartHostAsync(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var room = root.GetProperty("room").GetString()!.Trim().ToLowerInvariant();
            var pass = root.TryGetProperty("pass", out var p) ? p.GetString()?.Trim() ?? "" : "";
            var user = root.TryGetProperty("user", out var u) ? u.GetString()?.Trim() ?? "Host" : "Host";

            _settingsService.Settings.Channel = room;
            _settingsService.Settings.Password = pass;
            _settingsService.Settings.IsHosting = true;
            _settingsService.SaveSettings();

            // 1. Iniciar Kestrel
            var serverOk = await _serverService.StartServerAsync(room);
            if (!serverOk)
            {
                _uiDispatcher(() =>
                {
                    System.Windows.Forms.MessageBox.Show(
                        "No se pudo iniciar Lowcord.Server.exe. Asegúrate de tener los archivos en la carpeta Servidor.",
                        "Error al Iniciar Host",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    RenderLobby();
                });
                return;
            }

            // 2. Iniciar Túnel Cloudflare
            var tunnelUrl = await _tunnelService.StartTunnelAsync(8080);

            // 3. Registrar en Keyval
            if (!string.IsNullOrEmpty(tunnelUrl))
            {
                await _roomRegistry.RegisterRoomAsync(room, tunnelUrl, !string.IsNullOrEmpty(pass), user);
            }

            HostStatus.IsRunning = true;
            HostStatus.RoomName = room;
            HostStatus.TunnelUrl = tunnelUrl;
            HostStatus.Message = "Servidor online";

            // 4. Auto-unión inmediata del anfitrión a la llamada local
            var hostCallUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(room)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
            _uiDispatcher(() =>
            {
                if (Uri.TryCreate(hostCallUrl, UriKind.Absolute, out var uri))
                {
                    _webView.Source = uri;
                }
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher(() =>
            {
                System.Windows.Forms.MessageBox.Show($"Error al iniciar host: {ex.Message}", "Error");
                RenderLobby();
            });
        }
    }

    private async Task HandleStopHostAsync()
    {
        if (HostStatus.IsRunning && !string.IsNullOrEmpty(HostStatus.RoomName))
        {
            await _roomRegistry.UnregisterRoomAsync(HostStatus.RoomName);
        }

        await _tunnelService.StopTunnelAsync();
        await _serverService.StopServerAsync();

        HostStatus.IsRunning = false;
        HostStatus.RoomName = "";
        HostStatus.TunnelUrl = null;
        HostStatus.Message = "";

        _settingsService.Settings.IsHosting = false;
        _settingsService.SaveSettings();

        RenderLobby();
    }

    private async Task HandleJoinRoomAsync(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var room = root.GetProperty("room").GetString()!.Trim().ToLowerInvariant();
            var user = root.GetProperty("user").GetString()!.Trim();
            var pass = root.TryGetProperty("pass", out var p) ? p.GetString()?.Trim() ?? "" : "";
            var directUrl = root.TryGetProperty("url", out var u) ? u.GetString() : null;

            var targetUrl = directUrl;
            if (string.IsNullOrEmpty(targetUrl))
            {
                targetUrl = await _roomRegistry.ResolveRoomToUrlAsync(room);
            }

            if (string.IsNullOrEmpty(targetUrl))
            {
                _uiDispatcher(() =>
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"No se pudo encontrar el servidor #{room}. Asegúrate de que el anfitrión esté en línea.",
                        "Servidor No Disponible",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                });
                return;
            }

            var sep = targetUrl.Contains('?') ? "&" : "?";
            var finalCallUrl = $"{targetUrl}{sep}room={Uri.EscapeDataString(room)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";

            _uiDispatcher(() =>
            {
                if (Uri.TryCreate(finalCallUrl, UriKind.Absolute, out var fullUri))
                {
                    _webView.Source = fullUri;
                }
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher(() =>
            {
                System.Windows.Forms.MessageBox.Show($"Error al conectar: {ex.Message}", "Error");
            });
        }
    }

    private async Task HandleJoinDirectAsync(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var target = root.GetProperty("target").GetString()!.Trim();
            var user = root.GetProperty("user").GetString()!.Trim();

            var resolvedUrl = await _roomRegistry.ResolveRoomToUrlAsync(target);
            if (string.IsNullOrEmpty(resolvedUrl))
            {
                _uiDispatcher(() =>
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"No se pudo resolver '{target}'. Puede ser una IP no alcanzable o un servidor fuera de línea.",
                        "Conexión Fallida",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                });
                return;
            }

            var roomName = target.Contains('/') || target.Contains(':') ? "general" : target.ToLowerInvariant();
            var sep = resolvedUrl.Contains('?') ? "&" : "?";
            var finalCallUrl = $"{resolvedUrl}{sep}room={Uri.EscapeDataString(roomName)}&user={Uri.EscapeDataString(user)}&autojoin=true";

            _uiDispatcher(() =>
            {
                if (Uri.TryCreate(finalCallUrl, UriKind.Absolute, out var fullUri))
                {
                    _webView.Source = fullUri;
                }
            });
        }
        catch { }
    }

    public void UpdateHostStatus(HostStatus status)
    {
        HostStatus.IsRunning = status.IsRunning;
        HostStatus.RoomName = status.RoomName;
        HostStatus.TunnelUrl = status.TunnelUrl;
        HostStatus.Message = status.Message;
        RenderLobby();
    }

    public void JoinLocalHostCall(string room, string pass)
    {
        var user = _settingsService.Settings.Username;
        var finalUrl = $"http://127.0.0.1:8080/?room={Uri.EscapeDataString(room)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
        _uiDispatcher(() =>
        {
            if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri))
            {
                _webView.Source = uri;
            }
        });
    }
}
