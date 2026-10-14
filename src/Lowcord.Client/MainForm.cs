using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Lowcord.Client.Bridge;
using Lowcord.Client.Forms;
using Lowcord.Client.Services.Contracts;
using Lowcord.Client.Services.Implementations;

namespace Lowcord.Client;

public class MainForm : Form
{
    public const string APP_VERSION = "v2.8";

    private readonly WebView2 _webView;
    private readonly ISettingsService _settingsService;
    private readonly IServerProcessService _serverService;
    private readonly ITunnelService _tunnelService;
    private readonly IRoomRegistryService _roomRegistry;
    private readonly IHotkeyService _hotkeyService;
    private readonly IUpdateService _updateService;
    private WebUiBridge? _bridge;
    private ServerHostForm? _hostForm;

    public MainForm()
    {
        // 1. Inicializar servicios desacoplados
        _settingsService = new JsonSettingsService();
        _serverService = new KestrelServerService();
        _tunnelService = new CloudflareTunnelService();
        _roomRegistry = new KeyvalRoomRegistryService();
        _hotkeyService = new WindowsGlobalHotkeyService();
        _updateService = new GitHubUpdateService();

        // 2. Configuración de ventana WinForms
        Text = $"Lowcord {APP_VERSION}";
        Size = new Size(1100, 720);
        MinimumSize = new Size(680, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 31, 34);

        // 3. Control WebView2
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_webView);

        // 4. Configurar atajo de teclado global
        _hotkeyService.SetHotkey(_settingsService.Settings.HotkeyCode, _settingsService.Settings.HotkeyVk);
        _hotkeyService.OnKeyToggled += isDown =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "GLOBAL_HOTKEY",
                        state = isDown ? "down" : "up"
                    });
                    _webView.CoreWebView2?.PostWebMessageAsJson(payload);
                }
                catch { }
            });
        };
        _hotkeyService.Start();

        // 5. Atajo F2 para volver al lobby
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                _bridge?.RenderLobby();
            }
        };

        // 6. Inicializar WebView2 y puente
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Lowcord",
                "WebView2"
            );

            var options = new CoreWebView2EnvironmentOptions(
                "--autoplay-policy=no-user-gesture-required " +
                "--disable-features=Translate,OptimizationHints,MediaRouter " +
                "--disable-background-networking " +
                "--disable-component-update " +
                "--disable-domain-reliability " +
                "--disable-sync " +
                "--no-first-run " +
                "--no-default-browser-check " +
                "--enable-gpu-rasterization " +
                "--enable-zero-copy " +
                "--ignore-gpu-blocklist " +
                "--enable-hardware-overlays"
            );
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await _webView.EnsureCoreWebView2Async(env);

            // Permisos multimedia automáticos
            _webView.CoreWebView2.PermissionRequested += (s, args) =>
            {
                try
                {
                    if (args.PermissionKind is CoreWebView2PermissionKind.Microphone or
                        CoreWebView2PermissionKind.Camera or
                        (CoreWebView2PermissionKind)14) // Screen sharing
                    {
                        args.State = CoreWebView2PermissionState.Allow;
                    }
                }
                catch { }
            };

            // Crear el puente C# <-> JS
            _bridge = new WebUiBridge(
                _webView,
                _settingsService,
                _serverService,
                _tunnelService,
                _roomRegistry,
                _hotkeyService,
                _updateService,
                action => BeginInvoke(action),
                () => OpenHostWindow()
            );

            // Escuchar mensajes de WebView2 de forma segura
            _webView.CoreWebView2.WebMessageReceived += async (s, args) =>
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
                        // Si el mensaje es un objeto JS, TryGetWebMessageAsString arroja ArgumentException
                    }

                    if (!string.IsNullOrEmpty(msgString))
                    {
                        if (msgString.StartsWith("{"))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(msgString);
                                _bridge?.HandleJsonMessage(doc.RootElement);
                                return;
                            }
                            catch { }
                        }

                        await (_bridge?.HandleWebMessageAsync(msgString) ?? Task.CompletedTask);
                        return;
                    }

                    // Si no fue string, leer como JSON directamente
                    try
                    {
                        var rawJson = args.WebMessageAsJson;
                        if (!string.IsNullOrEmpty(rawJson))
                        {
                            using var doc = JsonDocument.Parse(rawJson);
                            _bridge?.HandleJsonMessage(doc.RootElement);
                        }
                    }
                    catch { }
                }
                catch { }
            };

            // Renderizar lobby inicial
            _bridge.RenderLobby();

            // Comprobar actualizaciones en segundo plano
            _ = Task.Run(async () =>
            {
                var newVer = await _updateService.CheckForUpdatesAsync(APP_VERSION);
                if (!string.IsNullOrEmpty(newVer))
                {
                    _bridge.RenderLobby();
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al inicializar WebView2:\n{ex.Message}",
                "Lowcord",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    public void OpenHostWindow()
    {
        if (_hostForm == null || _hostForm.IsDisposed)
        {
            _hostForm = new ServerHostForm(
                _settingsService,
                _serverService,
                _tunnelService,
                _roomRegistry,
                onJoinCall: (room, pass) =>
                {
                    _bridge?.JoinLocalHostCall(room, pass);
                },
                onHostStatusChanged: (status) =>
                {
                    _bridge?.UpdateHostStatus(status);
                }
            );
        }

        _hostForm.Show();
        _hostForm.BringToFront();
        _hostForm.WindowState = FormWindowState.Normal;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_hostForm != null && !_hostForm.IsDisposed)
        {
            try { _hostForm.Close(); } catch { }
        }
        _hotkeyService.Dispose();
        _tunnelService.Dispose();
        _serverService.Dispose();
        base.OnFormClosing(e);
    }
}
