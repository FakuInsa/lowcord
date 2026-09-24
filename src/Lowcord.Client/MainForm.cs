using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lowcord.Client;

public class MainForm : Form
{
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
    private readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.txt");
    private string _serverUrl = "";
    private System.Windows.Forms.Timer? _retryTimer;

    public MainForm()
    {
        InitializeComponent();
        InstallGlobalKeyboardHook();
        _ = InitializeWebViewAsync();
    }

    private void InitializeComponent()
    {
        Text = "Lowcord - Cliente Ultra-Ligero P2P";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 31, 34);

        if (File.Exists(_configFile))
        {
            var saved = File.ReadAllText(_configFile).Trim();
            if (!string.IsNullOrEmpty(saved))
            {
                _serverUrl = saved;
            }
        }

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_webView);

        // Tecla F2 para cambiar de sala en cualquier momento
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
                "--enable-gpu-rasterization"
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

            // Escuchar mensajes del cliente web (ej: atajos o botón Cambiar Sala)
            _webView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                try
                {
                    var msgString = args.TryGetWebMessageAsString();
                    if (msgString == "CHANGE_SERVER")
                    {
                        BeginInvoke(() => ShowConnectScreen());
                        return;
                    }
                    if (msgString == "EXIT_APP")
                    {
                        BeginInvoke(() => Application.Exit());
                        return;
                    }
                    if (!string.IsNullOrEmpty(msgString) && msgString.StartsWith("CONNECT_ROOM:"))
                    {
                        var target = msgString.Substring("CONNECT_ROOM:".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(target))
                        {
                            _serverUrl = target;
                            try { File.WriteAllText(_configFile, _serverUrl); } catch { }
                            BeginInvoke(async () => await NavigateToServerAsync());
                        }
                        return;
                    }

                    var rawJson = args.WebMessageAsJson;
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "UPDATE_HOTKEY")
                    {
                        if (root.TryGetProperty("code", out var codeProp))
                        {
                            var code = codeProp.GetString();
                            UpdateGlobalHotkey(code);
                        }
                    }
                }
                catch { }
            };

            // Si falla la navegación (ej: servidor caído o túnel cerrado), mostrar pantalla de espera con reintento automático
            _webView.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowWaitingScreen(_serverUrl);
                }
            };

            _ = NavigateToServerAsync();
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
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            var resp = await _httpClient.GetAsync("http://localhost:8080", cts.Token);
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

        // Si ya es una URL completa
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        // Si es IP directa
        if (input.StartsWith("127.0.0.1") || (input.Contains(':') && !input.Contains(' ')))
        {
            return "http://" + input;
        }

        // Si es un código de sala (ej: "facu")
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
        catch
        {
            // Error de conexión con keyval
        }

        return null;
    }

    private void ShowConnectScreen()
    {
        StopAutoRetry();

        var currentVal = string.IsNullOrWhiteSpace(_serverUrl) || _serverUrl.Contains("localhost") ? "" : _serverUrl;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Unirse a Sala Lowcord</title>
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
      height: 100vh;
      user-select: none;
    }}
    .card {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 38px 34px;
      width: 440px;
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
      margin-bottom: 16px;
      outline: none;
    }}
    .input-box:focus {{
      border-color: #5865F2;
    }}
    .btn {{
      width: 100%;
      background: #5865F2;
      color: #fff;
      border: none;
      padding: 12px;
      border-radius: 8px;
      font-size: 14.5px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s;
    }}
    .btn:hover {{
      background: #4752c4;
    }}
    .btn-secondary {{
      background: #35373c;
      color: #dbdee1;
    }}
    .btn-secondary:hover {{
      background: #4e5058;
      color: #fff;
    }}
  </style>
</head>
<body>
  <div class='card'>
    <div class='logo'>
      <svg viewBox='0 0 24 24'><path d='M12 3a9 9 0 0 0-9 9v7a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H5v-3a7 7 0 1 1 14 0v3h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-7a9 9 0 0 0-9-9z'/></svg>
    </div>
    <h2>Conectar a un Servidor</h2>
    <p>Ingresa el nombre o código del servidor de tu amigo (ej: facu, squad5):</p>
    <form onsubmit='submitRoom(event)'>
      <input type='text' id='roomInput' class='input-box' value='{currentVal}' placeholder='Nombre o código de servidor' autocomplete='off' required autofocus>
      <div style='display: flex; gap: 10px;'>
        <button type='button' class='btn btn-secondary' style='flex: 1;' onclick='window.chrome.webview.postMessage(""EXIT_APP"")'>Salir</button>
        <button type='submit' class='btn' style='flex: 2;'>Conectar</button>
      </div>
    </form>
  </div>
  <script>
    function submitRoom(e) {{
      e.preventDefault();
      var val = (document.getElementById('roomInput').value || '').trim();
      if (val) {{
        window.chrome.webview.postMessage('CONNECT_ROOM:' + val);
      }}
    }}
    setTimeout(function() {{
      var el = document.getElementById('roomInput');
      if (el) {{ el.focus(); el.select(); }}
    }}, 100);
  </script>
</body>
</html>";

        _webView.NavigateToString(html);
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

    private async Task NavigateToServerAsync()
    {
        // 1. Si el servidor local está activo Y el objetivo coincide con la sala local (o no hay sala configurada)
        if (await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(_serverUrl))
        {
            StopAutoRetry();
            _webView.Source = new Uri("http://localhost:8080");
            return;
        }

        // 2. Si no es el anfitrión (o apunta a otra sala) y no hay sala configurada en memoria
        if (string.IsNullOrWhiteSpace(_serverUrl) || _serverUrl.Contains("localhost"))
        {
            if (File.Exists(_configFile))
            {
                var saved = (await File.ReadAllTextAsync(_configFile)).Trim();
                if (!string.IsNullOrEmpty(saved) && !saved.Contains("localhost"))
                {
                    _serverUrl = saved;
                }
            }
        }

        // Si todavía no hay sala especificada
        if (string.IsNullOrWhiteSpace(_serverUrl) || _serverUrl.Contains("localhost"))
        {
            ShowConnectScreen();
            return;
        }

        // Si el objetivo es la sala local y el servidor local está corriendo
        if (await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(_serverUrl))
        {
            StopAutoRetry();
            _webView.Source = new Uri("http://localhost:8080");
            return;
        }

        // De lo contrario, buscar sala remota
        var resolved = await ResolveInputToUrlAsync(_serverUrl);
        if (!string.IsNullOrEmpty(resolved) && Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
        {
            StopAutoRetry();
            _webView.Source = uri;
        }
        else
        {
            ShowWaitingScreen(_serverUrl);
        }
    }

    private void ShowWaitingScreen(string roomCode)
    {
        var displayCode = string.IsNullOrWhiteSpace(roomCode) ? "-" : roomCode;
        if (displayCode.StartsWith("http://") || displayCode.StartsWith("https://"))
        {
            displayCode = "Enlace Web";
        }

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Buscando sala...</title>
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
    <h2>Buscando sala...</h2>
    <div class='room-pill'>Sala: {displayCode}</div>
    <p>Esperando a que tu amigo inicie Lowcord en su PC.<br>Te conectarás automáticamente apenas la sala esté activa.</p>
    <div class='actions'>
      <button onclick='window.chrome.webview.postMessage(""CHANGE_SERVER"")'>Cambiar Sala (F2)</button>
    </div>
    <div class='status-live'>
      <div class='dot'></div>
      <span>Reintentando automáticamente cada 3 segundos</span>
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
        _retryTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _retryTimer.Tick += async (s, e) =>
        {
            if (await IsLocalServerRunningAsync() && IsTargetMatchingLocalHost(_serverUrl))
            {
                StopAutoRetry();
                _webView.Source = new Uri("http://localhost:8080");
                return;
            }

            var resolved = await ResolveInputToUrlAsync(_serverUrl);
            if (!string.IsNullOrEmpty(resolved) && Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
            {
                StopAutoRetry();
                _webView.Source = uri;
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

    private void PromptChangeServer(string message = "Ingresa el CÓDIGO de sala (ej: facu) o enlace:")
    {
        using var prompt = new Form
        {
            Width = 500,
            Height = 240,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Text = "Conectar a Sala Lowcord",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(43, 45, 49),
            ForeColor = Color.White
        };

        var label = new Label
        {
            Left = 20,
            Top = 15,
            Width = 445,
            Height = 45,
            Text = message,
            Font = new Font("Segoe UI", 9.5f)
        };

        var textBox = new TextBox
        {
            Left = 20,
            Top = 68,
            Width = 445,
            Text = _serverUrl,
            BackColor = Color.FromArgb(30, 31, 34),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f)
        };

        var lblStatus = new Label
        {
            Left = 20,
            Top = 105,
            Width = 445,
            Height = 25,
            ForeColor = Color.FromArgb(241, 196, 15),
            Text = "",
            Font = new Font("Segoe UI", 9f)
        };

        var btnOk = new Button
        {
            Text = "Conectar",
            Left = 250,
            Width = 105,
            Top = 145,
            Height = 36,
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var btnCancel = new Button
        {
            Text = "Cancelar",
            Left = 365,
            Width = 100,
            Top = 145,
            Height = 36,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f)
        };

        btnOk.Click += async (s, e) =>
        {
            var rawInput = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawInput)) return;

            btnOk.Enabled = false;
            btnCancel.Enabled = false;
            textBox.Enabled = false;
            lblStatus.Text = "Buscando sala...";

            _serverUrl = rawInput;
            try
            {
                File.WriteAllText(_configFile, _serverUrl);
            }
            catch { }

            prompt.DialogResult = DialogResult.OK;
            prompt.Close();

            var resolvedUrl = await ResolveInputToUrlAsync(rawInput);
            if (!string.IsNullOrEmpty(resolvedUrl) && Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            {
                StopAutoRetry();
                if (_webView.CoreWebView2 != null)
                {
                    _webView.Source = uri;
                }
            }
            else
            {
                ShowWaitingScreen(_serverUrl);
            }
        };

        prompt.Controls.Add(label);
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(lblStatus);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        prompt.ShowDialog(this);
    }

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
        if (string.IsNullOrEmpty(code)) return 0;

        // Teclas de letras: KeyA .. KeyZ
        if (code.StartsWith("Key") && code.Length == 4 && char.IsLetter(code[3]))
        {
            return (uint)char.ToUpperInvariant(code[3]);
        }

        // Teclas numéricas: Digit0 .. Digit9
        if (code.StartsWith("Digit") && code.Length == 6 && char.IsDigit(code[5]))
        {
            return (uint)(Keys.D0 + (code[5] - '0'));
        }

        // Teclado numérico: Numpad0 .. Numpad9
        if (code.StartsWith("Numpad") && code.Length == 7 && char.IsDigit(code[6]))
        {
            return (uint)(Keys.NumPad0 + (code[6] - '0'));
        }

        // Teclas de función: F1 .. F24
        if (code.StartsWith("F") && int.TryParse(code.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
        {
            return (uint)(Keys.F1 + (fNum - 1));
        }

        return code switch
        {
            "Space" => (uint)Keys.Space,
            "CapsLock" => (uint)Keys.CapsLock,
            "Tab" => (uint)Keys.Tab,
            "Backquote" => 0xC0,
            "Minus" => 0xBD,
            "Equal" => 0xBB,
            "BracketLeft" => 0xDB,
            "BracketRight" => 0xDD,
            "Backslash" => 0xDC,
            "Semicolon" => 0xBA,
            "Quote" => 0xDE,
            "Comma" => 0xBC,
            "Period" => 0xBE,
            "Slash" => 0xBF,
            "Insert" => (uint)Keys.Insert,
            "Delete" => (uint)Keys.Delete,
            "Home" => (uint)Keys.Home,
            "End" => (uint)Keys.End,
            "PageUp" => (uint)Keys.PageUp,
            "PageDown" => (uint)Keys.PageDown,
            _ => 0
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UninstallGlobalKeyboardHook();
        base.OnFormClosing(e);
    }
}

