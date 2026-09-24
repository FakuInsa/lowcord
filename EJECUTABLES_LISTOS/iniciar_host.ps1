param(
    [switch]$NoClient
)

$Host.UI.RawUI.WindowTitle = "Lowcord - Consola del Servidor (Activo)"

$serverExe = Join-Path $PSScriptRoot "Servidor\Lowcord.Server.exe"
$clientExe = Join-Path $PSScriptRoot "Lowcord-Client.exe"
$cloudflaredExe = Join-Path $PSScriptRoot "cloudflared.exe"
$logFile = Join-Path $PSScriptRoot "cloudflared.log"

Clear-Host
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "                 LOWCORD - SERVIDOR DE LLAMADAS P2P                       " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

# 1. Detener procesos previos para no duplicar puertos
Get-Process -Name "Lowcord.Server", "cloudflared" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

# 2. Iniciar Servidor (Visible / administrado por esta consola)
Write-Host ""
Write-Host " [1/3] Iniciando Servidor WebRTC en http://localhost:8080..." -ForegroundColor Yellow
$procServer = Start-Process -FilePath $serverExe -WorkingDirectory (Split-Path -Parent $serverExe) -PassThru

# 3. Iniciar Cloudflare Tunnel
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

# 4. Esperar enlace de Cloudflare
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

# 4.5. Resolver codigo de sala (lee room_code.txt si existe, o genera uno unico)
$codeFile = Join-Path $PSScriptRoot "room_code.txt"
$roomCode = $null
if (Test-Path $codeFile) {
    $custom = (Get-Content $codeFile -Raw -ErrorAction SilentlyContinue).Trim().ToLower()
    if ($custom) { $roomCode = $custom }
}
if (-not $roomCode) {
    $defaultRoom = ($env:USERNAME -replace '[^a-zA-Z0-9]', '').ToLower()
    if (-not $defaultRoom) { $defaultRoom = "sala-" + (Get-Random -Minimum 1000 -Maximum 9999) }
    $roomCode = $defaultRoom
    Set-Content -Path $codeFile -Value $roomCode -Encoding UTF8
}

if ($tunnelUrl) {
    try {
        $encodedUrl = [System.Uri]::EscapeDataString($tunnelUrl)
        Invoke-RestMethod -Uri "https://api.keyval.org/set/lowcord_$roomCode/$encodedUrl" -TimeoutSec 5 -ErrorAction SilentlyContinue | Out-Null
    } catch {}
    Set-Clipboard -Value "https://fakuinsa.github.io/lowcord"
}

# 5. Iniciar cliente nativo si no esta en modo dedicado (-NoClient)
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
    Write-Host "  (Tus amigos con Lowcord-Client.exe entran automaticamente sin link!)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ENLACE WEB FIJO (Navegador PC o Celular - NUNCA CAMBIA):" -ForegroundColor Cyan
    Write-Host "  https://fakuinsa.github.io/lowcord" -ForegroundColor Yellow
    Write-Host "  (Ya copiado a tu portapapeles - Compartelo 1 sola vez)" -ForegroundColor DarkGray
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
