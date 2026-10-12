using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lowcord.Client;

public class ClientSettings
{
    public string Username { get; set; } = "";
    public string Channel { get; set; } = "general";
    public string Password { get; set; } = "";
    public bool IsHost { get; set; } = false;
}

public class MainForm : Form
{
    public const string APP_VERSION = "v2.2";

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

    public MainForm()
    {
        try
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal;
            CleanUpOldUpdates();
        }
        catch { }

        LoadSettings();
        InitializeComponent();
        InstallGlobalKeyboardHook();
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
                    _settings.Channel = saved;
                }
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
            File.WriteAllText(_configFile, _settings.Channel);
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

        // Tecla F2 para cambiar de canal en cualquier momento
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

            // Escuchar mensajes del cliente web
            _webView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                try
                {
                    string? msgString = null;
                    try
                    {
                        msgString = args.TryGetWebMessageAsString();
                    }
                    catch
                    {
                        // Si el mensaje viene como objeto JSON puro
                    }

                    if (!string.IsNullOrEmpty(msgString))
                    {
                        if (msgString == "CHANGE_SERVER" || msgString == "CHANGE_CHANNEL")
                        {
                            BeginInvoke(() => ShowConnectScreen());
                            return;
                        }
                        if (msgString == "EXIT_APP")
                        {
                            BeginInvoke(() => Application.Exit());
                            return;
                        }
                        if (msgString == "EXECUTE_UPDATE" || msgString == "START_UPDATE")
                        {
                            BeginInvoke(async () => await ExecuteUpdateAsync());
                            return;
                        }
                        if (msgString.StartsWith("CONNECT_CHANNEL:"))
                        {
                            var jsonPayload = msgString.Substring("CONNECT_CHANNEL:".Length).Trim();
                            try
                            {
                                using var doc = JsonDocument.Parse(jsonPayload);
                                var root = doc.RootElement;
                                _settings.Username = root.TryGetProperty("user", out var u) ? u.GetString() ?? "" : "";
                                _settings.Channel = (root.TryGetProperty("channel", out var c) ? c.GetString() ?? "general" : "general").Trim().ToLowerInvariant();
                                _settings.Password = root.TryGetProperty("pass", out var p) ? p.GetString() ?? "" : "";
                                _settings.IsHost = root.TryGetProperty("isHost", out var h) && h.GetBoolean();
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
                        try
                        {
                            rawJson = args.WebMessageAsJson;
                        }
                        catch { }
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
                                if (type == "UPDATE_HOTKEY" && root.TryGetProperty("code", out var codeProp))
                                {
                                    UpdateGlobalHotkey(codeProp.GetString());
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

            // Reintento automático en caso de pérdida de conexión
            _webView.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowWaitingScreen(_settings.Channel);
                }
            };

            // Comprobar actualizaciones en segundo plano al iniciar
            _ = Task.Run(async () => await CheckForUpdatesAsync());

            // Mostrar pantalla inicial de canal y contraseña
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

    private async Task<bool> IsLocalServerRunningAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(450));
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

        var code = input.ToLowerInvariant();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
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

    private void ShowConnectScreen()
    {
        StopAutoRetry();

        var safeUser = System.Net.WebUtility.HtmlEncode(_settings.Username);
        var safeChannel = System.Net.WebUtility.HtmlEncode(_settings.Channel);
        var safePass = System.Net.WebUtility.HtmlEncode(_settings.Password);
        var hostChecked = _settings.IsHost ? "checked" : "";

        var updateHtml = "";
        if (!string.IsNullOrEmpty(_pendingUpdateVersion))
        {
            updateHtml = $@"
        <div class='update-alert'>
          <span>🚀 <b>Actualización lista ({_pendingUpdateVersion})</b></span>
          <button type='button' class='btn-mini-update' onclick='window.chrome.webview.postMessage(""EXECUTE_UPDATE"")'>Actualizar Ahora</button>
        </div>";
        }

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Entrar a Lowcord</title>
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
      padding: 34px 30px;
      width: 420px;
      max-width: 95vw;
      text-align: center;
      box-shadow: 0 16px 40px rgba(0,0,0,0.5);
      position: relative;
    }}
    .logo {{
      width: 56px;
      height: 56px;
      background: linear-gradient(135deg, #5865F2 0%, #4752c4 100%);
      border-radius: 16px;
      margin: 0 auto 16px auto;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 6px 20px rgba(88, 101, 242, 0.35);
    }}
    .logo svg {{ width: 30px; height: 30px; fill: #fff; }}
    h2 {{
      margin-bottom: 6px;
      font-size: 22px;
      font-weight: 700;
      color: #fff;
    }}
    p.desc {{
      color: #949ba4;
      font-size: 13px;
      line-height: 1.4;
      margin-bottom: 22px;
    }}
    .form-group {{
      text-align: left;
      margin-bottom: 15px;
    }}
    label {{
      display: block;
      color: #b5bac1;
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 6px;
    }}
    .input-box {{
      width: 100%;
      padding: 11px 13px;
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      color: #fff;
      font-size: 14.5px;
      font-weight: 500;
      outline: none;
      transition: border-color 0.15s;
    }}
    .input-box:focus {{
      border-color: #5865F2;
    }}
    .toggle-row {{
      display: flex;
      align-items: center;
      gap: 9px;
      margin: 16px 0 20px 0;
      cursor: pointer;
      text-align: left;
    }}
    .toggle-row input {{
      width: 16px;
      height: 16px;
      accent-color: #5865F2;
      cursor: pointer;
    }}
    .toggle-row span {{
      color: #dbdee1;
      font-size: 13px;
      font-weight: 500;
    }}
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
      transition: background 0.15s, transform 0.05s;
    }}
    .btn:hover {{ background: #4752c4; }}
    .btn:active {{ transform: scale(0.99); }}
    .footer-note {{
      margin-top: 18px;
      color: #80848e;
      font-size: 11.5px;
    }}
    .update-alert {{
      background: rgba(88, 101, 242, 0.2);
      border: 1px solid #5865F2;
      border-radius: 8px;
      padding: 9px 12px;
      margin-bottom: 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 12.5px;
      color: #dbdee1;
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
    .btn-mini-update:hover {{ background: #4752c4; }}
  </style>
</head>
<body>
  <div class='card'>
    {updateHtml}
    <div class='logo'>
      <svg viewBox='0 0 24 24'><path d='M12 3a9 9 0 0 0-9 9v7a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H5v-3a7 7 0 1 1 14 0v3h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-7a9 9 0 0 0-9-9z'/></svg>
    </div>
    <h2>Lowcord</h2>
    <p class='desc'>Chat de voz y pantalla compartida P2P ultra-ligero</p>

    <form onsubmit='handleConnect(event)'>
      <div class='form-group'>
        <label>Tu Nombre o Apodo</label>
        <input type='text' id='userInput' class='input-box' value='{safeUser}' placeholder='Ej: Faku' required autocomplete='off' autofocus>
      </div>

      <div class='form-group'>
        <label>Nombre del Canal</label>
        <input type='text' id='channelInput' class='input-box' value='{safeChannel}' placeholder='Ej: tarkov, squad' required autocomplete='off'>
      </div>

      <div class='form-group'>
        <label>Contraseña del Canal</label>
        <input type='password' id='passInput' class='input-box' value='{safePass}' placeholder='Clave de acceso (opcional)' autocomplete='off'>
      </div>

      <label class='toggle-row'>
        <input type='checkbox' id='hostCheck' {hostChecked}>
        <span>Soy el Anfitrión (Alojar este canal en mi PC)</span>
      </label>

      <button type='submit' class='btn' id='btnSubmit'>Entrar al Canal</button>
    </form>

    <div class='footer-note'>
      {APP_VERSION} • Cero lag • Atajo global (M) • Presiona F2 para cambiar de canal
    </div>
  </div>

  <script>
    function handleConnect(e) {{
      e.preventDefault();
      var user = (document.getElementById('userInput').value || '').trim();
      var channel = (document.getElementById('channelInput').value || '').trim().toLowerCase();
      var pass = (document.getElementById('passInput').value || '').trim();
      var isHost = document.getElementById('hostCheck').checked;

      if (!user || !channel) return;

      var btn = document.getElementById('btnSubmit');
      btn.disabled = true;
      btn.innerText = 'Conectando...';

      var payload = JSON.stringify({{
        user: user,
        channel: channel,
        pass: pass,
        isHost: isHost
      }});

      window.chrome.webview.postMessage('CONNECT_CHANNEL:' + payload);
    }}
  </script>
</body>
</html>";

        _webView.NavigateToString(html);
    }

    private async Task ConnectToChannelAsync()
    {
        StopAutoRetry();

        var channel = _settings.Channel;
        var user = _settings.Username;
        var pass = _settings.Password;

        // 1. Modo anfitrión (Host)
        if (_settings.IsHost)
        {
            await EnsureHostServerRunningAsync(channel);
            var hostUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
            _webView.Source = new Uri(hostUrl);
            return;
        }

        // 2. Si el servidor local ya está activo y coincide con este canal
        if (await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(channel))
        {
            var hostUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
            _webView.Source = new Uri(hostUrl);
            return;
        }

        // 3. Cliente remoto: resolver canal
        var resolved = await ResolveInputToUrlAsync(channel);
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

        // 4. Si el canal aún no está online, mostrar espera con reintento automático
        ShowWaitingScreen(channel);
    }

    private void ShowWaitingScreen(string channel)
    {
        var displayChannel = string.IsNullOrWhiteSpace(channel) ? "-" : channel;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Conectando al canal...</title>
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
      font-size: 15px;
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
    <h2>Buscando canal...</h2>
    <div class='room-pill'>Canal: #{displayChannel}</div>
    <p>Esperando a que el anfitrión abra el canal.<br>Te conectarás automáticamente en cuanto esté disponible.</p>
    <div class='actions'>
      <button onclick='window.chrome.webview.postMessage(""CHANGE_CHANNEL"")'>Cambiar Canal (F2)</button>
    </div>
    <div class='status-live'>
      <div class='dot'></div>
      <span>Reintentando automáticamente cada 2.5s</span>
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
        _retryTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        _retryTimer.Tick += async (s, e) =>
        {
            var channel = _settings.Channel;
            var user = _settings.Username;
            var pass = _settings.Password;

            if (await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(channel))
            {
                StopAutoRetry();
                var hostUrl = $"http://localhost:8080/?room={Uri.EscapeDataString(channel)}&user={Uri.EscapeDataString(user)}&pass={Uri.EscapeDataString(pass)}&autojoin=true";
                _webView.Source = new Uri(hostUrl);
                return;
            }

            var resolved = await ResolveInputToUrlAsync(channel);
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

    private async Task EnsureHostServerRunningAsync(string channel)
    {
        if (await IsLocalServerRunningAsync()) return;

        var serverExe = FindServerExecutable();
        if (!string.IsNullOrEmpty(serverExe) && File.Exists(serverExe))
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

                var cloudflaredExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.exe");
                if (File.Exists(cloudflaredExe))
                {
                    var cfLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.log");
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

                    _ = Task.Run(async () =>
                    {
                        for (int i = 0; i < 30; i++)
                        {
                            await Task.Delay(600);
                            if (File.Exists(cfLog))
                            {
                                var content = await File.ReadAllTextAsync(cfLog);
                                var match = System.Text.RegularExpressions.Regex.Match(content, @"(https://[a-zA-Z0-9-]+\.trycloudflare\.com)");
                                if (match.Success)
                                {
                                    var tunnelUrl = match.Groups[1].Value;
                                    var encoded = Uri.EscapeDataString(tunnelUrl);
                                    await _httpClient.GetStringAsync($"https://api.keyval.org/set/lowcord_{channel}/{encoded}");
                                    break;
                                }
                            }
                        }
                    });
                }
            }
            catch { }
        }

        for (int i = 0; i < 15; i++)
        {
            if (await IsLocalServerRunningAsync()) break;
            await Task.Delay(300);
        }
    }

    private string? FindServerExecutable()
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
                    // Prioridad 1: Ejecutable directo Lowcord-Client.exe
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Client", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                    // Prioridad 2: Archivo zip portable
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

            // Reemplazo atómico en Windows
            var oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);

            File.Move(currentExe, oldExe);
            File.Move(newExePath, currentExe);

            // Iniciar nueva versión
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

            if ((uint)vkCode == _targetVk)
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
            "backquote" => 0xC0,
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
        base.OnFormClosing(e);
    }
}
