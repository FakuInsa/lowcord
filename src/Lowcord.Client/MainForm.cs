using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lowcord.Client;

public class ClientSettings
{
    public string Username { get; set; } = "";
    public string ServerCode { get; set; } = "facu";
    public string Channel { get; set; } = "general";
    public string Password { get; set; } = "";
    public bool IsHosting { get; set; } = false;
    public uint HotkeyVk { get; set; } = (uint)Keys.M;
    public string HotkeyCode { get; set; } = "KeyM";
}

public class MainForm : Form
{
    public const string APP_VERSION = "v2.6";

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _hookId = IntPtr.Zero;
    private uint _targetVk = (uint)Keys.M;
    private bool _isTargetKeyDown = false;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

    private WebView2 _webView = null!;
    private readonly string _settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_config.json");
    private readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.txt");
    private ClientSettings _settings = new();

    private System.Windows.Forms.Timer? _retryTimer;
    private string? _pendingUpdateUrl = null;
    private string? _pendingUpdateVersion = null;
    private string? _cachedTunnelUrl = null;
    private ServerHostForm? _serverHostForm;

    public MainForm()
    {
        try
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal;
            CleanUpOldUpdates();
        }
        catch { }

        LoadSettings();
        if (_settings.HotkeyVk > 0)
        {
            _targetVk = _settings.HotkeyVk;
        }

        InitializeComponent();
        InstallGlobalKeyboardHook();

        // SOLO iniciar servidor si el usuario eligió explícitamente ser Anfitrión
        if (_settings.IsHosting)
        {
            _ = StartHostProcessesAsync();
        }

        _ = InitializeWebViewAsync();
    }

    private void CleanUpOldUpdates()
    {
        try
        {
            var currentExe = Environment.ProcessPath ?? Application.ExecutablePath;
            var oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);

            var dir = Path.GetDirectoryName(currentExe);
            if (!string.IsNullOrEmpty(dir))
            {
                foreach (var tmp in Directory.GetFiles(dir, "*.tmp"))
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }
        catch { }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var loaded = JsonSerializer.Deserialize<ClientSettings>(json);
                if (loaded != null) _settings = loaded;
            }
            else if (File.Exists(_configFile))
            {
                var saved = File.ReadAllText(_configFile).Trim();
                if (!string.IsNullOrEmpty(saved))
                {
                    _settings.ServerCode = saved;
                }
            }

            var localRoom = GetLocalRoomCode();
            if (!string.IsNullOrEmpty(localRoom) && string.IsNullOrEmpty(_settings.ServerCode))
            {
                _settings.ServerCode = localRoom;
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
            File.WriteAllText(_configFile, _settings.ServerCode);
        }
        catch { }
    }

    private void InitializeComponent()
    {
        Text = $"Lowcord {APP_VERSION} - Cliente Ultra-Ligero P2P";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 31, 34);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_webView);

        // Tecla F2 para volver a la pantalla de canales en cualquier momento
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                ShowConnectScreen();
            }
        };
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var options = new CoreWebView2EnvironmentOptions(
                "--autoplay-policy=no-user-gesture-required " +
                "--disable-features=Translate,OptimizationHints,MediaRouter " +
                "--enable-gpu-rasterization " +
                "--enable-zero-copy " +
                "--ignore-gpu-blocklist " +
                "--enable-hardware-overlays"
            );
            var env = await CoreWebView2Environment.CreateAsync(null, null, options);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.PermissionRequested += (sender, args) =>
            {
                if (args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                    args.PermissionKind == CoreWebView2PermissionKind.Camera)
                {
                    args.State = CoreWebView2PermissionState.Allow;
                }
            };

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Escuchar mensajes del frontend
            _webView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                try
                {
                    string? msgString = null;
                    try
                    {
                        msgString = args.TryGetWebMessageAsString();
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(msgString))
                    {
                        if (msgString == "CHANGE_SERVER" || msgString == "CHANGE_CHANNEL" || msgString == "SHOW_CHANNELS")
                        {
                            BeginInvoke(() => ShowConnectScreen());
                            return;
                        }
                        if (msgString == "FETCH_CHANNELS")
                        {
                            _ = Task.Run(async () =>
                            {
                                string json = "[]";
                                try
                                {
                                    if (_settings.IsHosting && await IsLocalServerRunningAsync())
                                    {
                                        json = await FetchChannelsJsonAsync("http://localhost:8080");
                                    }
                                    else if (!string.IsNullOrWhiteSpace(_settings.ServerCode))
                                    {
                                        var resolved = await ResolveInputToUrlAsync(_settings.ServerCode);
                                        if (!string.IsNullOrEmpty(resolved))
                                        {
                                            json = await FetchChannelsJsonAsync(resolved);
                                        }
                                    }
                                }
                                catch { }

                                BeginInvoke(() =>
                                {
                                    try
                                    {
                                        _webView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new
                                        {
                                            type = "CHANNELS_DATA",
                                            channels = JsonSerializer.Deserialize<JsonElement>(json)
                                        }));
                                    }
                                    catch { }
                                });
                            });
                            return;
                        }
                        if (msgString == "EXIT_APP")
                        {
                            BeginInvoke(() => Application.Exit());
                            return;
                        }
                        if (msgString == "OPEN_SERVER_HOST_WINDOW" || msgString == "START_HOSTING")
                        {
                            BeginInvoke(() =>
                            {
                                if (_serverHostForm == null || _serverHostForm.IsDisposed)
                                {
                                    _serverHostForm = new ServerHostForm(_settings.ServerCode, this);
                                    _serverHostForm.Show();
                                }
                                else
                                {
                                    _serverHostForm.BringToFront();
                                    _serverHostForm.WindowState = FormWindowState.Normal;
                                }
                            });
                            return;
                        }
                        if (msgString == "STOP_HOSTING")
                        {
                            BeginInvoke(async () =>
                            {
                                await StopHostingAsync();
                                ShowConnectScreen();
                            });
                            return;
                        }
                        if (msgString == "EXECUTE_UPDATE" || msgString == "START_UPDATE")
                        {
                            BeginInvoke(async () => await ExecuteUpdateAsync());
                            return;
                        }
                        if (msgString.StartsWith("SET_NAME:"))
                        {
                            var newName = msgString.Substring("SET_NAME:".Length).Trim();
                            if (!string.IsNullOrEmpty(newName))
                            {
                                _settings.Username = newName;
                                SaveSettings();
                                BeginInvoke(() => ShowConnectScreen());
                            }
                            return;
                        }
                        if (msgString.StartsWith("SET_SERVER_CODE:"))
                        {
                            var newCode = msgString.Substring("SET_SERVER_CODE:".Length).Trim().ToLowerInvariant();
                            if (!string.IsNullOrEmpty(newCode))
                            {
                                _settings.ServerCode = newCode;
                                SaveSettings();
                                BeginInvoke(() => ShowConnectScreen());
                            }
                            return;
                        }
                        if (msgString.StartsWith("CONNECT_CHANNEL:"))
                        {
                            var jsonPayload = msgString.Substring("CONNECT_CHANNEL:".Length).Trim();
                            try
                            {
                                using var doc = JsonDocument.Parse(jsonPayload);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("user", out var u) && !string.IsNullOrEmpty(u.GetString()))
                                {
                                    _settings.Username = u.GetString()!.Trim();
                                }
                                if (root.TryGetProperty("server", out var s) && !string.IsNullOrEmpty(s.GetString()))
                                {
                                    _settings.ServerCode = s.GetString()!.Trim().ToLowerInvariant();
                                }
                                _settings.Channel = (root.TryGetProperty("channel", out var c) ? c.GetString() ?? "general" : "general").Trim().ToLowerInvariant();
                                _settings.Password = root.TryGetProperty("pass", out var p) ? p.GetString() ?? "" : "";
                                SaveSettings();

                                BeginInvoke(async () => await ConnectToChannelAsync());
                            }
                            catch { }
                            return;
                        }
                    }

                    string? rawJson = null;
                    if (!string.IsNullOrEmpty(msgString) && msgString.TrimStart().StartsWith("{"))
                    {
                        rawJson = msgString;
                    }
                    else
                    {
                        try { rawJson = args.WebMessageAsJson; } catch { }
                    }

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        using var doc = JsonDocument.Parse(rawJson);
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            if (root.TryGetProperty("type", out var typeProp))
                            {
                                var type = typeProp.GetString();
                                if (type == "UPDATE_HOTKEY")
                                {
                                    if (root.TryGetProperty("vk", out var vkProp) && vkProp.GetInt32() > 0)
                                    {
                                        _targetVk = (uint)vkProp.GetInt32();
                                        _settings.HotkeyVk = _targetVk;
                                    }
                                    if (root.TryGetProperty("code", out var codeProp))
                                    {
                                        var code = codeProp.GetString();
                                        _settings.HotkeyCode = code ?? "KeyM";
                                        if (_targetVk == 0) UpdateGlobalHotkey(code);
                                    }
                                    SaveSettings();
                                }
                                else if (type == "EXECUTE_UPDATE")
                                {
                                    BeginInvoke(async () => await ExecuteUpdateAsync());
                                }
                            }
                        }
                    }
                }
                catch { }
            };

            _webView.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowWaitingScreen(_settings.ServerCode, _settings.Channel);
                }
            };

            _ = Task.Run(async () => await CheckForUpdatesAsync());
            ShowConnectScreen();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo inicializar WebView2. Asegúrate de tener Windows 10/11 actualizado.\nDetalle: {ex.Message}",
                "Error en Lowcord",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private async Task StartHostingAsync()
    {
        _settings.IsHosting = true;
        SaveSettings();
        await StartHostProcessesAsync();
    }

    private async Task StopHostingAsync()
    {
        _settings.IsHosting = false;
        SaveSettings();

        await Task.Run(() =>
        {
            try
            {
                var cfProcs = System.Diagnostics.Process.GetProcessesByName("cloudflared");
                foreach (var p in cfProcs) { try { p.Kill(); } catch { } }

                var srvProcs = System.Diagnostics.Process.GetProcessesByName("Lowcord.Server");
                foreach (var p in srvProcs) { try { p.Kill(); } catch { } }
            }
            catch { }
        });
    }

    private async Task StartHostProcessesAsync()
    {
        var serverExe = FindServerExecutable();
        if (string.IsNullOrEmpty(serverExe) || !File.Exists(serverExe))
        {
            return;
        }

        // 1. Iniciar Lowcord.Server si no está escuchando
        if (!await IsLocalServerRunningAsync())
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = serverExe,
                    WorkingDirectory = Path.GetDirectoryName(serverExe)!,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        // 2. Iniciar cloudflared tunnel
        var cloudflaredExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.exe");
        if (File.Exists(cloudflaredExe))
        {
            var cfLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.log");
            try
            {
                var runningCf = System.Diagnostics.Process.GetProcessesByName("cloudflared");
                if (runningCf.Length == 0)
                {
                    var cfPsi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = cloudflaredExe,
                        Arguments = $"tunnel --url http://localhost:8080 --logfile \"{cfLog}\"",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(cfPsi);
                }
            }
            catch { }

            _ = Task.Run(async () =>
            {
                var roomCode = string.IsNullOrWhiteSpace(_settings.ServerCode) ? "facu" : _settings.ServerCode;
                for (int i = 0; i < 40; i++)
                {
                    await Task.Delay(800);
                    if (File.Exists(cfLog))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(cfLog);
                            var match = System.Text.RegularExpressions.Regex.Match(content, @"(https://[a-zA-Z0-9-]+\.trycloudflare\.com)");
                            if (match.Success)
                            {
                                _cachedTunnelUrl = match.Groups[1].Value;
                                var encoded = Uri.EscapeDataString(_cachedTunnelUrl);
                                await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_{roomCode}/{encoded}");
                                break;
                            }
                        }
                        catch { }
                    }
                }
            });
        }
    }

    public async Task<bool> IsLocalServerRunningAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            var resp = await _httpClient.GetAsync("http://localhost:8080/api/health", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> ResolveInputToUrlAsync(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;

        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        if (input.StartsWith("127.0.0.1") || (input.Contains(':') && !input.Contains(' ')))
        {
            return "http://" + input;
        }

        // Si este equipo es el host actualmente y coincide con la sala
        if (_settings.IsHosting && await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(input))
        {
            return "http://localhost:8080";
        }

        var code = input.ToLowerInvariant();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
            var res = await _httpClient.GetStringAsync($"https://api.keyval.org/get/lowcord_{code}", cts.Token);
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("val", out var valProp))
            {
                var val = valProp.GetString();
                if (!string.IsNullOrWhiteSpace(val) &&
                    (val.StartsWith("http://") || val.StartsWith("https://")) &&
                    !val.Equals("offline", StringComparison.OrdinalIgnoreCase))
                {
                    return val;
                }
            }
        }
        catch { }

        return null;
    }

    private async Task<string> FetchChannelsJsonAsync(string baseUrl)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var cleanBase = baseUrl.TrimEnd('/');
            var json = await _httpClient.GetStringAsync($"{cleanBase}/api/channels", cts.Token);
            return json;
        }
        catch
        {
            return "[]";
        }
    }

    private async void ShowConnectScreen()
    {
        StopAutoRetry();

        // 1. Si no tiene nombre guardado, pedir nombre (solo una vez para siempre)
        if (string.IsNullOrWhiteSpace(_settings.Username))
        {
            ShowAskNameScreen();
            return;
        }

        var hasHostFiles = !string.IsNullOrEmpty(FindServerExecutable());
        var isHostingNow = _settings.IsHosting && (await IsLocalServerRunningAsync());
        var serverCode = System.Net.WebUtility.HtmlEncode(_settings.ServerCode);
        var safeUser = System.Net.WebUtility.HtmlEncode(_settings.Username);

        // Obtener canales del servidor correspondiente
        string channelsJson = "[]";
        if (isHostingNow)
        {
            channelsJson = await FetchChannelsJsonAsync("http://localhost:8080");
        }
        else if (!string.IsNullOrWhiteSpace(_settings.ServerCode))
        {
            var resolved = await ResolveInputToUrlAsync(_settings.ServerCode);
            if (!string.IsNullOrEmpty(resolved))
            {
                channelsJson = await FetchChannelsJsonAsync(resolved);
            }
        }

        var updateHtml = "";
        if (!string.IsNullOrEmpty(_pendingUpdateVersion))
        {
            updateHtml = $@"
        <div class='update-alert'>
          <span>🚀 <b>Nueva versión disponible: {_pendingUpdateVersion}</b></span>
          <button type='button' class='btn-mini-update' onclick='window.chrome.webview.postMessage(""EXECUTE_UPDATE"")'>Actualizar Ahora</button>
        </div>";
        }

        var shareLink = $"https://fakuinsa.github.io/lowcord/?room={_settings.ServerCode}";

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Canales de Lowcord</title>
  <style>
    * {{ box-sizing: border-box; margin: 0; padding: 0; }}
    body {{
      background: linear-gradient(135deg, #111214 0%, #1e1f22 100%);
      color: #f2f3f5;
      font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      user-select: none;
      padding: 16px;
    }}
    .card {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 24px 26px;
      width: 480px;
      max-width: 95vw;
      box-shadow: 0 16px 40px rgba(0,0,0,0.5);
      position: relative;
    }}
    .header-row {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
      padding-bottom: 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }}
    .brand {{
      display: flex;
      align-items: center;
      gap: 10px;
    }}
    .brand-logo {{
      width: 36px;
      height: 36px;
      background: linear-gradient(135deg, #5865F2 0%, #4752c4 100%);
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
    }}
    .brand-logo svg {{ width: 20px; height: 20px; fill: #fff; }}
    .brand-title {{
      font-size: 18px;
      font-weight: 700;
      color: #fff;
    }}
    .header-actions {{
      display: flex;
      align-items: center;
      gap: 8px;
    }}
    .user-pill {{
      display: flex;
      align-items: center;
      gap: 6px;
      background: #1e1f22;
      padding: 6px 12px;
      border-radius: 20px;
      font-size: 13px;
      font-weight: 600;
      color: #dbdee1;
    }}
    .btn-gear {{
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #dbdee1;
      padding: 6px 10px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 14px;
      transition: background 0.15s, border-color 0.15s;
      display: flex;
      align-items: center;
      justify-content: center;
    }}
    .btn-gear:hover {{
      background: #35373c;
      border-color: rgba(255, 255, 255, 0.2);
    }}

    .server-status-bar {{
      display: flex;
      align-items: center;
      gap: 8px;
      background: rgba(0, 0, 0, 0.2);
      border-radius: 8px;
      padding: 8px 12px;
      margin-bottom: 16px;
      font-size: 12.5px;
      color: #949ba4;
    }}
    .status-dot {{
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: {(isHostingNow ? "#23a55a" : "#f0b232")};
    }}
    .status-text {{
      color: #dbdee1;
      font-weight: 500;
    }}
    .status-link {{
      margin-left: auto;
      color: #5865F2;
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
      font-size: 12px;
    }}
    .status-link:hover {{ text-decoration: underline; }}

    .section-title {{
      font-size: 11.5px;
      font-weight: 700;
      color: #949ba4;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 10px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }}
    
    .channels-list {{
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 16px;
      max-height: 200px;
      overflow-y: auto;
    }}
    .no-channels-box {{
      background: #1e1f22;
      border: 1px dashed rgba(255, 255, 255, 0.1);
      border-radius: 10px;
      padding: 24px 16px;
      text-align: center;
    }}
    .channel-item {{
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 8px;
      padding: 11px 14px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      cursor: pointer;
      transition: all 0.15s ease;
    }}
    .channel-item:hover {{
      background: #35373c;
      border-color: rgba(255, 255, 255, 0.15);
      transform: translateX(2px);
    }}
    .channel-name {{
      font-weight: 600;
      font-size: 14.5px;
      color: #fff;
      display: flex;
      align-items: center;
      gap: 7px;
    }}
    .channel-badge {{
      font-size: 12px;
      color: #949ba4;
    }}
    .channel-badge.active {{
      color: #23a55a;
      font-weight: 600;
    }}
    .btn-join-ch {{
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 5px 12px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}

    .create-box {{
      background: #1e1f22;
      border-radius: 10px;
      padding: 14px;
      margin-top: 4px;
    }}
    .create-title {{
      font-size: 12.5px;
      font-weight: 600;
      color: #dbdee1;
      margin-bottom: 8px;
    }}
    .create-form {{
      display: flex;
      flex-direction: column;
      gap: 8px;
    }}
    .form-inputs-row {{
      display: flex;
      gap: 8px;
    }}
    .input-ch {{
      flex: 1;
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 6px;
      color: #fff;
      padding: 8px 10px;
      font-size: 13px;
      outline: none;
    }}
    .input-ch:focus {{ border-color: #5865F2; }}
    .input-pass {{
      max-width: 140px;
    }}
    .btn-join-main {{
      width: 100%;
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 9px;
      border-radius: 6px;
      font-size: 13.5px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s;
    }}
    .btn-join-main:hover {{ background: #4752c4; }}

    /* Modal Popup de Configuración y Host */
    .modal-backdrop {{
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 999;
      animation: fadeIn 0.15s ease-out;
    }}
    .modal-window {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 14px;
      width: 440px;
      max-width: 92vw;
      padding: 22px 24px;
      box-shadow: 0 20px 50px rgba(0,0,0,0.6);
    }}
    .modal-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
      padding-bottom: 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }}
    .modal-header h3 {{
      font-size: 16px;
      font-weight: 700;
      color: #fff;
    }}
    .btn-close-modal {{
      background: transparent;
      border: none;
      color: #949ba4;
      font-size: 22px;
      line-height: 1;
      cursor: pointer;
    }}
    .btn-close-modal:hover {{ color: #fff; }}
    .modal-section {{
      margin-bottom: 14px;
    }}
    .modal-label {{
      display: block;
      font-size: 12px;
      font-weight: 700;
      color: #b5bac1;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 6px;
    }}
    .modal-row {{
      display: flex;
      gap: 8px;
    }}
    .modal-input {{
      flex: 1;
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 6px;
      color: #fff;
      padding: 7px 10px;
      font-size: 13px;
      outline: none;
    }}
    .modal-input:focus {{ border-color: #5865F2; }}
    .btn-modal-action {{
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .btn-modal-action:hover {{ background: #4752c4; }}
    .modal-hint {{
      display: block;
      font-size: 11.5px;
      color: #949ba4;
      margin-top: 5px;
    }}
    .modal-divider {{
      height: 1px;
      background: rgba(255, 255, 255, 0.08);
      margin: 16px 0;
    }}
    .modal-desc {{
      font-size: 12.5px;
      color: #dbdee1;
      line-height: 1.4;
    }}
    .btn-modal-host {{
      background: #23a55a;
      color: #fff;
      border: none;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .btn-modal-host:hover {{ background: #1d8a4b; }}
    .btn-modal-danger {{
      background: #da373c;
      color: #fff;
      border: none;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .btn-modal-danger:hover {{ background: #a1282c; }}
    .btn-modal-share {{
      background: #4e5058;
      color: #fff;
      border: none;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .btn-modal-share:hover {{ background: #6d6f78; }}
    .modal-footer {{
      display: flex;
      justify-content: flex-end;
      margin-top: 18px;
    }}
    .btn-modal-close {{
      background: #35373c;
      color: #dbdee1;
      border: none;
      padding: 7px 16px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .btn-modal-close:hover {{ background: #4e5058; }}
    
    .update-alert {{
      background: rgba(88, 101, 242, 0.2);
      border: 1px solid #5865F2;
      border-radius: 8px;
      padding: 8px 12px;
      margin-bottom: 14px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 12.5px;
    }}
    .btn-mini-update {{
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 11.5px;
      font-weight: 600;
      cursor: pointer;
    }}
    .footer-note {{
      text-align: center;
      color: #80848e;
      font-size: 11px;
      margin-top: 14px;
    }}
    @keyframes fadeIn {{
      from {{ opacity: 0; transform: scale(0.97); }}
      to {{ opacity: 1; transform: scale(1); }}
    }}
  </style>
</head>
<body>
  <div class='card'>
    {updateHtml}

    <div class='header-row'>
      <div class='brand'>
        <div class='brand-logo'>
          <svg viewBox='0 0 24 24'><path d='M12 3a9 9 0 0 0-9 9v7a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H5v-3a7 7 0 1 1 14 0v3h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-7a9 9 0 0 0-9-9z'/></svg>
        </div>
        <div class='brand-title'>Lowcord</div>
      </div>
      <div class='header-actions'>
        <div class='user-pill' title='Tu Apodo'>
          <span>👤 {safeUser}</span>
        </div>
        <button type='button' class='btn-gear' onclick='openSettingsModal()' title='Configuración y Servidor'>
          ⚙️
        </button>
      </div>
    </div>

    <!-- Indicador sutil de servidor -->
    <div class='server-status-bar'>
      <div class='status-dot'></div>
      <span class='status-text'>{(isHostingNow ? "Alojando en tu PC (Modo Anfitrión)" : $"Servidor actual: <b>{serverCode}</b>")}</span>
      <span class='status-link' onclick='openSettingsModal()'>Cambiar en ⚙️</span>
    </div>

    <!-- Lista de Canales -->
    <div class='section-title'>
      <span>Canales de Voz</span>
      <span style='cursor: pointer; color: #5865F2;' onclick='fetchChannelsNow()'>↻ Actualizar</span>
    </div>

    <div class='channels-list' id='channelsContainer'>
      <!-- Renderizado por JS -->
    </div>

    <!-- Crear o entrar a un canal -->
    <div class='create-box'>
      <div class='create-title'>+ Crear o Entrar a un Canal:</div>
      <form onsubmit='createAndJoin(event)' class='create-form'>
        <div class='form-inputs-row'>
          <input type='text' id='customChName' class='input-ch' placeholder='Nombre del canal (ej: tarkov)' required autocomplete='off'>
          <input type='password' id='customChPass' class='input-ch input-pass' placeholder='Contraseña (opcional)' autocomplete='off'>
        </div>
        <button type='submit' id='btnJoinMain' class='btn-join-main'>Conectar al Canal</button>
      </form>
    </div>

    <div class='footer-note'>
      {APP_VERSION} • Atajo global (M) para mutearte • Pulsa F2 para cambiar de canal
    </div>
  </div>

  <!-- Modal Popup de Configuración y Host -->
  <div id='settingsModal' class='modal-backdrop' style='display: none;'>
    <div class='modal-window'>
      <div class='modal-header'>
        <h3>Configuración y Servidor</h3>
        <button type='button' class='btn-close-modal' onclick='closeSettingsModal()'>&times;</button>
      </div>

      <div class='modal-section'>
        <label class='modal-label'>Tu Apodo / Nombre:</label>
        <div class='modal-row'>
          <input type='text' id='modalNameInput' class='modal-input' value='{safeUser}' maxlength='25' autocomplete='off'>
          <button type='button' class='btn-modal-action' onclick='saveNameFromModal()'>Guardar</button>
        </div>
        <small class='modal-hint'>Tu nombre se guarda para siempre y no se vuelve a pedir.</small>
      </div>

      <div class='modal-section'>
        <label class='modal-label'>Servidor para Conectarte (Amigo):</label>
        <div class='modal-row'>
          <input type='text' id='modalServerInput' class='modal-input' value='{serverCode}' autocomplete='off'>
          <button type='button' class='btn-modal-action' onclick='saveServerFromModal()'>Cambiar</button>
        </div>
        <small class='modal-hint'>Escribe el código del amigo que está alojando el servidor (ej: facu).</small>
      </div>

      {(hasHostFiles ? $@"
      <div class='modal-divider'></div>
      <div class='modal-section'>
        <label class='modal-label'>Modo Anfitrión (Crear tu propio Servidor):</label>
        <p class='modal-desc'>Abre la consola en una ventana separada para iniciar el servidor y ver los comandos en vivo estilo CMD.</p>
        <div style='margin-top: 10px;'>
          <button type='button' class='btn-modal-host' onclick='openHostWindow()'>🖥️ Iniciar Servidor (Abrir Consola)</button>
        </div>
      </div>" : "")}

      <div class='modal-footer'>
        <button type='button' class='btn-modal-close' onclick='closeSettingsModal()'>Cerrar</button>
      </div>
    </div>
  </div>

  <script>
    var currentChannels = {channelsJson};
    var currentUser = '{safeUser}';
    var currentServer = '{serverCode}';

    function renderChannels(channels) {{
      if (channels !== undefined) {{
        currentChannels = channels;
      }}
      var container = document.getElementById('channelsContainer');
      container.innerHTML = '';

      var list = (currentChannels && Array.isArray(currentChannels)) ? currentChannels : [];

      if (list.length === 0) {{
        container.innerHTML = `
          <div class='no-channels-box'>
            <div style='font-size: 26px; margin-bottom: 4px;'>🎧</div>
            <div style='font-weight: 600; color: #dbdee1;'>No hay canales creados aún</div>
            <div style='font-size: 12px; color: #949ba4; margin-top: 2px;'>Escribe un nombre abajo para crear un canal y conectarte.</div>
          </div>
        `;
        return;
      }}

      list.forEach(function(ch) {{
        var item = document.createElement('div');
        item.className = 'channel-item';

        var userLabel = ch.userCount > 0
          ? (ch.userCount + ' conectado' + (ch.userCount > 1 ? 's' : '') + ': ' + (ch.users || []).join(', '))
          : 'Canal libre';
        var badgeClass = ch.userCount > 0 ? 'channel-badge active' : 'channel-badge';
        var lockIcon = ch.hasPassword ? ' 🔒' : '';

        item.innerHTML = `
          <div>
            <div class='channel-name'>🔊 #${{ch.roomId}}${{lockIcon}}</div>
            <div class='${{badgeClass}}'>${{userLabel}}</div>
          </div>
          <button type='button' class='btn-join-ch'>Entrar</button>
        `;

        item.addEventListener('click', function() {{
          var pass = '';
          if (ch.hasPassword) {{
            pass = prompt('Ingresa la contraseña para #' + ch.roomId + ':') || '';
          }}
          var btn = item.querySelector('.btn-join-ch');
          if (btn) {{
            btn.disabled = true;
            btn.innerText = 'Entrando...';
          }}
          joinChannel(ch.roomId, pass);
        }});

        container.appendChild(item);
      }});
    }}

    function joinChannel(channelName, pass) {{
      var payload = JSON.stringify({{
        user: currentUser,
        server: currentServer,
        channel: channelName,
        pass: pass || ''
      }});
      window.chrome.webview.postMessage('CONNECT_CHANNEL:' + payload);
    }}

    function createAndJoin(e) {{
      e.preventDefault();
      var ch = (document.getElementById('customChName').value || '').trim().toLowerCase();
      var pass = (document.getElementById('customChPass').value || '').trim();
      if (ch) {{
        var btn = document.getElementById('btnJoinMain');
        if (btn) {{
          btn.disabled = true;
          btn.innerText = 'Creando...';
        }}
        joinChannel(ch, pass);
      }}
    }}

    function openHostWindow() {{
      closeSettingsModal();
      if (window.chrome && window.chrome.webview) {{
        window.chrome.webview.postMessage('OPEN_SERVER_HOST_WINDOW');
      }}
    }}

    function openSettingsModal() {{
      document.getElementById('settingsModal').style.display = 'flex';
    }}

    function closeSettingsModal() {{
      document.getElementById('settingsModal').style.display = 'none';
    }}

    document.getElementById('settingsModal').addEventListener('click', function(e) {{
      if (e.target === this) closeSettingsModal();
    }});

    function saveNameFromModal() {{
      var n = (document.getElementById('modalNameInput').value || '').trim();
      if (n) {{
        window.chrome.webview.postMessage('SET_NAME:' + n);
      }}
    }}

    function saveServerFromModal() {{
      var s = (document.getElementById('modalServerInput').value || '').trim().toLowerCase();
      if (s) {{
        window.chrome.webview.postMessage('SET_SERVER_CODE:' + s);
      }}
    }}

    function fetchChannelsNow() {{
      if (window.chrome && window.chrome.webview) {{
        window.chrome.webview.postMessage('FETCH_CHANNELS');
      }}
    }}

    function copyShare() {{
      var link = '{shareLink}';
      navigator.clipboard.writeText(link).then(function() {{
        alert('Enlace copiado al portapapeles:\n' + link + '\n\nPásaselo a tus amigos para que entren directo.');
      }}).catch(function() {{
        prompt('Copia este enlace para tus amigos:', link);
      }});
    }}

    // Auto-actualizar canales cada 3 segundos
    setInterval(function() {{
      fetchChannelsNow();
    }}, 3000);

    if (window.chrome && window.chrome.webview) {{
      window.chrome.webview.addEventListener('message', function(event) {{
        try {{
          var data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
          if (data && data.type === 'CHANNELS_DATA') {{
            renderChannels(data.channels);
          }}
        }} catch (e) {{}}
      }});
    }}

    renderChannels();
  </script>
</body>
</html>";

        _webView.NavigateToString(html);
    }

    private void ShowAskNameScreen()
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Bienvenido a Lowcord</title>
  <style>
    * {{ box-sizing: border-box; margin: 0; padding: 0; }}
    body {{
      background: linear-gradient(135deg, #111214 0%, #1e1f22 100%);
      color: #f2f3f5;
      font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      user-select: none;
      padding: 20px;
    }}
    .card {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 38px 34px;
      width: 400px;
      max-width: 95vw;
      text-align: center;
      box-shadow: 0 16px 40px rgba(0,0,0,0.5);
    }}
    .logo {{
      width: 60px;
      height: 60px;
      background: linear-gradient(135deg, #5865F2 0%, #4752c4 100%);
      border-radius: 18px;
      margin: 0 auto 18px auto;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 6px 20px rgba(88, 101, 242, 0.35);
    }}
    .logo svg {{ width: 34px; height: 34px; fill: #fff; }}
    h2 {{
      margin-bottom: 8px;
      font-size: 22px;
      font-weight: 700;
      color: #fff;
    }}
    p {{
      color: #949ba4;
      font-size: 13.5px;
      line-height: 1.4;
      margin-bottom: 22px;
    }}
    .input-box {{
      width: 100%;
      padding: 12px 14px;
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 8px;
      color: #fff;
      font-size: 15px;
      font-weight: 600;
      text-align: center;
      margin-bottom: 18px;
      outline: none;
    }}
    .input-box:focus {{ border-color: #5865F2; }}
    .btn {{
      width: 100%;
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 12px;
      border-radius: 8px;
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s;
    }}
    .btn:hover {{ background: #4752c4; }}
  </style>
</head>
<body>
  <div class='card'>
    <div class='logo'>
      <svg viewBox='0 0 24 24'><path d='M12 3a9 9 0 0 0-9 9v7a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H5v-3a7 7 0 1 1 14 0v3h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-7a9 9 0 0 0-9-9z'/></svg>
    </div>
    <h2>¿Cómo te llamas?</h2>
    <p>Ingresa tu apodo o nombre para comenzar (solo te lo pediremos esta vez):</p>
    <form onsubmit='submitName(event)'>
      <input type='text' id='nameInput' class='input-box' placeholder='Tu Apodo (ej: Faku)' required autofocus maxlength='25' autocomplete='off'>
      <button type='submit' class='btn'>Guardar y Continuar</button>
    </form>
  </div>
  <script>
    function submitName(e) {{
      e.preventDefault();
      var val = (document.getElementById('nameInput').value || '').trim();
      if (val) {{
        window.chrome.webview.postMessage('SET_NAME:' + val);
      }}
    }}
  </script>
</body>
</html>";

        _webView.NavigateToString(html);
    }

    private async Task ConnectToChannelAsync()
    {
        StopAutoRetry();

        var serverCode = _settings.ServerCode;
        var channel = _settings.Channel;
        var user = _settings.Username;
        var pass = _settings.Password;

        // 1. Si este equipo está hosteando activamente
        if (_settings.IsHosting && await IsLocalServerRunningAsync())
        {
            var hostUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
            _webView.Source = new Uri(hostUrl);
            return;
        }

        // 2. Cliente remoto: resolver servidor de tu amigo
        var resolved = await ResolveInputToUrlAsync(serverCode);
        if (!string.IsNullOrEmpty(resolved) && Uri.TryCreate(resolved, UriKind.Absolute, out var baseUri))
        {
            var sep = resolved.Contains('?') ? "&" : "?";
            var finalUrl = $"{resolved}{sep}room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
            if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var fullUri))
            {
                _webView.Source = fullUri;
                return;
            }
        }

        // 3. Si aún no está online, pantalla de espera
        ShowWaitingScreen(serverCode, channel);
    }

    private void ShowWaitingScreen(string serverCode, string channel)
    {
        var displayServer = string.IsNullOrWhiteSpace(serverCode) ? "-" : serverCode;
        var displayChannel = string.IsNullOrWhiteSpace(channel) ? "general" : channel;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Conectando al servidor...</title>
  <style>
    * {{ box-sizing: border-box; }}
    body {{
      background-color: #1e1f22;
      color: #f2f3f5;
      font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100vh;
      margin: 0;
      user-select: none;
    }}
    .card {{
      background: #2b2d31;
      border: 1px solid #3f4147;
      border-radius: 14px;
      padding: 38px 34px;
      width: 440px;
      text-align: center;
      box-shadow: 0 12px 32px rgba(0,0,0,0.5);
    }}
    .spinner {{
      width: 46px;
      height: 46px;
      border: 4px solid rgba(88, 101, 242, 0.2);
      border-top-color: #5865f2;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 22px auto;
    }}
    @keyframes spin {{ to {{ transform: rotate(360deg); }} }}
    h2 {{
      margin: 0 0 10px 0;
      font-size: 21px;
      font-weight: 700;
      color: #fff;
    }}
    .room-pill {{
      display: inline-block;
      background: rgba(88, 101, 242, 0.15);
      border: 1px solid rgba(88, 101, 242, 0.4);
      color: #5865f2;
      font-weight: 700;
      font-size: 14.5px;
      padding: 6px 18px;
      border-radius: 20px;
      margin-bottom: 16px;
    }}
    p {{
      color: #949ba4;
      font-size: 13.5px;
      line-height: 1.5;
      margin: 0 0 24px 0;
    }}
    .actions {{
      display: flex;
      gap: 10px;
      justify-content: center;
    }}
    button {{
      background: #35373c;
      border: 1px solid #4e5058;
      color: #dbdee1;
      padding: 9px 18px;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }}
    button:hover {{
      background: #5865f2;
      border-color: #5865f2;
      color: #fff;
    }}
    .status-live {{
      font-size: 12px;
      color: #23a55a;
      margin-top: 18px;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
    }}
    .dot {{
      width: 8px;
      height: 8px;
      background-color: #23a55a;
      border-radius: 50%;
      animation: pulse 1.5s infinite;
    }}
    @keyframes pulse {{
      0% {{ opacity: 0.3; }}
      50% {{ opacity: 1; }}
      100% {{ opacity: 0.3; }}
    }}
  </style>
</head>
<body>
  <div class='card'>
    <div class='spinner'></div>
    <h2>Buscando servidor...</h2>
    <div class='room-pill'>Servidor: {displayServer} • Canal: #{displayChannel}</div>
    <p>Esperando a que el anfitrión tenga Lowcord abierto.<br>Te conectarás automáticamente apenas esté online.</p>
    <div class='actions'>
      <button onclick='window.chrome.webview.postMessage(""CHANGE_SERVER"")'>Volver a Canales (F2)</button>
    </div>
    <div class='status-live'>
      <div class='dot'></div>
      <span>Reintentando automáticamente cada 4s</span>
    </div>
  </div>
</body>
</html>";

        _webView.NavigateToString(html);
        StartAutoRetry();
    }

    private void StartAutoRetry()
    {
        StopAutoRetry();
        _retryTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        _retryTimer.Tick += async (s, e) =>
        {
            var serverCode = _settings.ServerCode;
            var channel = _settings.Channel;
            var user = _settings.Username;
            var pass = _settings.Password;

            if (_settings.IsHosting && await IsLocalServerRunningAsync())
            {
                StopAutoRetry();
                var hostUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
                _webView.Source = new Uri(hostUrl);
                return;
            }

            var resolved = await ResolveInputToUrlAsync(serverCode);
            if (!string.IsNullOrEmpty(resolved) && Uri.TryCreate(resolved, UriKind.Absolute, out var baseUri))
            {
                StopAutoRetry();
                var sep = resolved.Contains('?') ? "&" : "?";
                var finalUrl = $"{resolved}{sep}room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
                if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var fullUri))
                {
                    _webView.Source = fullUri;
                }
            }
        };
        _retryTimer.Start();
    }

    private void StopAutoRetry()
    {
        if (_retryTimer != null)
        {
            _retryTimer.Stop();
            _retryTimer.Dispose();
            _retryTimer = null;
        }
    }

    public string? FindServerExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "EJECUTABLES_LISTOS", "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "EJECUTABLES_LISTOS", "Servidor", "Lowcord.Server.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lowcord.Server.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public void OnHostServerStarted(string room)
    {
        _settings.IsHosting = true;
        _settings.ServerCode = room;
        SaveSettings();
        BeginInvoke(() => ShowConnectScreen());
    }

    public void OnHostServerStopped()
    {
        _settings.IsHosting = false;
        SaveSettings();
        BeginInvoke(() => ShowConnectScreen());
    }

    private string? GetLocalRoomCode()
    {
        try
        {
            var paths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "room_code.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "room_code.txt"),
                Path.Combine(Environment.CurrentDirectory, "room_code.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "EJECUTABLES_LISTOS", "room_code.txt")
            };
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    var txt = File.ReadAllText(p).Trim();
                    if (!string.IsNullOrEmpty(txt)) return txt.ToLowerInvariant();
                }
            }
        }
        catch { }
        return null;
    }

    private bool IsTargetMatchingLocalHost(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Contains("localhost") || target.StartsWith("127.0.0.1"))
            return true;

        var localRoom = GetLocalRoomCode();
        if (!string.IsNullOrEmpty(localRoom) && string.Equals(target.Trim(), localRoom, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // =========================================================
    // AUTO-UPDATER NATIVO
    // =========================================================
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/FakuInsa/lowcord/releases/latest");
            req.Headers.UserAgent.ParseAdd("Lowcord-Client");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagProp)) return;

            var tagName = tagProp.GetString()?.Trim();
            if (string.IsNullOrEmpty(tagName) || tagName.Equals(APP_VERSION, StringComparison.OrdinalIgnoreCase))
                return;

            if (IsVersionNewer(tagName, APP_VERSION))
            {
                string? downloadUrl = null;
                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Client", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                    if (downloadUrl == null)
                    {
                        foreach (var asset in assetsProp.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? "";
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && name.Contains("Client", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    _pendingUpdateUrl = downloadUrl;
                    _pendingUpdateVersion = tagName;

                    BeginInvoke(() =>
                    {
                        try
                        {
                            var updatePayload = JsonSerializer.Serialize(new
                            {
                                type = "UPDATE_AVAILABLE",
                                version = tagName,
                                downloadUrl = downloadUrl
                            });
                            _webView.CoreWebView2?.PostWebMessageAsJson(updatePayload);
                        }
                        catch { }
                    });
                }
            }
        }
        catch { }
    }

    private static bool IsVersionNewer(string remote, string local)
    {
        try
        {
            var r = remote.TrimStart('v', 'V').Split('.');
            var l = local.TrimStart('v', 'V').Split('.');
            for (int i = 0; i < Math.Max(r.Length, l.Length); i++)
            {
                var rNum = i < r.Length && int.TryParse(r[i], out var rn) ? rn : 0;
                var lNum = i < l.Length && int.TryParse(l[i], out var ln) ? ln : 0;
                if (rNum > lNum) return true;
                if (rNum < lNum) return false;
            }
        }
        catch { }
        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteUpdateAsync()
    {
        if (string.IsNullOrEmpty(_pendingUpdateUrl)) return;

        try
        {
            var currentExe = Environment.ProcessPath ?? Application.ExecutablePath;
            var dir = Path.GetDirectoryName(currentExe)!;
            var tempDownloadFile = Path.Combine(dir, "update_download.tmp");

            using (var req = new HttpRequestMessage(HttpMethod.Get, _pendingUpdateUrl))
            {
                req.Headers.UserAgent.ParseAdd("Lowcord-Client");
                var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("No se pudo descargar la actualización desde GitHub. Intenta nuevamente.", "Aviso de Actualización");
                    return;
                }

                await using var srcStream = await resp.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempDownloadFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await srcStream.CopyToAsync(fileStream);
            }

            string newExePath = tempDownloadFile;
            if (_pendingUpdateUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractFolder = Path.Combine(dir, "extract_" + Guid.NewGuid().ToString("N"));
                System.IO.Compression.ZipFile.ExtractToDirectory(tempDownloadFile, extractFolder, true);
                var foundExe = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(f => Path.GetFileName(f).Contains("Client", StringComparison.OrdinalIgnoreCase));

                if (foundExe != null)
                {
                    newExePath = Path.Combine(dir, "extracted_client.tmp");
                    File.Copy(foundExe, newExePath, true);
                }
                try { Directory.Delete(extractFolder, true); } catch { }
                try { File.Delete(tempDownloadFile); } catch { }
            }

            var oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);

            File.Move(currentExe, oldExe);
            File.Move(newExePath, currentExe);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true
            });

            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al actualizar automáticamente: {ex.Message}", "Aviso de Actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // =========================================================
    // ATAJOS GLOBALES DE TECLADO (HOOK)
    // =========================================================
    private void InstallGlobalKeyboardHook()
    {
        if (_hookId != IntPtr.Zero) return;
        _keyboardProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    private void UninstallGlobalKeyboardHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            // Coincidencia exacta o equivalencia para la tecla al lado del 1 (0xDC / 0xC0 / 0xDF)
            bool isMatch = ((uint)vkCode == _targetVk);
            if (!isMatch && (_targetVk == 0xDC || _targetVk == 0xC0 || _targetVk == 0xDF))
            {
                isMatch = (vkCode == 0xDC || vkCode == 0xC0 || vkCode == 0xDF);
            }

            if (isMatch)
            {
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (!_isTargetKeyDown)
                    {
                        _isTargetKeyDown = true;
                        BeginInvoke(() =>
                        {
                            try
                            {
                                _webView?.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"GLOBAL_KEYDOWN\"}");
                            }
                            catch { }
                        });
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    _isTargetKeyDown = false;
                    BeginInvoke(() =>
                    {
                        try
                        {
                            _webView?.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"GLOBAL_KEYUP\"}");
                        }
                        catch { }
                    });
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void UpdateGlobalHotkey(string? code)
    {
        if (string.IsNullOrEmpty(code)) return;
        uint vk = ParseKeyCodeToVk(code);
        if (vk != 0)
        {
            _targetVk = vk;
        }
    }

    private static uint ParseKeyCodeToVk(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return 0;
        code = code.Trim();

        if (code.Length == 1 && char.IsLetter(code[0]))
        {
            return (uint)char.ToUpperInvariant(code[0]);
        }

        if (code.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && code.Length == 4 && char.IsLetter(code[3]))
        {
            return (uint)char.ToUpperInvariant(code[3]);
        }

        if (code.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && code.Length == 6 && char.IsDigit(code[5]))
        {
            return (uint)(Keys.D0 + (code[5] - '0'));
        }

        if (code.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) && code.Length == 7 && char.IsDigit(code[6]))
        {
            return (uint)(Keys.NumPad0 + (code[6] - '0'));
        }

        if (code.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(code.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
        {
            return (uint)(Keys.F1 + (fNum - 1));
        }

        return code.ToLowerInvariant() switch
        {
            "space" => (uint)Keys.Space,
            "capslock" => (uint)Keys.CapsLock,
            "tab" => (uint)Keys.Tab,
            "backquote" => 0xDC,
            "oem5" or "oem_5" => 0xDC,
            "oem3" or "oem_3" => 0xC0,
            "minus" => 0xBD,
            "equal" => 0xBB,
            "bracketleft" => 0xDB,
            "bracketright" => 0xDD,
            "backslash" => 0xDC,
            "semicolon" => 0xBA,
            "quote" => 0xDE,
            "comma" => 0xBC,
            "period" => 0xBE,
            "slash" => 0xBF,
            "insert" => (uint)Keys.Insert,
            "delete" => (uint)Keys.Delete,
            "home" => (uint)Keys.Home,
            "end" => (uint)Keys.End,
            "pageup" => (uint)Keys.PageUp,
            "pagedown" => (uint)Keys.PageDown,
            "controlleft" or "controlright" => (uint)Keys.ControlKey,
            "shiftleft" or "shiftright" => (uint)Keys.ShiftKey,
            "altleft" or "altright" => (uint)Keys.Menu,
            _ => 0
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UninstallGlobalKeyboardHook();

        try { _serverHostForm?.Close(); } catch { }

        // Limpiar procesos de servidor y túnel al cerrar la aplicación
        try
        {
            var cfProcs = System.Diagnostics.Process.GetProcessesByName("cloudflared");
            foreach (var p in cfProcs) { try { p.Kill(); } catch { } }

            var srvProcs = System.Diagnostics.Process.GetProcessesByName("Lowcord.Server");
            foreach (var p in srvProcs) { try { p.Kill(); } catch { } }
        }
        catch { }

        base.OnFormClosing(e);
    }
}
