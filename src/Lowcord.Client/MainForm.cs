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

    private WebView2 _webView = null!;
    private readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.txt");
    private string _serverUrl = "http://localhost:8080";

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

        // Tecla F2 para cambiar la URL del servidor manualmente en cualquier momento
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                PromptChangeServer("Ingresa la URL o IP del servidor Lowcord (ej: https://...):");
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

            // Escuchar mensajes del cliente web (ej: actualización de atajo de teclado)
            _webView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                try
                {
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

            // Si falla la conexión (por ejemplo, en la PC de un amigo donde localhost no existe),
            // solicitar automáticamente la URL del servidor sin mostrar pantalla de error
            _webView.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    PromptChangeServer("No se pudo conectar a la dirección actual.\nIngresa el enlace o IP que te pasó tu amigo:");
                }
            };

            NavigateToServer();
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

    private void NavigateToServer()
    {
        if (Uri.TryCreate(_serverUrl, UriKind.Absolute, out var uri))
        {
            _webView.Source = uri;
        }
        else
        {
            PromptChangeServer("Ingresa la URL del servidor:");
        }
    }

    private void PromptChangeServer(string message = "Ingresa la URL del servidor:")
    {
        using var prompt = new Form
        {
            Width = 480,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Conectar a Servidor Lowcord",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(43, 45, 49),
            ForeColor = Color.White
        };

        var label = new Label
        {
            Left = 20,
            Top = 15,
            Width = 420,
            Height = 40,
            Text = message
        };

        var textBox = new TextBox
        {
            Left = 20,
            Top = 65,
            Width = 420,
            Text = _serverUrl,
            BackColor = Color.FromArgb(30, 31, 34),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10)
        };

        var btnOk = new Button
        {
            Text = "Conectar",
            Left = 240,
            Width = 100,
            Top = 115,
            Height = 35,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        var btnCancel = new Button
        {
            Text = "Cancelar",
            Left = 350,
            Width = 90,
            Top = 115,
            Height = 35,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        prompt.Controls.Add(label);
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            _serverUrl = textBox.Text.Trim();
            if (!_serverUrl.StartsWith("http://") && !_serverUrl.StartsWith("https://"))
            {
                _serverUrl = "http://" + _serverUrl;
            }
            try
            {
                File.WriteAllText(_configFile, _serverUrl);
            }
            catch { }

            if (_webView.CoreWebView2 != null)
            {
                _webView.Source = new Uri(_serverUrl);
            }
        }
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

