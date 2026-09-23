# Lowcord

> **Chat de voz y pantalla compartida P2P ultra-ligero, seguro y de baja latencia.**

Lowcord es una alternativa autoalojada y minimalista a Discord. Permite llamadas de voz y streaming de pantalla a 60 FPS en alta calidad sin intermediarios que espíen tus datos ni consumos abusivos de memoria RAM o CPU.

---

## Características Principales

- **Conexión P2P Directa (WebRTC Mesh):** El tráfico de audio y video viaja directamente entre los participantes mediante túneles cifrados de extremo a extremo (**DTLS-SRTP**).
- **Consumo Ultra Bajo (<0.2% CPU):** Motor de audio optimizado con detección de volumen throttled, evitando sobrecarga en procesador y GPU.
- **Túnel Seguro Automático (Cloudflare Tunnel):** Tus amigos se conectan mediante un enlace seguro HTTPS sin necesidad de abrir puertos en tu router ni configurar DDNS.
- **Sin Instalación para Amigos:** Tus amigos pueden unirse directamente desde su navegador favorito (Chrome, Brave, Edge, Firefox, Opera o móvil).
- **Cliente Nativo de Escritorio (Windows C#):**
  - Atajo global de teclado para silenciar/desmutear mientras juegas en pantalla completa sin hacer `Alt + Tab`.
  - Soporte para **Actividad de Voz (VAD)** con puerta de ruido (*noise gate*) calibrable y **Pulsar para Hablar (*PTT*)**.
- **Compartir Pantalla en Alta Calidad:** Soporte para transmisión fluida a 60 FPS y audio del sistema.
- **Dos Modos de Detención:**
  - `DETENER.bat`: Apaga tu servidor local pero permite que tus amigos continúen hablando por la red P2P autónoma.
  - `DETENER-TODO.bat`: Cierra la sala de forma instantánea para todos los participantes.

---

## Estructura del Proyecto

```text
Lowcord/
├── src/
│   ├── Lowcord.Server/       # Servidor de señalización SignalR y servidor web (.NET 9)
│   │   └── wwwroot/          # Interfaz web WebRTC (HTML5, CSS3, JS Vanilla)
│   └── Lowcord.Client/       # Cliente nativo WinForms + WebView2 con atajos globales
├── EJECUTABLES_LISTOS/       # Scripts de ejecución del host
│   └── iniciar_host.ps1      # Orquestador del servidor y túnel
├── INICIAR.bat               # Inicia la consola del servidor y abre la app
├── DETENER.bat               # Cierra tu instancia (amigos siguen en P2P)
├── DETENER-TODO.bat          # Cierra la sala para todos
└── COMPILAR.bat              # Compila el proyecto desde el código fuente
```

---

## Cómo Iniciar

1. Ejecuta **`INICIAR.bat`**.
2. La consola iniciará el servidor local y generará un enlace seguro de Cloudflare.
3. El enlace se copiará automáticamente a tu portapapeles. Pégaselo a tus amigos por WhatsApp, Discord o Steam.
4. ¡Listo! Ya están conectados en llamada directa.

---

## Cómo Compilar desde el Código Fuente

Requisitos:
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

Simplemente ejecuta:
```bash
COMPILAR.bat
```
O manualmente mediante CLI:
```bash
dotnet publish src/Lowcord.Server/Lowcord.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o EJECUTABLES_LISTOS/Servidor
dotnet publish src/Lowcord.Client/Lowcord.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src/Lowcord.Client/bin/publish
```

---

## Licencia

Distribuido bajo la Licencia MIT.
