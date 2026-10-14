using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Lowcord.Client.Models;
using Lowcord.Client.Services.Contracts;

namespace Lowcord.Client.Forms;

public class ServerHostForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly ISettingsService _settingsService;
    private readonly IServerProcessService _serverService;
    private readonly ITunnelService _tunnelService;
    private readonly IRoomRegistryService _roomRegistry;
    private readonly Action<string, string> _onJoinCall;
    private readonly Action<HostStatus> _onHostStatusChanged;

    private readonly TextBox _txtServerName;
    private readonly TextBox _txtPassword;
    private readonly Button _btnToggleHost;
    private readonly Button _btnEnterCall;
    private readonly Button _btnCopyLink;
    private readonly Label _lblStatus;
    private readonly RichTextBox _txtConsole;
    private readonly Panel _statusPanel;

    private bool _isStarting = false;
    private string? _currentTunnelUrl;

    public ServerHostForm(
        ISettingsService settingsService,
        IServerProcessService serverService,
        ITunnelService tunnelService,
        IRoomRegistryService roomRegistry,
        Action<string, string> onJoinCall,
        Action<HostStatus> onHostStatusChanged)
    {
        _settingsService = settingsService;
        _serverService = serverService;
        _tunnelService = tunnelService;
        _roomRegistry = roomRegistry;
        _onJoinCall = onJoinCall;
        _onHostStatusChanged = onHostStatusChanged;

        // Configuración de la Ventana
        Text = "Lowcord - Panel del Servidor";
        Size = new Size(580, 560);
        MinimumSize = new Size(500, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 31, 34);
        ForeColor = Color.FromArgb(242, 243, 245);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        ShowIcon = false;

        EnableDarkTitleBar();

        // 1. Cabecera
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(43, 45, 49),
            Padding = new Padding(16, 10, 16, 10)
        };

        var lblTitle = new Label
        {
            Text = "🖥️ Control del Servidor Anfitrión",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(14, 10)
        };
        var lblSubtitle = new Label
        {
            Text = "Inicia tu servidor para que tus amigos se conecten contigo.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 155, 164),
            AutoSize = true,
            Location = new Point(16, 34)
        };
        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSubtitle);
        Controls.Add(headerPanel);

        // 2. Controles de Configuración
        var configCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 145,
            BackColor = Color.FromArgb(43, 45, 49),
            Padding = new Padding(16)
        };

        var lblName = new Label
        {
            Text = "NOMBRE DEL SERVIDOR:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(181, 186, 193),
            Location = new Point(16, 12),
            AutoSize = true
        };

        _txtServerName = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(_settingsService.Settings.Channel) ? "facu" : _settingsService.Settings.Channel,
            Location = new Point(16, 32),
            Width = 260,
            Height = 28,
            BackColor = Color.FromArgb(30, 31, 34),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10.5f)
        };

        var lblPass = new Label
        {
            Text = "CONTRASEÑA (OPCIONAL):",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(181, 186, 193),
            Location = new Point(295, 12),
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Text = _settingsService.Settings.Password ?? "",
            Location = new Point(295, 32),
            Width = 245,
            Height = 28,
            BackColor = Color.FromArgb(30, 31, 34),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10.5f),
            PasswordChar = '●'
        };

        _btnToggleHost = new Button
        {
            Text = "▶ Iniciar Servidor",
            Location = new Point(16, 75),
            Size = new Size(524, 40),
            BackColor = Color.FromArgb(35, 165, 90),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnToggleHost.FlatAppearance.BorderSize = 0;
        _btnToggleHost.Click += async (s, e) => await OnToggleHostClickedAsync();

        configCard.Controls.Add(lblName);
        configCard.Controls.Add(_txtServerName);
        configCard.Controls.Add(lblPass);
        configCard.Controls.Add(_txtPassword);
        configCard.Controls.Add(_btnToggleHost);
        Controls.Add(configCard);

        // 3. Panel de Estado y Acciones Rápidas
        _statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(37, 38, 43),
            Padding = new Padding(16, 8, 16, 8)
        };

        _lblStatus = new Label
        {
            Text = "⚪ Servidor apagado",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 155, 164),
            Location = new Point(16, 14),
            AutoSize = true
        };

        _btnEnterCall = new Button
        {
            Text = "📞 Entrar a Llamada",
            Location = new Point(310, 8),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnEnterCall.FlatAppearance.BorderSize = 0;
        _btnEnterCall.Click += (s, e) =>
        {
            var room = _txtServerName.Text.Trim().ToLowerInvariant();
            var pass = _txtPassword.Text.Trim();
            _onJoinCall?.Invoke(room, pass);
        };

        _btnCopyLink = new Button
        {
            Text = "📋 Copiar Enlace",
            Location = new Point(445, 8),
            Size = new Size(100, 32),
            BackColor = Color.FromArgb(78, 80, 88),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnCopyLink.FlatAppearance.BorderSize = 0;
        _btnCopyLink.Click += (s, e) =>
        {
            var room = _txtServerName.Text.Trim().ToLowerInvariant();
            var link = $"https://fakuinsa.github.io/lowcord/?room={room}";
            Clipboard.SetText(link);
            MessageBox.Show($"Enlace copiado al portapapeles:\n{link}\n\nTus amigos pueden entrar desde el cliente o navegador.", "Lowcord", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        _statusPanel.Controls.Add(_lblStatus);
        _statusPanel.Controls.Add(_btnEnterCall);
        _statusPanel.Controls.Add(_btnCopyLink);
        Controls.Add(_statusPanel);

        // 4. Consola de Logs en Tiempo Real
        var consoleHeader = new Label
        {
            Text = "REGISTRO EN TIEMPO REAL:",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 155, 164),
            Dock = DockStyle.Top,
            Height = 22,
            Padding = new Padding(16, 6, 0, 0),
            BackColor = Color.FromArgb(30, 31, 34)
        };
        Controls.Add(consoleHeader);

        _txtConsole = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 18, 20),
            ForeColor = Color.FromArgb(219, 222, 225),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        Controls.Add(_txtConsole);

        // Subscribir logs de los servicios
        _serverService.OnLog += msg => Log(msg, Color.FromArgb(219, 222, 225));
        _tunnelService.OnLog += msg => Log(msg, Color.FromArgb(88, 101, 242));

        // Separadores de dock en orden correcto
        configCard.BringToFront();
        _statusPanel.BringToFront();
        consoleHeader.BringToFront();
        _txtConsole.BringToFront();

        Log("Consola de servidor inicializada. Pulsa 'Iniciar Servidor' para comenzar.", Color.FromArgb(148, 155, 164));
    }

    private void EnableDarkTitleBar()
    {
        try
        {
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int));
        }
        catch { }
    }

    private void Log(string message, Color color)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message, color)));
            return;
        }

        try
        {
            _txtConsole.SelectionStart = _txtConsole.TextLength;
            _txtConsole.SelectionLength = 0;
            _txtConsole.SelectionColor = color;
            _txtConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _txtConsole.ScrollToCaret();
        }
        catch { }
    }

    private async Task OnToggleHostClickedAsync()
    {
        if (_isStarting) return;

        if (_serverService.IsRunning)
        {
            await StopServerInternalAsync();
        }
        else
        {
            await StartServerInternalAsync();
        }
    }

    private async Task StartServerInternalAsync()
    {
        var room = _txtServerName.Text.Trim().ToLowerInvariant();
        var pass = _txtPassword.Text.Trim();

        if (string.IsNullOrWhiteSpace(room))
        {
            MessageBox.Show("Por favor ingresa un nombre para tu servidor.", "Servidor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtServerName.Focus();
            return;
        }

        _isStarting = true;
        _btnToggleHost.Enabled = false;
        _btnToggleHost.Text = "Iniciando servidor y túnel...";
        _txtServerName.Enabled = false;
        _txtPassword.Enabled = false;

        _lblStatus.Text = "⏳ Iniciando Kestrel...";
        _lblStatus.ForeColor = Color.Yellow;

        Log($"[Host] Guardando configuración: Servidor=#{room}", Color.White);
        _settingsService.Settings.Channel = room;
        _settingsService.Settings.Password = pass;
        _settingsService.Settings.IsHosting = true;
        _settingsService.SaveSettings();

        // 1. Iniciar Kestrel
        Log("[Host] [1/3] Iniciando backend Kestrel (Lowcord.Server.exe)...", Color.Cyan);
        var serverOk = await _serverService.StartServerAsync(room);
        if (!serverOk)
        {
            Log("[ERROR] No se pudo iniciar el proceso de Lowcord.Server.", Color.FromArgb(242, 63, 67));
            MessageBox.Show(
                "No se pudo iniciar Lowcord.Server.exe.\nAsegúrate de que no haya otro servidor en ejecución y de tener la carpeta 'Servidor' en su lugar.",
                "Error al Iniciar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            ResetToStopped();
            return;
        }

        Log("[Host] ✓ Servidor Kestrel online en http://127.0.0.1:8080", Color.FromArgb(35, 165, 90));

        // 2. Iniciar Túnel Cloudflare
        _lblStatus.Text = "⏳ Conectando Túnel Cloudflare...";
        Log("[Host] [2/3] Levantando túnel HTTPS público con Cloudflare...", Color.Cyan);
        _currentTunnelUrl = await _tunnelService.StartTunnelAsync(8080);

        // 3. Registrar Servidor en el Índice Global
        _lblStatus.Text = "⏳ Registrando servidor...";
        Log("[Host] [3/3] Registrando servidor en red Keyval...", Color.Cyan);
        var hostUser = _settingsService.Settings.Username;
        if (!string.IsNullOrEmpty(_currentTunnelUrl))
        {
            await _roomRegistry.RegisterRoomAsync(room, _currentTunnelUrl, !string.IsNullOrEmpty(pass), hostUser);
            Log($"[Host] ✓ Servidor registrado en Keyval como #{room}", Color.FromArgb(35, 165, 90));
        }
        else
        {
            Log("[Host] ⚠️ Túnel público no disponible. El servidor funcionará localmente.", Color.Orange);
        }

        // Estado Online
        _isStarting = false;
        _btnToggleHost.Enabled = true;
        _btnToggleHost.Text = "⏹ Detener Servidor";
        _btnToggleHost.BackColor = Color.FromArgb(218, 55, 60);

        _lblStatus.Text = $"🟢 En Línea: #{room}";
        _lblStatus.ForeColor = Color.FromArgb(35, 165, 90);
        _btnEnterCall.Visible = true;
        _btnCopyLink.Visible = true;

        var status = new HostStatus
        {
            IsRunning = true,
            RoomName = room,
            TunnelUrl = _currentTunnelUrl,
            Message = "Servidor online"
        };
        _onHostStatusChanged?.Invoke(status);

        Log("==========================================================", Color.FromArgb(35, 165, 90));
        Log($"★ SERVIDOR ACTIVO: #{room}", Color.Yellow);
        Log($"★ Tus amigos pueden conectarse buscando #{room} en Lowcord", Color.White);
        if (!string.IsNullOrEmpty(_currentTunnelUrl))
        {
            Log($"★ Enlace web: https://fakuinsa.github.io/lowcord/?room={room}", Color.FromArgb(88, 101, 242));
        }
        Log("==========================================================", Color.FromArgb(35, 165, 90));
    }

    private async Task StopServerInternalAsync()
    {
        _btnToggleHost.Enabled = false;
        _btnToggleHost.Text = "Deteniendo servidor...";
        _lblStatus.Text = "⏳ Deteniendo procesos...";
        _lblStatus.ForeColor = Color.Yellow;

        var room = _txtServerName.Text.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(room))
        {
            Log($"[Host] Desregistrando servidor #{room}...", Color.Yellow);
            await _roomRegistry.UnregisterRoomAsync(room);
        }

        Log("[Host] Deteniendo túnel Cloudflare...", Color.Yellow);
        await _tunnelService.StopTunnelAsync();

        Log("[Host] Deteniendo Kestrel...", Color.Yellow);
        await _serverService.StopServerAsync();

        ResetToStopped();

        var status = new HostStatus
        {
            IsRunning = false,
            RoomName = "",
            TunnelUrl = null,
            Message = "Servidor detenido"
        };
        _onHostStatusChanged?.Invoke(status);

        Log("[Host] Servidor detenido completamente.", Color.FromArgb(148, 155, 164));
    }

    private void ResetToStopped()
    {
        _isStarting = false;
        _currentTunnelUrl = null;
        _btnToggleHost.Enabled = true;
        _btnToggleHost.Text = "▶ Iniciar Servidor";
        _btnToggleHost.BackColor = Color.FromArgb(35, 165, 90);
        _txtServerName.Enabled = true;
        _txtPassword.Enabled = true;

        _lblStatus.Text = "⚪ Servidor apagado";
        _lblStatus.ForeColor = Color.FromArgb(148, 155, 164);
        _btnEnterCall.Visible = false;
        _btnCopyLink.Visible = false;

        _settingsService.Settings.IsHosting = false;
        _settingsService.SaveSettings();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_serverService.IsRunning)
        {
            var res = MessageBox.Show(
                "Tu servidor está en línea.\n\n¿Deseas detener el servidor y finalizar la llamada?\n(Pulsa 'No' para mantenerlo funcionando en segundo plano)",
                "Cerrar Panel del Servidor",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                // Detener servidor y cerrar
                _ = StopServerInternalAsync();
            }
            else if (res == DialogResult.No)
            {
                // Ocultar la ventana en lugar de cerrar para que siga corriendo
                e.Cancel = true;
                Hide();
                return;
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }
}
