using System.Collections.Generic;
using System.Text.Json;
using Lowcord.Client.Models;

namespace Lowcord.Client.Bridge;

public static class LobbyHtmlBuilder
{
    public static string BuildLobbyHtml(
        string appVersion,
        string username,
        HostStatus hostStatus,
        string hostDefaultRoom,
        string hostDefaultPassword,
        List<RoomInfo> rooms,
        string? pendingUpdateVersion)
    {
        var safeUser = System.Net.WebUtility.HtmlEncode(username);
        var safeHostRoom = System.Net.WebUtility.HtmlEncode(hostDefaultRoom);
        var safeHostPass = System.Net.WebUtility.HtmlEncode(hostDefaultPassword);
        var roomsJson = JsonSerializer.Serialize(rooms);

        var updateHtml = "";
        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            updateHtml = $@"
        <div class='update-alert' id='updateAlertBox'>
          <span id='updateAlertText'>🚀 <b>Nueva versión disponible: {pendingUpdateVersion}</b></span>
          <button type='button' id='btnUpdateNow' class='btn-mini-update' onclick='triggerUpdate()'>Actualizar Ahora</button>
        </div>";
        }

        var shareLink = hostStatus.IsRunning ? $"https://fakuinsa.github.io/lowcord/?room={hostStatus.RoomName}" : "";

        return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Lowcord</title>
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
    .container {{
      width: 480px;
      max-width: 95vw;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }}
    .header-card {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 12px 18px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      box-shadow: 0 8px 24px rgba(0,0,0,0.3);
    }}
    .brand {{
      display: flex;
      align-items: center;
      gap: 10px;
    }}
    .brand-logo {{
      width: 34px;
      height: 34px;
      background: linear-gradient(135deg, #5865F2 0%, #4752c4 100%);
      border-radius: 9px;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 12px rgba(88, 101, 242, 0.4);
    }}
    .brand-logo svg {{ width: 20px; height: 20px; fill: #fff; }}
    .brand-title {{ font-size: 18px; font-weight: 700; color: #fff; }}
    
    .header-actions {{
      display: flex;
      align-items: center;
      gap: 8px;
    }}
    .user-pill {{
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 6px 12px;
      border-radius: 20px;
      font-size: 13px;
      font-weight: 600;
      color: #dbdee1;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 6px;
      transition: background 0.15s;
    }}
    .user-pill:hover {{ background: #35373c; color: #fff; }}
    .btn-icon {{
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #dbdee1;
      padding: 6px 10px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 15px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s;
    }}
    .btn-icon:hover {{ background: #35373c; color: #fff; border-color: rgba(255, 255, 255, 0.2); }}

    /* Banner si tu servidor está activo */
    .host-active-banner {{
      background: rgba(35, 165, 90, 0.15);
      border: 1px solid #23a55a;
      border-radius: 12px;
      padding: 10px 14px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 13px;
      color: #23a55a;
      box-shadow: 0 4px 16px rgba(35, 165, 90, 0.15);
    }}
    .host-active-info {{ display: flex; align-items: center; gap: 8px; font-weight: 600; }}
    .host-active-dot {{ width: 8px; height: 8px; border-radius: 50%; background: #23a55a; animation: pulse 1.5s infinite; }}

    .card {{
      background: #2b2d31;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px 22px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.35);
    }}
    .card-title {{
      font-size: 12px;
      font-weight: 700;
      color: #949ba4;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 12px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }}
    
    .servers-list {{
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-height: 190px;
      overflow-y: auto;
      margin-bottom: 16px;
    }}
    .server-item {{
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 9px;
      padding: 12px 14px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      transition: all 0.15s ease;
    }}
    .server-item:hover {{
      background: #35373c;
      border-color: rgba(255, 255, 255, 0.15);
      transform: translateX(2px);
    }}
    .server-name {{ font-weight: 700; font-size: 15px; color: #fff; display: flex; align-items: center; gap: 6px; }}
    .server-meta {{ font-size: 12px; color: #949ba4; margin-top: 2px; }}

    .empty-state {{
      background: #1e1f22;
      border: 1px dashed rgba(255, 255, 255, 0.1);
      border-radius: 9px;
      padding: 22px 16px;
      text-align: center;
      color: #949ba4;
      font-size: 12.5px;
      line-height: 1.5;
    }}

    .divider-row {{
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 14px 0 12px 0;
      color: #80848e;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}
    .divider-row::before, .divider-row::after {{
      content: '';
      flex: 1;
      height: 1px;
      background: rgba(255, 255, 255, 0.08);
    }}

    .form-row {{
      display: flex;
      gap: 8px;
    }}
    .input-box {{
      flex: 1;
      background: #1e1f22;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 7px;
      color: #fff;
      padding: 10px 12px;
      font-size: 13.5px;
      outline: none;
      transition: border-color 0.15s;
    }}
    .input-box:focus {{ border-color: #5865F2; }}
    .input-pass {{ max-width: 140px; }}

    .btn {{
      padding: 10px 16px;
      border-radius: 7px;
      font-size: 13.5px;
      font-weight: 600;
      border: none;
      cursor: pointer;
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
    }}
    .btn:active {{ transform: scale(0.99); }}
    .btn-primary {{ background: #5865F2; color: #fff; }}
    .btn-primary:hover {{ background: #4752c4; }}
    .btn-success {{ background: #23a55a; color: #fff; }}
    .btn-success:hover {{ background: #1d8a4b; }}
    .btn-danger {{ background: #da373c; color: #fff; }}
    .btn-danger:hover {{ background: #a1282c; }}
    .btn-secondary {{ background: #4e5058; color: #fff; }}
    .btn-secondary:hover {{ background: #6d6f78; }}
    .btn-full {{ width: 100%; }}

    /* Modal de Configuración y Host */
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
    .modal-header h3 {{ font-size: 16px; font-weight: 700; color: #fff; }}
    .btn-close {{ background: transparent; border: none; color: #949ba4; font-size: 22px; cursor: pointer; }}
    .btn-close:hover {{ color: #fff; }}
    .modal-section {{ margin-bottom: 16px; }}
    .modal-label {{ display: block; font-size: 11.5px; font-weight: 700; color: #b5bac1; text-transform: uppercase; margin-bottom: 6px; }}
    .modal-hint {{ display: block; font-size: 11.5px; color: #949ba4; margin-top: 5px; }}

    .update-alert {{
      background: rgba(88, 101, 242, 0.2);
      border: 1px solid #5865F2;
      border-radius: 8px;
      padding: 8px 12px;
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
    }}
    @keyframes pulse {{
      0%, 100% {{ opacity: 1; }}
      50% {{ opacity: 0.4; }}
    }}
    @keyframes fadeIn {{
      from {{ opacity: 0; transform: scale(0.97); }}
      to {{ opacity: 1; transform: scale(1); }}
    }}
  </style>
</head>
<body>
  <div class='container'>
    {updateHtml}

    <!-- Encabezado Limpio -->
    <div class='header-card'>
      <div class='brand'>
        <div class='brand-logo'>
          <svg viewBox='0 0 24 24'><path d='M12 3a9 9 0 0 0-9 9v7a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H5v-3a7 7 0 1 1 14 0v3h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-7a9 9 0 0 0-9-9z'/></svg>
        </div>
        <div class='brand-title'>Lowcord</div>
      </div>
      <div class='header-actions'>
        <div class='user-pill' onclick='openProfileModal()' title='Tu nombre (Clic para cambiar)'>
          <span>👤 {safeUser}</span>
        </div>
        <button type='button' class='btn btn-primary' onclick='openHostWindow()' style='padding: 6px 14px; font-size: 13px; font-weight: 600;' title='Abrir Panel de Control del Servidor'>
          🖥️ Crear Servidor
        </button>
      </div>
    </div>

    <!-- Si tu servidor está activo, banner destacado -->
    {(hostStatus.IsRunning ? $@"
    <div class='host-active-banner'>
      <div class='host-active-info'>
        <div class='host-active-dot'></div>
        <span>Tu Servidor está Online: <b>#{hostStatus.RoomName}</b></span>
      </div>
      <div style='display: flex; gap: 6px;'>
        <button type='button' class='btn btn-success' style='padding: 5px 12px; font-size: 11.5px;' onclick='joinOwnServer()'>📞 Entrar a Mi Servidor</button>
        <button type='button' class='btn btn-secondary' style='padding: 5px 10px; font-size: 11.5px;' onclick='openHostWindow()'>⚙️ Panel Servidor</button>
        <button type='button' class='btn btn-secondary' style='padding: 5px 10px; font-size: 11.5px;' onclick='copyLink(""{shareLink}"")'>📋 Copiar Enlace</button>
        <button type='button' class='btn btn-danger' style='padding: 5px 10px; font-size: 11.5px;' onclick='stopHost()'>⏹ Detener</button>
      </div>
    </div>" : "")}

    <!-- TARJETA PRINCIPAL: SERVIDORES Y CONEXIÓN -->
    <div class='card'>
      <div class='card-title'>
        <span>🖥️ Servidores en Línea</span>
        <span style='cursor: pointer; color: #5865F2; font-size: 11px;' onclick='refreshServers()'>↻ Actualizar</span>
      </div>

      <div class='servers-list' id='serversContainer'>
        <!-- Renderizado dinámicamente -->
      </div>

      <div class='divider-row'>O entra escribiendo el nombre</div>

      <!-- Formulario para entrar directo a cualquier servidor -->
      <form onsubmit='handleDirectJoin(event)' style='display: flex; flex-direction: column; gap: 8px;'>
        <div class='form-row'>
          <input type='text' id='joinServerInput' class='input-box' placeholder='Nombre del Servidor (ej: facu)' required autocomplete='off'>
          <input type='password' id='joinPassInput' class='input-box input-pass' placeholder='Contraseña (opcional)' autocomplete='off'>
        </div>
        <button type='submit' id='btnJoinDirect' class='btn btn-primary btn-full'>
          Conectarse al Servidor
        </button>
      </form>
    </div>

    <div class='footer-note'>
      {appVersion} • Atajo global (M) para mutearte en cualquier juego • Pulsa F2 para cambiar de servidor
    </div>
  </div>

  <!-- MODAL RÁPIDO: PERFIL DE USUARIO -->
  <div id='profileModal' class='modal-backdrop' style='display: none;'>
    <div class='modal-window' style='width: 380px;'>
      <div class='modal-header'>
        <h3>Tu Nombre de Usuario</h3>
        <button type='button' class='btn-close' onclick='closeProfileModal()'>&times;</button>
      </div>

      <div class='modal-section'>
        <label class='modal-label'>Tu Nombre o Apodo:</label>
        <div class='form-row'>
          <input type='text' id='modalNameInput' class='input-box' value='{safeUser}' maxlength='25' autocomplete='off'>
          <button type='button' class='btn btn-primary' onclick='saveName()'>Guardar</button>
        </div>
        <small class='modal-hint'>Tus amigos verán este nombre cuando jueguen contigo.</small>
      </div>

      <div style='display: flex; justify-content: flex-end; margin-top: 14px;'>
        <button type='button' class='btn btn-secondary' onclick='closeProfileModal()'>Cerrar</button>
      </div>
    </div>
  </div>

  <script>
    var currentServers = {roomsJson};
    var currentUser = '{safeUser}';

    function renderServers(servers) {{
      if (servers) currentServers = servers;
      var container = document.getElementById('serversContainer');
      container.innerHTML = '';

      if (!currentServers || currentServers.length === 0) {{
        container.innerHTML = ""<div class='empty-state'><div style='font-size: 24px; margin-bottom: 6px;'>🎧</div><b>No hay servidores públicos activos ahora.</b><br>Haz clic en <b>'🖥️ Crear Servidor'</b> arriba para ser el anfitrión, o conéctate con el nombre abajo.</div>"";
        return;
      }}

      currentServers.forEach(function(s) {{
        var item = document.createElement('div');
        item.className = 'server-item';
        var lockIcon = s.HasPassword ? ' 🔒' : '';
        var meta = (s.HostUser ? 'Anfitrión: ' + s.HostUser + ' • ' : '') + 'En línea';

        item.innerHTML = `
          <div>
            <div class='server-name'>🖥️ #${{s.RoomId}}${{lockIcon}}</div>
            <div class='server-meta'>${{meta}}</div>
          </div>
          <button type='button' class='btn btn-primary' style='padding: 6px 14px; font-size: 12.5px;'>Conectar</button>
        `;

        item.querySelector('button').addEventListener('click', function() {{
          connectToServer(s.RoomId, s.HasPassword, s.TunnelUrl);
        }});

        container.appendChild(item);
      }});
    }}

    function connectToServer(serverId, hasPassword, directUrl) {{
      var pass = '';
      if (hasPassword) {{
        pass = prompt('Ingresa la contraseña para el servidor #' + serverId + ':') || '';
      }}
      postMsg('JOIN_ROOM:' + JSON.stringify({{
        room: serverId,
        user: currentUser,
        pass: pass,
        url: directUrl || ''
      }}));
    }}

    function joinOwnServer() {{
      postMsg('JOIN_ROOM:' + JSON.stringify({{
        room: '{safeHostRoom}',
        user: currentUser,
        pass: '{safeHostPass}',
        url: 'http://localhost:8080'
      }}));
    }}

    function handleDirectJoin(e) {{
      e.preventDefault();
      var server = (document.getElementById('joinServerInput').value || '').trim().toLowerCase();
      var pass = (document.getElementById('joinPassInput').value || '').trim();
      if (!server) return;

      var btn = document.getElementById('btnJoinDirect');
      btn.disabled = true;
      btn.innerText = 'Conectando al Servidor...';

      postMsg('JOIN_ROOM:' + JSON.stringify({{
        room: server,
        user: currentUser,
        pass: pass,
        url: ''
      }}));
    }}

    function openHostWindow() {{
      postMsg('OPEN_HOST_WINDOW');
    }}

    function stopHost() {{
      postMsg('STOP_HOST');
    }}

    function openProfileModal() {{
      document.getElementById('profileModal').style.display = 'flex';
    }}

    function closeProfileModal() {{
      document.getElementById('profileModal').style.display = 'none';
    }}

    document.getElementById('profileModal').addEventListener('click', function(e) {{
      if (e.target === this) closeProfileModal();
    }});

    function saveName() {{
      var n = (document.getElementById('modalNameInput').value || '').trim();
      if (n) {{
        postMsg('SET_NAME:' + n);
        closeProfileModal();
      }}
    }}

    function refreshServers() {{
      postMsg('FETCH_ROOMS');
    }}

    function copyLink(link) {{
      navigator.clipboard.writeText(link).then(function() {{
        alert('Enlace copiado al portapapeles:\n' + link);
      }}).catch(function() {{
        prompt('Copia este enlace para tus amigos:', link);
      }});
    }}

    function triggerUpdate() {{
      var btn = document.getElementById('btnUpdateNow');
      var txt = document.getElementById('updateAlertText');
      if (btn) {{ btn.disabled = true; btn.innerText = 'Descargando...'; }}
      if (txt) {{ txt.innerHTML = '⏳ <b>Descargando actualización...</b>'; }}
      postMsg('EXECUTE_UPDATE');
    }}

    function postMsg(data) {{
      if (window.chrome && window.chrome.webview) {{
        window.chrome.webview.postMessage(data);
      }}
    }}

    if (window.chrome && window.chrome.webview) {{
      window.chrome.webview.addEventListener('message', function(event) {{
        try {{
          var data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
          if (data && data.type === 'ROOMS_DATA') {{
            renderServers(data.rooms);
          }}
          if (data && data.type === 'UPDATE_PROGRESS') {{
            var btn = document.getElementById('btnUpdateNow');
            var txt = document.getElementById('updateAlertText');
            if (data.msg && txt) txt.innerHTML = '🚀 <b>' + data.msg + '</b>';
            if (btn && data.percent !== undefined) btn.innerText = data.percent >= 100 ? 'Reiniciando...' : (data.percent + '%');
          }}
        }} catch(e) {{}}
      }});
    }}

    renderServers();
  </script>
</body>
</html>";
    }
}
