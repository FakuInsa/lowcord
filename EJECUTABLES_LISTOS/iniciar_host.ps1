param(
    [switch]$NoClient
)

$Host.UI.RawUI.WindowTitle = "Lowcord - Consola del Servidor (Activo)"

$serverExe = Join-Path $PSScriptRoot "Servidor\Lowcord.Server.exe"
$clientExe = Join-Path $PSScriptRoot "Lowcord-Client.exe"
$cloudflaredExe = Join-Path $PSScriptRoot "cloudflared.exe"
$logFile = Join-Path $PSScriptRoot "cloudflared.log"
$codeFile = Join-Path $PSScriptRoot "room_code.txt"

Clear-Host
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "                 LOWCORD - SERVIDOR DE LLAMADAS P2P                       " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

# 1. Configurar o confirmar nombre de sala (letras y numeros)
$previousRoom = $null
if (Test-Path $codeFile) {
    $previousRoom = (Get-Content $codeFile -Raw -ErrorAction SilentlyContinue).Trim().ToLower()
}
$suggested = if ($previousRoom) { $previousRoom } else { "sala" + (Get-Random -Minimum 10 -Maximum 99) }

Write-Host ""
Write-Host "  CONFIGURACION DE TU SALA:" -ForegroundColor Yellow
Write-Host "  Elige el nombre de tu sala (letras y numeros, ej: charla12, squad5)." -ForegroundColor White
Write-Host "  Presiona [ENTER] para usar: " -NoNewline -ForegroundColor Gray
Write-Host "[$suggested]" -ForegroundColor Green -NoNewline
Write-Host " o escribe uno nuevo:" -ForegroundColor Gray

$inputRoom = Read-Host "  > Nombre de sala"
$inputRoom = ($inputRoom -replace '[^a-zA-Z0-9_-]', '').Trim().ToLower()

$roomCode = if ($inputRoom) { $inputRoom } else { $suggested }
Set-Content -Path $codeFile -Value $roomCode -Encoding UTF8

Write-Host "  -> Sala activa fijada en: $roomCode" -ForegroundColor Cyan
Write-Host ""

# 2. Detener procesos previos para no duplicar puertos
Get-Process -Name "Lowcord.Server", "cloudflared" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

# 3. Iniciar Servidor (Visible / administrado por esta consola)
Write-Host " [1/3] Iniciando Servidor WebRTC en http://localhost:8080..." -ForegroundColor Yellow
$procServer = Start-Process -FilePath $serverExe -WorkingDirectory (Split-Path -Parent $serverExe) -PassThru

# 4. Iniciar Cloudflare Tunnel
Write-Host " [2/3] Conectando Tunel Seguro Cloudflare (HTTPS)..." -ForegroundColor Yellow
if (-not (Test-Path $cloudflaredExe)) {
    Write-Host "       Descargando ejecutable oficial de Cloudflare Tunnel..." -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile $cloudflaredExe
    } catch {
        Write-Host "       Aviso: No se pudo descargar cloudflared automaticamente: $_" -ForegroundColor Red
    }
}
$procCf = Start-Process -FilePath $cloudflaredExe -ArgumentList "tunnel --url http://localhost:8080 --logfile `"$logFile`"" -PassThru -WindowStyle Minimized

# 5. Esperar enlace de Cloudflare
$tunnelUrl = $null
$attempts = 0
while ($attempts -lt 25 -and -not $tunnelUrl) {
    Start-Sleep -Milliseconds 600
    if (Test-Path $logFile) {
        $content = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
        if ($content -match "(https://[a-zA-Z0-9-]+\.trycloudflare\.com)") {
            $tunnelUrl = $matches[1]
        }
    }
    $attempts++
}

# 6. Publicar sala en resolvedor P2P
if ($tunnelUrl) {
    try {
        $encodedUrl = [System.Uri]::EscapeDataString($tunnelUrl)
        Invoke-RestMethod -Uri "https://api.keyval.org/set/lowcord_$roomCode/$encodedUrl" -TimeoutSec 5 -ErrorAction SilentlyContinue | Out-Null
    } catch {}
    $shareUrl = "https://fakuinsa.github.io/lowcord/?room=$roomCode"
    Set-Clipboard -Value $shareUrl
}

# 7. Iniciar cliente nativo si no esta en modo dedicado (-NoClient)
if (-not $NoClient) {
    Write-Host " [3/3] Abriendo aplicacion Lowcord..." -ForegroundColor Yellow
    Start-Process -FilePath $clientExe -WorkingDirectory $PSScriptRoot
} else {
    Write-Host " [3/3] Modo Servidor Dedicado (sin cliente grafico local)..." -ForegroundColor Magenta
}

Clear-Host
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "                 LOWCORD - SERVIDOR ACTIVO Y VISIBLE                      " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host ""
if ($tunnelUrl) {
    Write-Host "  CODIGO DE TU SALA: " -NoNewline -ForegroundColor Green
    Write-Host "  $roomCode  " -ForegroundColor Black -BackgroundColor Yellow
    Write-Host "  (Tus amigos abren Lowcord y solo escriben este codigo para entrar!)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ENLACE WEB (Navegador PC o Celular):" -ForegroundColor Cyan
    Write-Host "  https://fakuinsa.github.io/lowcord/?room=$roomCode" -ForegroundColor Yellow
    Write-Host "  (Ya copiado a tu portapapeles - Compartelo con tus amigos)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  ENLACE DIRECTO DE SESION:" -ForegroundColor DarkGray
    Write-Host "  $tunnelUrl" -ForegroundColor DarkGray
    Write-Host ""
} else {
    Write-Host "  Servidor local listo en: http://localhost:8080" -ForegroundColor White
}
if ($NoClient) {
    Write-Host "  MODO: SERVIDOR DEDICADO (Solo backend + tunel activo)." -ForegroundColor Magenta
} else {
    Write-Host "  ESTADO: SERVIDOR ACTIVO (Consola abierta de forma transparente)." -ForegroundColor Green
}
Write-Host "  Manten esta ventana abierta mientras juegues con tus amigos." -ForegroundColor Gray
Write-Host ""
Write-Host "  OPCIONES DE CIERRE:" -ForegroundColor Cyan
Write-Host "  - DETENER.bat      -> Cierra tu servidor pero deja a tus amigos en P2P." -ForegroundColor White
Write-Host "  - DETENER-TODO.bat -> Cierra la sala completa y desconecta a todos." -ForegroundColor White
Write-Host "  - O presiona Ctrl + C en esta ventana para salir." -ForegroundColor DarkGray
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host ""

$lastPublish = [System.Diagnostics.Stopwatch]::StartNew()
try {
    while ($true) {
        if ($procServer.HasExited) {
            Write-Host "El servidor local se ha detenido." -ForegroundColor Red
            break
        }
        if ($tunnelUrl -and $lastPublish.Elapsed.TotalMinutes -ge 2) {
            try {
                $encodedUrl = [System.Uri]::EscapeDataString($tunnelUrl)
                Invoke-RestMethod -Uri "https://api.keyval.org/set/lowcord_$roomCode/$encodedUrl" -TimeoutSec 5 -ErrorAction SilentlyContinue | Out-Null
                $lastPublish.Restart()
            } catch {}
        }
        Start-Sleep -Seconds 2
    }
}
finally {
    Write-Host "Cerrando procesos locales..." -ForegroundColor Gray
    try {
        Invoke-RestMethod -Uri "https://api.keyval.org/set/lowcord_$roomCode/offline" -TimeoutSec 2 -ErrorAction SilentlyContinue | Out-Null
    } catch {}
    if ($procServer -and -not $procServer.HasExited) { Stop-Process -Id $procServer.Id -Force -ErrorAction SilentlyContinue }
    if ($procCf -and -not $procCf.HasExited) { Stop-Process -Id $procCf.Id -Force -ErrorAction SilentlyContinue }
}
