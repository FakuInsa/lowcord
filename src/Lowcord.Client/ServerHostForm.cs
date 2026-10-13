using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Lowcord.Client;

public class ServerHostForm : Form
{
    private readonly MainForm _mainForm;
    private TextBox _txtServerName = null!;
    private Button _btnStart = null!;
    private Button _btnCopyLink = null!;
    private Button _btnClear = null!;
    private RichTextBox _txtConsole = null!;
    private Process? _serverProc;
    private Process? _cfProc;
    private string? _tunnelUrl;
    private bool _isRunning = false;
    private System.Windows.Forms.Timer? _keepAliveTimer;

    public ServerHostForm(string initialName, MainForm mainForm)
    {
        _mainForm = mainForm;
        InitializeUi(initialName);
    }

    private void InitializeUi(string initialName)
    {
        Text = "Lowcord - Consola del Servidor (Host)";
        Size = new Size(680, 480);
        MinimumSize = new Size(540, 360);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(20, 20, 24);
        ForeColor = Color.White;

        // Panel Superior de Controles
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(32, 34, 38),
            Padding = new Padding(12)
        };

        var lblName = new Label
        {
            Text = "Nombre del Servidor (Código):",
            Location = new Point(14, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(219, 222, 225)
        };

        _txtServerName = new TextBox
        {
            Location = new Point(14, 30),
            Width = 160,
            Text = string.IsNullOrWhiteSpace(initialName) ? "facu" : initialName,
            BackColor = Color.FromArgb(30, 31, 34),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnStart = new Button
        {
            Location = new Point(184, 27),
            Size = new Size(110, 30),
            Text = "Iniciar",
            BackColor = Color.FromArgb(35, 165, 90),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnStart.FlatAppearance.BorderSize = 0;
        _btnStart.Click += async (s, e) =>
        {
            if (!_isRunning)
            {
                await StartServerAsync();
            }
            else
            {
                await StopServerAsync();
            }
        };

        _btnCopyLink = new Button
        {
            Location = new Point(304, 27),
            Size = new Size(130, 30),
            Text = "📋 Copiar Enlace",
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnCopyLink.FlatAppearance.BorderSize = 0;
        _btnCopyLink.Click += (s, e) =>
        {
            var serverName = _txtServerName.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(serverName)) serverName = "facu";
            var link = $"https://fakuinsa.github.io/lowcord/?room={serverName}";
            try
            {
                Clipboard.SetText(link);
                _btnCopyLink.Text = "✓ ¡Copiado!";
                var t = new System.Windows.Forms.Timer { Interval = 2000 };
                t.Tick += (ts, te) =>
                {
                    _btnCopyLink.Text = "📋 Copiar Enlace";
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            }
            catch { }
        };

        _btnClear = new Button
        {
            Location = new Point(444, 27),
            Size = new Size(80, 30),
            Text = "Limpiar",
            BackColor = Color.FromArgb(53, 55, 60),
            ForeColor = Color.FromArgb(181, 186, 193),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.Click += (s, e) => _txtConsole.Clear();

        topPanel.Controls.Add(lblName);
        topPanel.Controls.Add(_txtServerName);
        topPanel.Controls.Add(_btnStart);
        topPanel.Controls.Add(_btnCopyLink);
        topPanel.Controls.Add(_btnClear);

        // Consola de Comandos (Estilo CMD / Terminal)
        _txtConsole = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 12, 12),
            ForeColor = Color.FromArgb(0, 230, 118),
            Font = new Font("Consolas", 9.5f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        Controls.Add(_txtConsole);
        Controls.Add(topPanel);

        // Banner inicial en la consola
        Log("Microsoft Windows [Consola de Servidor Lowcord]");
        Log("(c) Lowcord Voice & Screen P2P Server.");
        Log("Haz clic en 'Iniciar' para encender el servidor y generar el enlace para tus amigos.\r\n", Color.FromArgb(148, 155, 164));
    }

    public void Log(string message, Color? color = null)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message, color));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}\r\n";
        _txtConsole.SelectionStart = _txtConsole.TextLength;
        _txtConsole.SelectionLength = 0;
        _txtConsole.SelectionColor = color ?? Color.FromArgb(0, 230, 118);
        _txtConsole.AppendText(line);
        _txtConsole.ScrollToCaret();
    }

    private async Task StartServerAsync()
    {
        var serverName = _txtServerName.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(serverName)) serverName = "facu";
        _txtServerName.Text = serverName;

        _btnStart.Text = "Creando...";
        _btnStart.Enabled = false;
        _txtServerName.Enabled = false;
        _btnCopyLink.Visible = false;

        Log("==================================================================", Color.Cyan);
        Log($"[CMD] INICIANDO SERVIDOR PARA LA SALA: '{serverName}'", Color.Cyan);
        Log("==================================================================", Color.Cyan);

        // 1. Detener procesos previos
        await Task.Run(() =>
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("cloudflared")) try { p.Kill(); } catch { }
                foreach (var p in Process.GetProcessesByName("Lowcord.Server")) try { p.Kill(); } catch { }
            }
            catch { }
        });

        // 2. Buscar ejecutable del servidor
        var serverExe = _mainForm.FindServerExecutable();
        if (string.IsNullOrEmpty(serverExe) || !File.Exists(serverExe))
        {
            Log("[ERROR] No se encontró el ejecutable 'Lowcord.Server.exe' en la carpeta Servidor.", Color.FromArgb(242, 63, 67));
            Log("Asegúrate de que la carpeta 'Servidor' esté junto a la aplicación.", Color.Orange);
            ResetToStopped();
            return;
        }

        // 3. Iniciar Lowcord.Server
        Log($"[CMD] [1/3] Iniciando servidor Kestrel en http://localhost:8080...", Color.Yellow);
        try
        {
            var roomFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "room_code.txt");
            try { File.WriteAllText(roomFile, serverName); } catch { }

            var srvPsi = new ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"--room \"{serverName}\"",
                WorkingDirectory = Path.GetDirectoryName(serverExe)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _serverProc = new Process { StartInfo = srvPsi, EnableRaisingEvents = true };
            _serverProc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log($"[Server] {e.Data}", Color.LightGray);
                }
            };
            _serverProc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log($"[Server-Err] {e.Data}", Color.OrangeRed);
                }
            };
            _serverProc.Exited += (s, e) =>
            {
                if (_isRunning)
                {
                    Log("[Server] El proceso del servidor se cerró inesperadamente.", Color.Orange);
                    BeginInvoke(async () => await StopServerAsync());
                }
            };
            _serverProc.Start();
            _serverProc.BeginOutputReadLine();
            _serverProc.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Falló al iniciar Lowcord.Server: {ex.Message}", Color.FromArgb(242, 63, 67));
            ResetToStopped();
            return;
        }

        // Esperar a que el servidor responda
        bool serverReady = false;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(300);
            if (await _mainForm.IsLocalServerRunningAsync())
            {
                serverReady = true;
                break;
            }
        }
        if (serverReady)
        {
            Log("[Server] ✓ Servidor local Kestrel activo en http://localhost:8080", Color.FromArgb(35, 165, 90));
        }

        // 4. Iniciar Cloudflare Tunnel
        Log("[CMD] [2/3] Conectando túnel seguro Cloudflare (HTTPS)...", Color.Yellow);
        var cloudflaredExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.exe");
        var cfLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.log");
        if (File.Exists(cfLog))
        {
            try { File.Delete(cfLog); } catch { }
        }

        if (!File.Exists(cloudflaredExe))
        {
            Log("[Cloudflare] Descargando binario oficial cloudflared...", Color.Yellow);
            try
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var bytes = await hc.GetByteArrayAsync("https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe");
                await File.WriteAllBytesAsync(cloudflaredExe, bytes);
                Log("[Cloudflare] Descarga completada con éxito.", Color.Green);
            }
            catch (Exception ex)
            {
                Log($"[Cloudflare] Aviso descarga: {ex.Message}", Color.Orange);
            }
        }

        if (File.Exists(cloudflaredExe))
        {
            try
            {
                var cfPsi = new ProcessStartInfo
                {
                    FileName = cloudflaredExe,
                    Arguments = $"tunnel --url http://localhost:8080 --logfile \"{cfLog}\" --proxy-dns --proxy-dns-upstream \"https://1.1.1.1/dns-query\"",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _cfProc = Process.Start(cfPsi);
            }
            catch (Exception ex)
            {
                Log($"[Cloudflare-Err] No se pudo lanzar cloudflared: {ex.Message}", Color.FromArgb(242, 63, 67));
            }

            // Buscar la URL en el log
            _tunnelUrl = null;
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(500);
                if (File.Exists(cfLog))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(cfLog);
                        var match = Regex.Match(content, @"(https://[a-zA-Z0-9-]+\.trycloudflare\.com)");
                        if (match.Success)
                        {
                            _tunnelUrl = match.Groups[1].Value;
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        // 5. Publicar en keyval.org
        if (!string.IsNullOrEmpty(_tunnelUrl))
        {
            Log($"[Cloudflare] ✓ Túnel HTTPS establecido:", Color.FromArgb(35, 165, 90));
            Log($"             {_tunnelUrl}", Color.White);
            Log($"[CMD] [3/3] Publicando código '{serverName}' en la red...", Color.Yellow);

            try
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var encoded = Uri.EscapeDataString(_tunnelUrl);
                await hc.GetStringAsync($"https://api.keyval.org/set/lowcord_{serverName}/{encoded}");
                Log($"[CMD] ✓ Sala vinculada correctamente con: {serverName}", Color.FromArgb(35, 165, 90));
            }
            catch (Exception ex)
            {
                Log($"[AVISO] No se pudo vincular con keyval.org: {ex.Message}", Color.Orange);
            }

            Log("==================================================================", Color.Cyan);
            Log($"★ SERVIDOR ACTIVO Y ONLINE", Color.Cyan);
            Log($"★ CÓDIGO PARA TUS AMIGOS: {serverName}", Color.Yellow);
            Log($"★ ENLACE COMPARTIR: https://fakuinsa.github.io/lowcord/?room={serverName}", Color.LightGreen);
            Log("==================================================================", Color.Cyan);
            Log("Tus amigos pueden ingresar a Lowcord y colocar este código.", Color.White);
            Log("Mantén esta consola abierta mientras juegues con ellos.", Color.FromArgb(148, 155, 164));
        }
        else
        {
            Log("[AVISO] No se obtuvo túnel público. Solo accesible localmente en red.", Color.Orange);
        }

        _isRunning = true;
        _btnStart.Text = "Detener";
        _btnStart.BackColor = Color.FromArgb(218, 55, 60);
        _btnStart.Enabled = true;
        _btnCopyLink.Visible = true;

        // Mantener viva la publicación cada 2 minutos
        _keepAliveTimer = new System.Windows.Forms.Timer { Interval = 120000 };
        _keepAliveTimer.Tick += async (s, e) =>
        {
            if (_isRunning && !string.IsNullOrEmpty(_tunnelUrl))
            {
                try
                {
                    using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                    var encoded = Uri.EscapeDataString(_tunnelUrl);
                    await hc.GetStringAsync($"https://api.keyval.org/set/lowcord_{serverName}/{encoded}");
                }
                catch { }
            }
        };
        _keepAliveTimer.Start();

        _mainForm.OnHostServerStarted(serverName);
    }

    private async Task StopServerAsync()
    {
        _btnStart.Text = "Deteniendo...";
        _btnStart.Enabled = false;

        _keepAliveTimer?.Stop();
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;

        var serverName = _txtServerName.Text.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(serverName))
        {
            try
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                await hc.GetStringAsync($"https://api.keyval.org/set/lowcord_{serverName}/offline");
            }
            catch { }
        }

        await Task.Run(() =>
        {
            try
            {
                if (_serverProc != null && !_serverProc.HasExited) _serverProc.Kill();
                if (_cfProc != null && !_cfProc.HasExited) _cfProc.Kill();
                foreach (var p in Process.GetProcessesByName("cloudflared")) try { p.Kill(); } catch { }
                foreach (var p in Process.GetProcessesByName("Lowcord.Server")) try { p.Kill(); } catch { }
            }
            catch { }
        });

        _serverProc = null;
        _cfProc = null;
        _tunnelUrl = null;

        Log("[CMD] Servidor y túnel detenidos correctamente.", Color.Yellow);

        ResetToStopped();
        _mainForm.OnHostServerStopped();
    }

    private void ResetToStopped()
    {
        _isRunning = false;
        _btnStart.Text = "Iniciar";
        _btnStart.BackColor = Color.FromArgb(35, 165, 90);
        _btnStart.Enabled = true;
        _txtServerName.Enabled = true;
        _btnCopyLink.Visible = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isRunning)
        {
            var res = MessageBox.Show(
                "¿Deseas detener el servidor y desconectar a tus amigos?",
                "Cerrar Servidor Lowcord",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Detener procesos al cerrar la ventana
            try
            {
                _keepAliveTimer?.Stop();
                if (_serverProc != null && !_serverProc.HasExited) _serverProc.Kill();
                if (_cfProc != null && !_cfProc.HasExited) _cfProc.Kill();
                foreach (var p in Process.GetProcessesByName("cloudflared")) try { p.Kill(); } catch { }
                foreach (var p in Process.GetProcessesByName("Lowcord.Server")) try { p.Kill(); } catch { }
            }
            catch { }

            _mainForm.OnHostServerStopped();
        }

        base.OnFormClosing(e);
    }
}
