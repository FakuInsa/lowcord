/**
 * Lowcord - P2P Mesh WebRTC Client & SignalR Signaling
 * Ultra-ligero, cifrado de extremo a extremo (DTLS-SRTP).
 */

// Iconos vectoriales limpios (SVG)
const ICONS = {
  micOn: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72h-1.7z"/></svg>',
  micOff: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23c.56-.98.9-2.09.9-3.28zm-4.02.17c0-.06.02-.11.02-.17V5c0-1.66-1.34-3-3-3S9 3.34 9 5v.18l5.98 5.99zM4.27 3L3 4.27l6.01 6.01V11c0 1.66 1.33 3 2.99 3 .22 0 .44-.03.65-.08l1.66 1.66c-.71.33-1.5.52-2.31.52-2.76 0-5.3-2.1-5.3-5.1H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c.91-.13 1.77-.45 2.54-.9L19.73 21 21 19.73 4.27 3z"/></svg>',
  screen: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.11-.9-2-2-2H4c-1.11 0-2 .89-2 2v10c0 1.1.89 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z"/></svg>',
  screenStop: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4 14H8V8h8v8z"/></svg>',
  fullscreen: '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z"/></svg>',
  hide: '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z"/></svg>',
  pin: '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M16 12V4h1V2H7v2h1v8l-2 2v2h5.2v6h1.6v-6H18v-2l-2-2z"/></svg>',
  vol: '<svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/></svg>',
  volMute: '<svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z"/></svg>',
  audioActive: '<svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M12 3v18l-4-4H4V7h4l4-4zm6.5 9c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z"/></svg>'
};

// Estado global de la aplicación
let connection = null;
let myConnectionId = null;
let myUserName = 'Usuario';
let currentRoomId = 'general';

let localAudioStream = null;
let localScreenStream = null;
let isMicMuted = false;
let isScreenSharing = false;

// AudioContext global para reproducción garantizada por Web Audio API
let sharedAudioContext = null;
function getAudioContext() {
  if (!sharedAudioContext) {
    sharedAudioContext = new (window.AudioContext || window.webkitAudioContext)();
  }
  if (sharedAudioContext.state === 'suspended') {
    sharedAudioContext.resume().catch(e => {});
  }
  return sharedAudioContext;
}

// Configuración de audio general
let currentInputDeviceId = localStorage.getItem('lowcord_input_device') || '';
let currentOutputDeviceId = localStorage.getItem('lowcord_output_device') || '';
let currentOutputVolume = parseInt(localStorage.getItem('lowcord_output_volume') || '100', 10);

// Configuración avanzada de voz y micrófono
let inputMode = localStorage.getItem('lowcord_input_mode') || 'vad'; // 'vad' (actividad de voz) o 'ptt' (pulsar para hablar)
let vadThreshold = parseInt(localStorage.getItem('lowcord_vad_threshold') || '20', 10);
let activeKeybind = {
  code: localStorage.getItem('lowcord_keybind_code') || 'KeyM',
  label: localStorage.getItem('lowcord_keybind_label') || 'M'
};
let noiseSuppressionEnabled = localStorage.getItem('lowcord_noise_suppression') !== 'false';

// Configuración de pantalla compartida (resolución, FPS y ahorro de GPU)
let screenQuality = {
  resolution: parseInt(localStorage.getItem('lowcord_screen_res') || '1080', 10),
  fps: parseInt(localStorage.getItem('lowcord_screen_fps') || '60', 10),
  localPreview: localStorage.getItem('lowcord_screen_preview') === 'true' // false por defecto para máximo ahorro de GPU
};

let isPttActive = false;
let isGateOpen = true;
let gateHangoverTimer = null;
let pttHangoverTimer = null;
let isRecordingKeybind = false;

let rawMicStream = null;
let micSourceNode = null;
let micHighPassFilter = null;
let micGateGainNode = null;
let outboundMicDestination = null;
let signalrPingInterval = null;

const peers = new Map();
const hiddenScreens = new Set();
const screenVolumes = new Map(); // id -> volumen 0.0 - 1.0

const rtcConfig = {
  iceServers: [
    { urls: 'stun:stun.l.google.com:19302' },
    { urls: 'stun:stun1.l.google.com:19302' },
    { urls: 'stun:stun2.l.google.com:19302' },
    { urls: 'stun:stun.cloudflare.com:3478' }
  ],
  iceCandidatePoolSize: 10
};

// Elementos del DOM
const joinModal = document.getElementById('join-modal');
const joinForm = document.getElementById('join-form');
const joinError = document.getElementById('join-error');
const appContainer = document.getElementById('app-container');
const displayRoomName = document.getElementById('display-room-name');
const userCount = document.getElementById('user-count');
const stageContainer = document.getElementById('stage-container');
const participantsGrid = document.getElementById('participants-grid');
const screenShareGrid = document.getElementById('screen-share-grid');
const hiddenScreensBar = document.getElementById('hidden-screens-bar');
const btnToggleAvatars = document.getElementById('btn-toggle-avatars');
const remoteAudios = document.getElementById('remote-audios');

const btnToggleMic = document.getElementById('btn-toggle-mic');
const btnToggleScreen = document.getElementById('btn-toggle-screen');
const btnDisconnect = document.getElementById('btn-disconnect');
const iconMic = document.getElementById('icon-mic');
const iconScreen = document.getElementById('icon-screen');
const labelMic = document.getElementById('label-mic');
const labelScreen = document.getElementById('label-screen');
const localMicBadge = document.getElementById('local-mic-badge');

// Configuración de Audio Modal
const settingsModal = document.getElementById('settings-modal');
const btnOpenSettings = document.getElementById('btn-open-settings');
const btnCloseSettings = document.getElementById('btn-close-settings');
const selectAudioInput = document.getElementById('select-audio-input');
const selectAudioOutput = document.getElementById('select-audio-output');
const sliderVolume = document.getElementById('slider-volume');
const volumeValDisplay = document.getElementById('volume-val-display');
const micMeterFill = document.getElementById('mic-meter-fill');

// Controles avanzados de voz
const btnModeVad = document.getElementById('btn-mode-vad');
const btnModePtt = document.getElementById('btn-mode-ptt');
const vadSettingsBox = document.getElementById('vad-settings-box');
const sliderSensitivity = document.getElementById('slider-sensitivity');
const sensitivityValDisplay = document.getElementById('sensitivity-val-display');
const sensitivityCutoffLine = document.getElementById('sensitivity-cutoff-line');
const keybindTitleLabel = document.getElementById('keybind-title-label');
const btnRecordKeybind = document.getElementById('btn-record-keybind');
const keybindDisplayText = document.getElementById('keybind-display-text');
const keybindStatusHint = document.getElementById('keybind-status-hint');
const chkNoiseSuppression = document.getElementById('chk-noise-suppression');

// Modal de calidad de pantalla
const screenQualityModal = document.getElementById('screen-quality-modal');
const btnRes720 = document.getElementById('btn-res-720');
const btnRes1080 = document.getElementById('btn-res-1080');
const btnFps30 = document.getElementById('btn-fps-30');
const btnFps60 = document.getElementById('btn-fps-60');
const chkLocalPreview = document.getElementById('chk-local-preview');
const btnCancelScreen = document.getElementById('btn-cancel-screen');
const btnConfirmScreen = document.getElementById('btn-confirm-screen');

// Toggle de sonido de silenciado / activación (estilo Discord)
let muteSoundsEnabled = localStorage.getItem('lowcord_mute_sounds') !== 'false';
const chkMuteSound = document.getElementById('chk-mute-sound');

// Miniatura flotante de vista previa (PiP local)
const streamMiniPreview = document.getElementById('stream-mini-preview');
const miniPreviewVideo = document.getElementById('mini-preview-video');
const miniPreviewBadge = document.getElementById('mini-preview-badge');
const btnCloseMiniPreview = document.getElementById('btn-close-mini-preview');

iconMic.innerHTML = ICONS.micOn;
iconScreen.innerHTML = ICONS.screen;
localMicBadge.innerHTML = ICONS.micOn;

btnToggleAvatars.addEventListener('click', () => {
  const isHidden = stageContainer.classList.toggle('hide-participants');
  btnToggleAvatars.innerText = isHidden ? 'Mostrar participantes' : 'Ocultar participantes';
});

// Pre-cargar valores de sala y usuario desde URL y localStorage
(function prefillInputs() {
  try {
    const urlParams = new URLSearchParams(window.location.search);
    const roomParam = (urlParams.get('room') || localStorage.getItem('lowcord_room_id') || '').trim().toLowerCase();
    if (roomParam) {
      const roomInput = document.getElementById('room-id');
      if (roomInput) roomInput.value = roomParam;
    }
    const savedUser = localStorage.getItem('lowcord_username');
    if (savedUser) {
      const userInput = document.getElementById('username');
      if (userInput && !userInput.value) userInput.value = savedUser;
    }
  } catch (e) {}
})();

// =========================================================
// 1. INICIALIZACIÓN Y EVENTOS DE FORMULARIO
// =========================================================
joinForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  joinError.style.display = 'none';

  // Despertar AudioContext con interacción del usuario
  getAudioContext();

  myUserName = document.getElementById('username').value.trim();
  currentRoomId = document.getElementById('room-id').value.trim().toLowerCase();
  const password = document.getElementById('room-password').value.trim();

  try {
    localStorage.setItem('lowcord_username', myUserName);
    localStorage.setItem('lowcord_room_id', currentRoomId);
  } catch (e) {}

  const btnSubmit = document.getElementById('btn-join');
  btnSubmit.disabled = true;
  btnSubmit.innerText = 'Conectando audio...';

  try {
    await initLocalAudio();
    await loadAudioDevices();
    await initSignalR();
    await connection.invoke('JoinRoom', currentRoomId, password, myUserName);

  } catch (err) {
    console.error('Error al iniciar llamada:', err);
    joinError.innerText = err.message || 'Error al conectar al micrófono o al servidor.';
    joinError.style.display = 'block';
    btnSubmit.disabled = false;
    btnSubmit.innerText = 'Unirse a la Llamada';
  }
});

function applyMicTransmissionState() {
  const shouldTransmit = !isMicMuted && (inputMode === 'ptt' ? isPttActive : isGateOpen);
  if (rawMicStream) {
    rawMicStream.getAudioTracks().forEach(track => {
      if (track.enabled !== shouldTransmit) {
        track.enabled = shouldTransmit;
      }
    });
  }
  if (localAudioStream && localAudioStream !== rawMicStream) {
    localAudioStream.getAudioTracks().forEach(track => {
      if (track.enabled !== shouldTransmit) {
        track.enabled = shouldTransmit;
      }
    });
  }
}

async function initLocalAudio() {
  const audioConstraints = {
    echoCancellation: { ideal: noiseSuppressionEnabled },
    noiseSuppression: { ideal: noiseSuppressionEnabled },
    autoGainControl: { ideal: true },
    googEchoCancellation: noiseSuppressionEnabled,
    googAutoGainControl: true,
    googNoiseSuppression: noiseSuppressionEnabled,
    googHighpassFilter: true,
    googTypingNoiseDetection: noiseSuppressionEnabled,
    googAudioMirroring: false
  };

  if (currentInputDeviceId) {
    audioConstraints.deviceId = { exact: currentInputDeviceId };
  }

  try {
    rawMicStream = await navigator.mediaDevices.getUserMedia({
      audio: audioConstraints,
      video: false
    });
  } catch (err) {
    console.warn('Fallo dispositivo guardado, usando predeterminado:', err);
    rawMicStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: noiseSuppressionEnabled,
        noiseSuppression: noiseSuppressionEnabled,
        autoGainControl: true,
        googEchoCancellation: noiseSuppressionEnabled,
        googNoiseSuppression: noiseSuppressionEnabled,
        googTypingNoiseDetection: noiseSuppressionEnabled
      },
      video: false
    });
  }

  isGateOpen = true;
  setupLocalAudioProcessing();
}

function setupLocalAudioProcessing() {
  if (!rawMicStream) return;
  localAudioStream = rawMicStream;
  const ctx = getAudioContext();

  if (micSourceNode) {
    try { micSourceNode.disconnect(); } catch(e){}
  }

  micSourceNode = ctx.createMediaStreamSource(rawMicStream);

  if (micHighPassFilter) {
    try { micHighPassFilter.disconnect(); } catch(e){}
  } else {
    // Filtro Paso Alto a 110 Hz:
    // Elimina físicamente retumbes de escritorio, golpes de teclado y viento para el análisis
    micHighPassFilter = ctx.createBiquadFilter();
    micHighPassFilter.type = 'highpass';
    micHighPassFilter.frequency.value = 110;
    micHighPassFilter.Q.value = 0.8;
  }

  // Conectar a filtro paso alto exclusivamente para el analizador de voz (VAD y medidor)
  micSourceNode.connect(micHighPassFilter);

  isGateOpen = true;
  applyMicTransmissionState();

  // Analizar la voz luego del filtro para que ruidos graves no abran la compuerta
  setupSpeakingDetection(micHighPassFilter, 'local-participant');
}

// =========================================================
// 2. CONFIGURACIÓN DE DISPOSITIVOS Y VOLUMEN GENERAL
// =========================================================
btnOpenSettings.addEventListener('click', () => {
  loadAudioDevices();
  settingsModal.style.display = 'flex';
});

btnCloseSettings.addEventListener('click', () => {
  settingsModal.style.display = 'none';
});

settingsModal.addEventListener('click', (e) => {
  if (e.target === settingsModal) settingsModal.style.display = 'none';
});

window.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && settingsModal.style.display === 'flex') {
    settingsModal.style.display = 'none';
  }
});

async function loadAudioDevices() {
  if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) return;

  try {
    const devices = await navigator.mediaDevices.enumerateDevices();

    selectAudioInput.innerHTML = '';
    selectAudioOutput.innerHTML = '';

    const audioInputs = devices.filter(d => d.kind === 'audioinput');
    const audioOutputs = devices.filter(d => d.kind === 'audiooutput');

    audioInputs.forEach((dev, idx) => {
      const opt = document.createElement('option');
      opt.value = dev.deviceId;
      opt.text = dev.label || `Micrófono ${idx + 1}`;
      if (dev.deviceId === currentInputDeviceId || (!currentInputDeviceId && idx === 0)) {
        opt.selected = true;
      }
      selectAudioInput.appendChild(opt);
    });

    if (audioOutputs.length > 0) {
      audioOutputs.forEach((dev, idx) => {
        const opt = document.createElement('option');
        opt.value = dev.deviceId;
        opt.text = dev.label || `Altavoces ${idx + 1}`;
        if (dev.deviceId === currentOutputDeviceId || (!currentOutputDeviceId && idx === 0)) {
          opt.selected = true;
        }
        selectAudioOutput.appendChild(opt);
      });
      selectAudioOutput.disabled = false;
    } else {
      const opt = document.createElement('option');
      opt.text = 'Altavoces del sistema (Predeterminado)';
      selectAudioOutput.appendChild(opt);
      selectAudioOutput.disabled = true;
    }

    sliderVolume.value = currentOutputVolume;
    volumeValDisplay.innerText = `${currentOutputVolume}%`;

  } catch (err) {
    console.error('Error al enumerar dispositivos:', err);
  }
}

if (navigator.mediaDevices && navigator.mediaDevices.addEventListener) {
  navigator.mediaDevices.addEventListener('devicechange', loadAudioDevices);
}

selectAudioInput.addEventListener('change', async () => {
  const newDeviceId = selectAudioInput.value;
  if (!newDeviceId) return;

  try {
    const newStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        deviceId: { exact: newDeviceId },
        echoCancellation: noiseSuppressionEnabled,
        noiseSuppression: noiseSuppressionEnabled,
        autoGainControl: false
      },
      video: false
    });

    if (rawMicStream) {
      rawMicStream.getTracks().forEach(t => t.stop());
    }
    rawMicStream = newStream;
    currentInputDeviceId = newDeviceId;
    localStorage.setItem('lowcord_input_device', newDeviceId);

    setupLocalAudioProcessing();

  } catch (err) {
    console.error('Error al cambiar de micrófono:', err);
    alert('No se pudo cambiar al micrófono seleccionado: ' + err.message);
  }
});

selectAudioOutput.addEventListener('change', async () => {
  const newDeviceId = selectAudioOutput.value;
  currentOutputDeviceId = newDeviceId;
  localStorage.setItem('lowcord_output_device', newDeviceId);

  document.querySelectorAll('audio').forEach(audioEl => {
    if (typeof audioEl.setSinkId === 'function') {
      audioEl.setSinkId(newDeviceId).catch(err => {});
    }
  });

  const ctx = getAudioContext();
  if (typeof ctx.setSinkId === 'function') {
    ctx.setSinkId(newDeviceId).catch(err => {});
  }
});

sliderVolume.addEventListener('input', (e) => {
  currentOutputVolume = parseInt(e.target.value, 10);
  volumeValDisplay.innerText = `${currentOutputVolume}%`;
  localStorage.setItem('lowcord_output_volume', currentOutputVolume);

  for (const [peerId, peer] of peers) {
    if (peer.audioEl) {
      const userGain = peer.userVolume !== undefined ? peer.userVolume : 1.0;
      peer.audioEl.volume = Math.min(1.0, userGain * (currentOutputVolume / 100));
    }
  }
});

// =========================================================
// 3. CONEXIÓN SIGNALR (SEÑALIZACIÓN)
// =========================================================
async function initSignalR() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/signalingHub')
    .withAutomaticReconnect()
    .build();

  connection.on('JoinedSuccessfully', async (connectionId, existingUsers) => {
    myConnectionId = connectionId;
    joinModal.style.display = 'none';
    if (document.activeElement && typeof document.activeElement.blur === 'function') {
      document.activeElement.blur();
    }
    appContainer.style.display = 'flex';
    displayRoomName.innerText = currentRoomId;
    document.getElementById('local-display-name').innerText = `${myUserName} (Tú)`;
    document.getElementById('dock-user-name').innerText = myUserName;
    document.getElementById('dock-avatar').innerText = myUserName.slice(0, 2).toUpperCase();
    document.getElementById('local-avatar-initials').innerText = myUserName.slice(0, 2).toUpperCase();
    updateUserCount();

    for (const user of existingUsers) {
      addParticipantCard(user.connectionId, user.userName, user.isMuted);
      await createPeerConnection(user.connectionId, user.userName, true);
    }
  });

  connection.on('JoinFailed', (reason) => {
    joinError.innerText = reason;
    joinError.style.display = 'block';
    const btnSubmit = document.getElementById('btn-join');
    btnSubmit.disabled = false;
    btnSubmit.innerText = 'Unirse a la Llamada';
  });

  connection.on('UserJoined', (peerId, peerUserName) => {
    addParticipantCard(peerId, peerUserName, false);
    createPeerConnection(peerId, peerUserName, false);
    updateUserCount();
  });

  connection.on('ReceiveOffer', async (senderId, sdpJson) => {
    let peer = peers.get(senderId);
    if (!peer) {
      await createPeerConnection(senderId, 'Compañero', false);
      peer = peers.get(senderId);
    }

    const offer = JSON.parse(sdpJson);
    await peer.pc.setRemoteDescription(new RTCSessionDescription(offer));

    while (peer.queue.length > 0) {
      await peer.pc.addIceCandidate(peer.queue.shift());
    }

    const answer = await peer.pc.createAnswer();
    await peer.pc.setLocalDescription(answer);
    await connection.invoke('SendAnswer', senderId, JSON.stringify(peer.pc.localDescription));
  });

  connection.on('ReceiveAnswer', async (senderId, sdpJson) => {
    const peer = peers.get(senderId);
    if (peer) {
      const answer = JSON.parse(sdpJson);
      await peer.pc.setRemoteDescription(new RTCSessionDescription(answer));
      while (peer.queue.length > 0) {
        await peer.pc.addIceCandidate(peer.queue.shift());
      }
    }
  });

  connection.on('ReceiveIceCandidate', async (senderId, candidateJson) => {
    const peer = peers.get(senderId);
    if (!peer) return;

    const candidate = new RTCIceCandidate(JSON.parse(candidateJson));
    if (peer.pc.remoteDescription && peer.pc.remoteDescription.type) {
      await peer.pc.addIceCandidate(candidate);
    } else {
      peer.queue.push(candidate);
    }
  });

  connection.on('UserMediaStateChanged', (peerId, isMuted, isScreenSharing) => {
    const card = document.getElementById(`participant-${peerId}`);
    if (card) {
      const micBadge = card.querySelector('.mic-status-icon');
      if (micBadge) {
        micBadge.innerHTML = isMuted ? ICONS.micOff : ICONS.micOn;
        micBadge.classList.toggle('muted', isMuted);
      }
    }
    if (isScreenSharing === false) {
      removeScreenCard(peerId);
    }
  });

  connection.on('UserLeft', (peerId) => {
    console.log(`[SignalR] Participante desconectado: ${peerId}`);
    closePeerConnection(peerId);
    removeParticipantCard(peerId);
    removeScreenCard(peerId);
    updateUserCount();
  });

  // Evento recibido cuando el anfitrión cierra la sala
  connection.on('ServerShuttingDown', (reason) => {
    console.warn('[SignalR] El anfitrión cerró la sala:', reason);
    terminateCallSession(reason || 'El anfitrión ha cerrado la sala. La llamada ha finalizado.');
  });

  connection.onreconnecting((error) => {
    console.warn('[SignalR] Conexión perdida con el servidor. Modo P2P autónomo activo.');
    const banner = document.getElementById('reconnect-banner');
    if (banner) {
      banner.innerHTML = '<span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:#57f287;margin-right:6px;"></span> Modo P2P autónomo (Servidor desconectado - Pueden seguir hablando)';
      banner.style.display = 'flex';
    }
  });

  connection.onreconnected(async (newConnectionId) => {
    console.log('[SignalR] Reconectado exitosamente al servidor. ID:', newConnectionId);
    myConnectionId = newConnectionId || connection.connectionId;
    const banner = document.getElementById('reconnect-banner');
    if (banner) banner.style.display = 'none';

    // Resincronizar la sesión en el servidor con el nuevo connectionId
    try {
      const password = document.getElementById('room-password').value.trim();
      await connection.invoke('JoinRoom', currentRoomId, password, myUserName);
      console.log('[SignalR] Sala resincronizada con el servidor tras reconexión.');
    } catch (err) {
      console.error('[SignalR] Error al resincronizar sala tras reconexión:', err);
    }
  });

  connection.onclose((error) => {
    console.warn('[SignalR] Servidor desconectado. La llamada continúa en modo P2P directo entre los participantes actuales.');
    const banner = document.getElementById('reconnect-banner');
    if (banner) {
      banner.innerHTML = '<span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:#57f287;margin-right:6px;"></span> Modo P2P autónomo (Servidor apagado - La llamada continúa)';
      banner.style.display = 'flex';
    }
    // No cortamos los peers: los participantes actuales siguen hablando directamente por P2P WebRTC
  });

  await connection.start();

  // Heartbeat ping cada 10s: Evita que Cloudflare Tunnel y proxies NAT cierren WebSockets por silencio
  if (signalrPingInterval) clearInterval(signalrPingInterval);
  signalrPingInterval = setInterval(async () => {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('Ping');
      } catch (e) {
        // Ignorar fallos transitorios
      }
    }
  }, 10000);
}

// =========================================================
// 4. GESTIÓN DE PEER CONNECTIONS (WEBRTC MESH)
// =========================================================
async function createPeerConnection(peerId, peerUserName, isInitiator) {
  if (peers.has(peerId)) return peers.get(peerId).pc;

  const pc = new RTCPeerConnection(rtcConfig);
  const audioEl = document.createElement('audio');
  audioEl.autoplay = true;
  audioEl.playsInline = true;
  audioEl.volume = currentOutputVolume / 100;

  if (currentOutputDeviceId && typeof audioEl.setSinkId === 'function') {
    audioEl.setSinkId(currentOutputDeviceId).catch(err => {});
  }

  remoteAudios.appendChild(audioEl);

  const peerData = {
    pc,
    userName: peerUserName,
    audioEl,
    queue: [],
    screenSenders: [],
    userVolume: 1.0,
    voiceTrackId: null,
    screenNegotiatedForPeer: false
  };
  peers.set(peerId, peerData);

  // 1. Agregar micrófono local
  if (localAudioStream) {
    localAudioStream.getAudioTracks().forEach(track => pc.addTrack(track, localAudioStream));
  }

  // 2. Si este peer ya está transmitiendo pantalla al entrar un nuevo participante:
  if (localScreenStream) {
    const vTrack = localScreenStream.getVideoTracks()[0];
    const aTrack = localScreenStream.getAudioTracks()[0];
    if (vTrack) {
      const sender = pc.addTrack(vTrack, localScreenStream);
      configureHighQualityVideoSender(sender);
      peerData.screenSenders.push(sender);
    }
    if (aTrack) {
      const aSender = pc.addTrack(aTrack, localScreenStream);
      peerData.screenSenders.push(aSender);
    }
  }

  // SOLUCIÓN LATE-JOINER: Cuando la conexión base se vuelve estable y conectada, si compartimos pantalla
  // enviamos inmediatamente la oferta de pantalla para que el recién llegado la vea!
  const tryTriggerScreenOffer = async () => {
    const isConn = (pc.connectionState === 'connected' || pc.iceConnectionState === 'connected' || pc.iceConnectionState === 'completed');
    if (isConn && pc.signalingState === 'stable' && localScreenStream && !peerData.screenNegotiatedForPeer) {
      peerData.screenNegotiatedForPeer = true;
      console.log(`[WebRTC] Enviando pantalla compartida al nuevo usuario: ${peerUserName}`);
      await renegotiatePeer(peerId);
    }
  };

  let iceRecoveryTimeout = null;

  const handleConnectionStateChange = async () => {
    tryTriggerScreenOffer();

    const iceState = pc.iceConnectionState;
    const connState = pc.connectionState;

    if (iceState === 'connected' || iceState === 'completed' || connState === 'connected') {
      if (iceRecoveryTimeout) {
        clearTimeout(iceRecoveryTimeout);
        iceRecoveryTimeout = null;
      }
    } else if (iceState === 'failed' || connState === 'failed') {
      console.warn(`[WebRTC] Conexión fallida con ${peerUserName} (ICE: ${iceState}, Conn: ${connState}). Reiniciando ICE...`);
      if (pc.signalingState === 'stable') {
        try {
          const offer = await pc.createOffer({ iceRestart: true });
          await pc.setLocalDescription(offer);
          if (connection && connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke('SendOffer', peerId, JSON.stringify(pc.localDescription));
            console.log(`[WebRTC] Oferta iceRestart enviada exitosamente a ${peerUserName}`);
          }
        } catch (e) {
          console.error(`[WebRTC] Error al reiniciar ICE con ${peerUserName}:`, e);
        }
      }
    } else if (iceState === 'disconnected') {
      // Si se desconecta momentáneamente por un corte o cambio de red, esperar 3s antes de reiniciar ICE
      if (!iceRecoveryTimeout) {
        iceRecoveryTimeout = setTimeout(async () => {
          iceRecoveryTimeout = null;
          if (peers.has(peerId) && pc.iceConnectionState === 'disconnected' && pc.signalingState === 'stable') {
            console.warn(`[WebRTC] Enlace con ${peerUserName} continúa desconectado tras 3s. Forzando iceRestart...`);
            try {
              const offer = await pc.createOffer({ iceRestart: true });
              await pc.setLocalDescription(offer);
              if (connection && connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke('SendOffer', peerId, JSON.stringify(pc.localDescription));
              }
            } catch (e) {
              console.error(`[WebRTC] Error al forzar iceRestart tras timeout:`, e);
            }
          }
        }, 3000);
      }
    }
  };

  pc.onsignalingstatechange = tryTriggerScreenOffer;
  pc.onconnectionstatechange = handleConnectionStateChange;
  pc.oniceconnectionstatechange = handleConnectionStateChange;

  pc.onicecandidate = (event) => {
    if (event.candidate && connection) {
      connection.invoke('SendIceCandidate', peerId, JSON.stringify(event.candidate));
    }
  };

  // 3. Recepción de pistas (Audio de voz, Video de pantalla y Audio de pantalla)
  pc.ontrack = (event) => {
    const track = event.track;
    console.log(`[WebRTC] Pista recibida de ${peerUserName}: kind=${track.kind}, id=${track.id}`);

    if (track.kind === 'video') {
      const stream = event.streams[0] || new MediaStream([track]);
      addScreenCard(peerId, peerUserName, stream);
    } else if (track.kind === 'audio') {
      // Distinguir entre micrófono y audio de pantalla:
      if (!peerData.voiceTrackId) {
        peerData.voiceTrackId = track.id;
        console.log(`[WebRTC] Registrada pista de voz para ${peerUserName}`);
        audioEl.srcObject = new MediaStream([track]);
        setupSpeakingDetection(audioEl.srcObject, `participant-${peerId}`);
      } else if (track.id !== peerData.voiceTrackId) {
        console.log(`[WebRTC] Audio de pantalla recibido de ${peerUserName}! Conectando audio en vivo...`);
        setupScreenAudioOutput(peerId, track);
      }
    }
  };

  if (isInitiator) {
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);
    await connection.invoke('SendOffer', peerId, JSON.stringify(pc.localDescription));
  }

  return pc;
}

// Configurar codificador WebRTC dinámicamente según la calidad seleccionada
function configureHighQualityVideoSender(sender) {
  try {
    const params = sender.getParameters();
    if (!params.encodings || params.encodings.length === 0) {
      params.encodings = [{}];
    }

    let bitrate = 6000000;
    if (screenQuality.resolution === 720 && screenQuality.fps === 30) {
      bitrate = 1500000; // 1.5 Mbps (Ultra Ahorro)
    } else if (screenQuality.resolution === 720 && screenQuality.fps === 60) {
      bitrate = 3000000; // 3.0 Mbps (720p 60 FPS)
    } else if (screenQuality.resolution === 1080 && screenQuality.fps === 30) {
      bitrate = 3500000; // 3.5 Mbps (1080p 30 FPS)
    } else {
      bitrate = 6000000; // 6.0 Mbps (1080p 60 FPS)
    }

    params.encodings[0].maxBitrate = bitrate;
    params.encodings[0].maxFramerate = screenQuality.fps;
    params.encodings[0].networkPriority = 'high';
    params.degradationPreference = 'maintain-framerate';

    sender.setParameters(params).catch(e => {});
  } catch(e) {}
}

// Reproducir audio de pantalla con Web Audio API (Destination) + fallback elemento <audio>
function setupScreenAudioOutput(peerId, audioTrack) {
  const ctx = getAudioContext();
  const screenStream = new MediaStream([audioTrack]);

  let webAudioSuccess = false;
  try {
    // 1. Conexión de audio pura por Web Audio API directa a parlantes
    const sourceNode = ctx.createMediaStreamSource(screenStream);
    const gainNode = ctx.createGain();

    const savedVol = screenVolumes.get(peerId);
    const initialVol = savedVol !== undefined ? savedVol : 1.0;
    gainNode.gain.setValueAtTime(initialVol, ctx.currentTime);

    sourceNode.connect(gainNode);
    gainNode.connect(ctx.destination);

    const peer = peers.get(peerId);
    if (peer) {
      peer.screenGainNode = gainNode;
    }
    webAudioSuccess = true;
    console.log(`[ScreenAudio] Audio de pantalla conectado a parlantes vía Web Audio API para ${peerId}`);
  } catch (err) {
    console.warn('[ScreenAudio] Web Audio API no disponible, usando fallback <audio>:', err);
  }

  // 2. Elemento <audio> dedicado (muted si Web Audio API ya reproduce, para evitar eco)
  let screenAudioEl = document.getElementById(`audio-screen-${peerId}`);
  if (!screenAudioEl) {
    screenAudioEl = document.createElement('audio');
    screenAudioEl.id = `audio-screen-${peerId}`;
    screenAudioEl.autoplay = true;
    screenAudioEl.playsInline = true;
    remoteAudios.appendChild(screenAudioEl);
  }

  screenAudioEl.srcObject = screenStream;

  if (webAudioSuccess) {
    screenAudioEl.muted = true; // Evita eco porque Web Audio API ya lo reproduce a los altavoces
  } else {
    screenAudioEl.muted = false;
    if (currentOutputDeviceId && typeof screenAudioEl.setSinkId === 'function') {
      screenAudioEl.setSinkId(currentOutputDeviceId).catch(err => {});
    }
    const savedVol = screenVolumes.get(peerId);
    screenAudioEl.volume = savedVol !== undefined ? savedVol : 1.0;
    screenAudioEl.play().catch(e => console.warn('Error al reproducir audio de pantalla (fallback):', e));
  }

  // 3. Actualizar etiqueta en pantalla compartida según estado real de la pista
  const card = document.getElementById(`screen-${peerId}`);
  if (card) {
    const badge = card.querySelector('.screen-audio-badge');
    if (badge) {
      const updateAudioBadge = () => {
        if (audioTrack.muted) {
          badge.innerHTML = 'Audio silenciado/vacío';
          badge.classList.remove('has-sound');
        } else {
          badge.innerHTML = `${ICONS.audioActive} Audio activo`;
          badge.classList.add('has-sound');
        }
      };
      updateAudioBadge();
      audioTrack.onmute = updateAudioBadge;
      audioTrack.onunmute = updateAudioBadge;
    }
  }
}

function closePeerConnection(peerId) {
  const peer = peers.get(peerId);
  if (peer) {
    if (peer.detectionTimer) {
      clearInterval(peer.detectionTimer);
      peer.detectionTimer = null;
    }
    peer.pc.close();
    if (peer.audioEl && peer.audioEl.parentNode) peer.audioEl.parentNode.removeChild(peer.audioEl);

    const screenAudio = document.getElementById(`audio-screen-${peerId}`);
    if (screenAudio && screenAudio.parentNode) screenAudio.parentNode.removeChild(screenAudio);

    peers.delete(peerId);
  }
}

function terminateCallSession(reason) {
  console.warn('[Lowcord] Terminando sesión completa de llamada:', reason);

  if (signalrPingInterval) {
    clearInterval(signalrPingInterval);
    signalrPingInterval = null;
  }

  if (localSpeakingTimer) {
    clearInterval(localSpeakingTimer);
    localSpeakingTimer = null;
  }

  // 1. Detener pantalla compartida local
  if (isScreenSharing) {
    try { stopScreenShare(); } catch(e) {}
  }

  // 2. Detener micrófono local
  if (localAudioStream) {
    try {
      localAudioStream.getTracks().forEach(t => t.stop());
    } catch(e) {}
    localAudioStream = null;
  }

  // 3. Cerrar todas las conexiones P2P inmediatamente (corta el audio entre participantes de inmediato)
  for (const [peerId, peer] of peers) {
    try {
      peer.pc.close();
      if (peer.audioEl && peer.audioEl.parentNode) peer.audioEl.parentNode.removeChild(peer.audioEl);
      if (peer.screenGainNode) {
        try { peer.screenGainNode.disconnect(); } catch(e){}
      }
    } catch (e) {}
  }
  peers.clear();

  // 4. Limpiar reproductores y Web Audio API
  if (remoteAudios) {
    remoteAudios.innerHTML = '';
  }
  if (sharedAudioContext) {
    try { sharedAudioContext.close(); } catch(e) {}
    sharedAudioContext = null;
  }

  // 5. Ocultar banner de reconexión y mostrar modal de finalización
  const banner = document.getElementById('reconnect-banner');
  if (banner) banner.style.display = 'none';

  const termModal = document.getElementById('terminated-modal');
  const termMsg = document.getElementById('terminated-msg');
  if (termMsg && reason) {
    termMsg.innerText = reason;
  }
  if (termModal) {
    termModal.style.display = 'flex';
  }
}

const btnReloadApp = document.getElementById('btn-reload-app');
if (btnReloadApp) {
  btnReloadApp.addEventListener('click', () => {
    window.location.reload();
  });
}

// =========================================================
// 5. COMPARTIR PANTALLA CON CALIDAD REGULABLE Y AHORRO GPU
// =========================================================
btnToggleScreen.addEventListener('click', () => {
  if (isScreenSharing) {
    stopScreenShare();
  } else {
    screenQualityModal.style.display = 'flex';
  }
});

function initScreenQualityUI() {
  if (!screenQualityModal) return;

  btnRes720.classList.toggle('active', screenQuality.resolution === 720);
  btnRes1080.classList.toggle('active', screenQuality.resolution === 1080);
  btnFps30.classList.toggle('active', screenQuality.fps === 30);
  btnFps60.classList.toggle('active', screenQuality.fps === 60);
  chkLocalPreview.checked = screenQuality.localPreview;

  btnRes720.addEventListener('click', () => {
    screenQuality.resolution = 720;
    btnRes720.classList.add('active');
    btnRes1080.classList.remove('active');
  });

  btnRes1080.addEventListener('click', () => {
    screenQuality.resolution = 1080;
    btnRes1080.classList.add('active');
    btnRes720.classList.remove('active');
  });

  btnFps30.addEventListener('click', () => {
    screenQuality.fps = 30;
    btnFps30.classList.add('active');
    btnFps60.classList.remove('active');
  });

  btnFps60.addEventListener('click', () => {
    screenQuality.fps = 60;
    btnFps60.classList.add('active');
    btnFps30.classList.remove('active');
  });

  chkLocalPreview.addEventListener('change', (e) => {
    screenQuality.localPreview = e.target.checked;
  });

  btnCancelScreen.addEventListener('click', () => {
    screenQualityModal.style.display = 'none';
  });

  screenQualityModal.addEventListener('click', (e) => {
    if (e.target === screenQualityModal) {
      screenQualityModal.style.display = 'none';
    }
  });

  btnConfirmScreen.addEventListener('click', async () => {
    screenQualityModal.style.display = 'none';
    localStorage.setItem('lowcord_screen_res', screenQuality.resolution);
    localStorage.setItem('lowcord_screen_fps', screenQuality.fps);
    localStorage.setItem('lowcord_screen_preview', screenQuality.localPreview);
    await startScreenShare();
  });
}

async function startScreenShare() {
  try {
    getAudioContext();

    const idealWidth = screenQuality.resolution === 720 ? 1280 : 1920;
    const idealHeight = screenQuality.resolution === 720 ? 720 : 1080;
    const idealFps = screenQuality.fps;

    localScreenStream = await navigator.mediaDevices.getDisplayMedia({
      video: {
        cursor: 'always',
        frameRate: { ideal: idealFps, max: idealFps },
        width: { ideal: idealWidth, max: idealWidth },
        height: { ideal: idealHeight, max: idealHeight }
      },
      audio: {
        echoCancellation: false,
        noiseSuppression: false,
        autoGainControl: false
      }
    });

    const videoTrack = localScreenStream.getVideoTracks()[0];
    const audioTrack = localScreenStream.getAudioTracks()[0];

    if (!audioTrack) {
      alert("Aviso sobre el Sonido:\n\nNo se detectó audio en la transmisión seleccionada.\n\n• Para PELÍCULAS o VIDEOS: Selecciona 'Pestaña de Chrome' y marca 'Compartir audio'.\n• Para JUEGOS: Selecciona 'Toda la pantalla' y marca 'Compartir audio del sistema'.\n\n(Nota: Windows no permite capturar sonido si seleccionas solo 'Ventana').");
    } else {
      console.log('[ScreenShare] Audio de pantalla capturado en alta fidelidad');
    }

    isScreenSharing = true;
    btnToggleScreen.classList.add('active');
    iconScreen.innerHTML = ICONS.screenStop;
    labelScreen.innerText = 'Dejar de Compartir';

    if (screenQuality.localPreview) {
      addScreenCard('local', `${myUserName} (Tu Pantalla)`, localScreenStream);
    } else {
      renderLocalScreenPill();
    }

    // Agregar pistas a todos los pares
    for (const [peerId, peer] of peers) {
      peer.screenSenders = [];
      if (videoTrack) {
        const vSender = peer.pc.addTrack(videoTrack, localScreenStream);
        configureHighQualityVideoSender(vSender);
        peer.screenSenders.push(vSender);
      }
      if (audioTrack) {
        console.log(`[WebRTC] Transmitiendo pista de audio de pantalla a ${peerId}`);
        const aSender = peer.pc.addTrack(audioTrack, localScreenStream);
        peer.screenSenders.push(aSender);
      }
      peer.screenNegotiatedForPeer = true;
      await renegotiatePeer(peerId);
    }

    videoTrack.onended = () => stopScreenShare();
    if (connection) connection.invoke('UpdateMediaState', isMicMuted, true);
  } catch (err) {
    console.error('Error al compartir pantalla:', err);
  }
}

async function stopScreenShare() {
  if (!isScreenSharing) return;
  isScreenSharing = false;
  btnToggleScreen.classList.remove('active');
  iconScreen.innerHTML = ICONS.screen;
  labelScreen.innerText = 'Compartir';

  toggleMiniPreview(false);

  removeScreenCard('local');
  const localPill = document.getElementById('pill-local');
  if (localPill && localPill.parentNode) localPill.parentNode.removeChild(localPill);

  if (localScreenStream) {
    localScreenStream.getTracks().forEach(t => t.stop());
  }

  for (const [peerId, peer] of peers) {
    peer.screenNegotiatedForPeer = false;
    if (peer.screenSenders && peer.screenSenders.length > 0) {
      peer.screenSenders.forEach(sender => {
        try { peer.pc.removeTrack(sender); } catch (e) {}
      });
      peer.screenSenders = [];
      await renegotiatePeer(peerId);
    }
  }

  localScreenStream = null;
  updateScreenLayout();
  if (connection) connection.invoke('UpdateMediaState', isMicMuted, false);
}

// Renderizar la barra de pantalla local con métricas en tiempo real y botones de acción
function renderLocalScreenPill() {
  let pill = document.getElementById('pill-local');
  if (!pill) {
    pill = document.createElement('div');
    pill.id = 'pill-local';
    pill.className = 'hidden-screen-pill';
    hiddenScreensBar.appendChild(pill);
  }

  const localCard = document.getElementById('screen-local');
  const hasLocalCard = !!localCard;

  pill.innerHTML = `
    <div class="pill-local-content">
      <span class="status-pulse-dot"></span>
      <span class="pill-screen-text">Tu Pantalla (<b id="pill-screen-resolution">${screenQuality.resolution}p @ ${screenQuality.fps} FPS</b>)</span>
      <div class="pill-actions">
        ${hasLocalCard ? '<button type="button" class="btn-pill-action" id="btn-restore-local-card">Mostrar</button>' : '<button type="button" class="btn-pill-action" id="btn-toggle-preview-pip" title="Ver miniatura de tu transmisión">Ver cómo se ve</button>'}
      </div>
    </div>
  `;

  const btnRestore = pill.querySelector('#btn-restore-local-card');
  if (btnRestore) {
    btnRestore.addEventListener('click', (e) => {
      e.stopPropagation();
      restoreScreen('local');
    });
  }

  const btnPip = pill.querySelector('#btn-toggle-preview-pip');
  if (btnPip) {
    btnPip.addEventListener('click', (e) => {
      e.stopPropagation();
      toggleMiniPreview();
    });
  }

  updateScreenLayout();
}

// Control de miniatura flotante (PiP)
let isMiniPreviewOpen = false;

function toggleMiniPreview(forceState) {
  if (forceState !== undefined) {
    isMiniPreviewOpen = forceState;
  } else {
    isMiniPreviewOpen = !isMiniPreviewOpen;
  }

  if (!streamMiniPreview || !miniPreviewVideo) return;

  if (isMiniPreviewOpen && isScreenSharing && localScreenStream) {
    streamMiniPreview.style.display = 'flex';
    if (miniPreviewVideo.srcObject !== localScreenStream) {
      miniPreviewVideo.srcObject = localScreenStream;
    }
    miniPreviewVideo.play().catch(e => {});
    if (miniPreviewBadge) {
      miniPreviewBadge.innerText = `${screenQuality.resolution}p @ ${screenQuality.fps} FPS`;
    }
    const btnPip = document.getElementById('btn-toggle-preview-pip');
    if (btnPip) btnPip.innerText = 'Ocultar miniatura';
  } else {
    streamMiniPreview.style.display = 'none';
    miniPreviewVideo.srcObject = null;
    isMiniPreviewOpen = false;
    const btnPip = document.getElementById('btn-toggle-preview-pip');
    if (btnPip) btnPip.innerText = 'Ver cómo se ve';
  }
}


// Ajuste rápido de calidad en caliente (sin desconectar ni cortar llamada)
async function applyStreamQualityLive(res, fps) {
  screenQuality.resolution = res;
  screenQuality.fps = fps;
  localStorage.setItem('lowcord_screen_res', res);
  localStorage.setItem('lowcord_screen_fps', fps);

  const targetWidth = res === 720 ? 1280 : 1920;
  const targetHeight = res === 720 ? 720 : 1080;

  if (localScreenStream) {
    const videoTrack = localScreenStream.getVideoTracks()[0];
    if (videoTrack && videoTrack.applyConstraints) {
      try {
        await videoTrack.applyConstraints({
          width: { ideal: targetWidth, max: targetWidth },
          height: { ideal: targetHeight, max: targetHeight },
          frameRate: { ideal: fps, max: fps }
        });
        console.log(`[WebRTC] Calidad de pantalla actualizada en vivo a ${res}p @ ${fps} FPS`);
      } catch (err) {
        console.warn('[WebRTC] applyConstraints error:', err);
      }
    }

    for (const [peerId, peer] of peers) {
      if (peer.screenSenders) {
        const vSender = peer.screenSenders.find(s => s.track && s.track.kind === 'video');
        if (vSender) {
          configureHighQualityVideoSender(vSender);
        }
      }
    }
  }

  const resLabel = document.getElementById('pill-screen-resolution');
  if (resLabel) resLabel.innerText = `${res}p @ ${fps} FPS`;

  if (miniPreviewBadge) miniPreviewBadge.innerText = `${res}p @ ${fps} FPS`;

  if (btnRes720 && btnRes1080 && btnFps30 && btnFps60) {
    btnRes720.classList.toggle('active', res === 720);
    btnRes1080.classList.toggle('active', res === 1080);
    btnFps30.classList.toggle('active', fps === 30);
    btnFps60.classList.toggle('active', fps === 60);
  }
}

async function renegotiatePeer(peerId) {
  const peer = peers.get(peerId);
  if (!peer || !connection) return;
  if (peer.pc.signalingState !== 'stable') {
    console.warn(`[WebRTC] Renegociación pospuesta con ${peerId} porque signalingState es: ${peer.pc.signalingState}`);
    return;
  }
  try {
    const offer = await peer.pc.createOffer();
    await peer.pc.setLocalDescription(offer);
    await connection.invoke('SendOffer', peerId, JSON.stringify(peer.pc.localDescription));
  } catch (err) {
    console.error(`Error al renegociar con ${peerId}:`, err);
  }
}

// =========================================================
// 6. CONTROL DE MICRÓFONO, SONIDOS Y ENTRADA (VAD / PTT)
// =========================================================

// Sonidos suaves de silenciado y desilenciado estilo Discord sintetizados con Web Audio API pura
function playAudioCue(type) {
  if (!muteSoundsEnabled) return;
  try {
    const ctx = getAudioContext();
    if (!ctx) return;
    if (ctx.state === 'suspended') {
      ctx.resume().catch(() => {});
    }

    const now = ctx.currentTime;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();

    osc.type = 'sine';

    if (type === 'mute') {
      // Tono descendente suave (estilo Discord mute): 440 Hz a 310 Hz
      osc.frequency.setValueAtTime(440, now);
      osc.frequency.exponentialRampToValueAtTime(310, now + 0.12);

      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.linearRampToValueAtTime(0.04, now + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.15);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start(now);
      osc.stop(now + 0.16);
    } else if (type === 'unmute') {
      // Tono ascendente suave (estilo Discord unmute): 310 Hz a 450 Hz
      osc.frequency.setValueAtTime(310, now);
      osc.frequency.exponentialRampToValueAtTime(450, now + 0.12);

      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.linearRampToValueAtTime(0.04, now + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.15);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start(now);
      osc.stop(now + 0.16);
    }
  } catch (e) {
    console.warn('[AudioCue] Error al sintetizar sonido:', e);
  }
}

btnToggleMic.addEventListener('click', toggleMic);

function toggleMic() {
  isMicMuted = !isMicMuted;

  // Reproducir sonido suave de confirmación
  playAudioCue(isMicMuted ? 'mute' : 'unmute');

  if (isMicMuted) {
    isGateOpen = false;
    isPttActive = false;
  } else {
    if (inputMode === 'vad') {
      isGateOpen = true;
    }
  }
  applyMicTransmissionState();

  btnToggleMic.classList.toggle('muted', isMicMuted);
  iconMic.innerHTML = isMicMuted ? ICONS.micOff : ICONS.micOn;
  labelMic.innerText = isMicMuted ? 'Desmutear' : 'Silenciar';

  if (localMicBadge) {
    localMicBadge.innerHTML = isMicMuted ? ICONS.micOff : ICONS.micOn;
    localMicBadge.classList.toggle('muted', isMicMuted);
  }

  const localEl = document.getElementById('local-participant');
  if (localEl && isMicMuted) {
    localEl.classList.remove('speaking');
  }

  if (connection) connection.invoke('UpdateMediaState', isMicMuted, isScreenSharing);
}

function setPttState(active) {
  if (inputMode !== 'ptt' || isMicMuted) return;
  if (active) {
    if (pttHangoverTimer) {
      clearTimeout(pttHangoverTimer);
      pttHangoverTimer = null;
    }
    isPttActive = true;
    applyMicTransmissionState();
  } else {
    if (pttHangoverTimer) clearTimeout(pttHangoverTimer);
    pttHangoverTimer = setTimeout(() => {
      isPttActive = false;
      applyMicTransmissionState();
      pttHangoverTimer = null;
    }, 150);
  }
  const localEl = document.getElementById('local-participant');
  if (localEl) {
    localEl.classList.toggle('speaking', isPttActive);
  }
}

// Salir del canal de voz y regresar a la pantalla de Selección de Canales
async function leaveVoiceChannel() {
  // 1. Detener micrófono y pantalla local
  if (rawMicStream) {
    rawMicStream.getTracks().forEach(t => t.stop());
    rawMicStream = null;
  }
  localAudioStream = null;

  if (localScreenStream) {
    localScreenStream.getTracks().forEach(t => t.stop());
    localScreenStream = null;
    isScreenSharing = false;
  }

  // 2. Cerrar todas las conexiones peer WebRTC
  peers.forEach((p, id) => closePeerConnection(id));
  peers.clear();

  // 3. Desconectar SignalR para que el servidor notifique inmediatamente a todos
  if (connection) {
    try { await connection.stop(); } catch(e) {}
    connection = null;
  }

  // 4. Limpiar tarjetas remotas del DOM
  const remoteCards = participantsGrid.querySelectorAll('.participant-card:not(.local-card)');
  remoteCards.forEach(c => c.remove());
  if (screenShareGrid) {
    screenShareGrid.innerHTML = '';
    screenShareGrid.style.display = 'none';
  }
  stageContainer.classList.remove('has-screens', 'hide-participants');

  // 5. Ocultar la pantalla de llamada y volver a mostrar la Selección de Canales
  appContainer.style.display = 'none';
  joinModal.style.display = 'flex';

  const btnSubmit = document.getElementById('btn-join');
  if (btnSubmit) {
    btnSubmit.disabled = false;
    btnSubmit.innerText = 'Unirse al Canal';
  }
}

// Botón "Salir" del dock en la llamada: vuelve a la selección de canales
btnDisconnect.addEventListener('click', () => {
  if (confirm('¿Deseas salir del canal de voz?')) {
    leaveVoiceChannel();
  }
});

// Botón "Desconectar del Servidor" en la selección de canales: sale del servidor completamente
const btnDisconnectServer = document.getElementById('btn-disconnect-server');
if (btnDisconnectServer) {
  btnDisconnectServer.addEventListener('click', () => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage('CHANGE_SERVER');
    } else {
      window.location.href = 'https://fakuinsa.github.io/lowcord/';
    }
  });
}

window.addEventListener('beforeunload', () => {
  try {
    if (rawMicStream) {
      rawMicStream.getTracks().forEach(t => t.stop());
    }
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      connection.stop();
    }
  } catch (e) {}
});

// =========================================================
// 7. DETECTOR DE VOZ Y PUERTA DE RUIDO (NOISE GATE)
//    Altamente optimizado: intervalos throttled + caché de estado
// =========================================================
let localSpeakingTimer = null;
let lastLocalSpeakingState = false;

function setupSpeakingDetection(streamOrNode, containerId) {
  try {
    const audioContext = getAudioContext();
    if (audioContext.state === 'suspended') {
      audioContext.resume().catch(e => {});
    }

    const analyser = audioContext.createAnalyser();
    analyser.fftSize = 256;
    analyser.smoothingTimeConstant = 0.4;

    if (streamOrNode instanceof AudioNode) {
      streamOrNode.connect(analyser);
    } else {
      const source = audioContext.createMediaStreamSource(streamOrNode);
      source.connect(analyser);
    }

    // Nodo sumidero silencioso obligatorio:
    // En Chromium (Blink), si un nodo AnalyserNode no llega a un AudioDestinationNode,
    // el pipeline de audio no procesa tramas (devuelve todo ceros).
    // Conectamos a un Gain de volumen 0 hacia destination para bombear las muestras.
    const silentGain = audioContext.createGain();
    silentGain.gain.value = 0.0;
    analyser.connect(silentGain);
    silentGain.connect(audioContext.destination);

    const buffer = new Uint8Array(analyser.frequencyBinCount);

    if (containerId === 'local-participant') {
      if (localSpeakingTimer) {
        clearInterval(localSpeakingTimer);
        localSpeakingTimer = null;
      }

      const localEl = document.getElementById(containerId);

      // Throttling a 40ms (25 FPS): Fluidez total de VAD consumiendo <0.1% de CPU
      localSpeakingTimer = setInterval(() => {
        analyser.getByteFrequencyData(buffer);
        let sum = 0;
        for (let i = 0; i < buffer.length; i++) sum += buffer[i];
        const avg = sum / buffer.length;
        const pct = Math.min(100, Math.round((avg / 50) * 100));

        // 1. Actualizar barra visual ÚNICAMENTE si la ventana de ajustes está abierta
        if (settingsModal && settingsModal.style.display === 'flex' && micMeterFill) {
          micMeterFill.style.width = `${pct}%`;
          if (pct >= vadThreshold) {
            micMeterFill.classList.add('voice-active');
          } else {
            micMeterFill.classList.remove('voice-active');
          }
        }

        // 2. Control de Puerta de Ruido en modo Actividad de Voz (VAD)
        if (!isMicMuted && inputMode === 'vad') {
          if (vadThreshold <= 0 || pct >= vadThreshold) {
            if (gateHangoverTimer) {
              clearTimeout(gateHangoverTimer);
              gateHangoverTimer = null;
            }
            if (!isGateOpen) {
              isGateOpen = true;
              applyMicTransmissionState();
            }
          } else {
            if (isGateOpen && !gateHangoverTimer) {
              gateHangoverTimer = setTimeout(() => {
                isGateOpen = false;
                applyMicTransmissionState();
                gateHangoverTimer = null;
              }, 400); // 400ms para no cortar colas de palabras
            }
          }
        }

        // 3. Indicador de habla en el avatar local (Caché de estado: Cero recálculos de DOM si no hay cambio)
        const isSpeakingNow = !isMicMuted && (inputMode === 'ptt' ? isPttActive : isGateOpen);
        if (isSpeakingNow !== lastLocalSpeakingState) {
          lastLocalSpeakingState = isSpeakingNow;
          if (localEl) localEl.classList.toggle('speaking', isSpeakingNow);
        }
      }, 40);

    } else {
      // Participante remoto: Throttling a 80ms (12.5 FPS)
      const element = document.getElementById(containerId);
      const peerId = containerId.replace('participant-', '');
      const peer = peers.get(peerId);

      let lastRemoteSpeaking = false;
      const remoteTimer = setInterval(() => {
        if (!peers.has(peerId) || !document.getElementById(containerId)) {
          clearInterval(remoteTimer);
          return;
        }

        analyser.getByteFrequencyData(buffer);
        let sum = 0;
        for (let i = 0; i < buffer.length; i++) sum += buffer[i];
        const avg = sum / buffer.length;

        const isSpeaking = avg > 15;
        if (isSpeaking !== lastRemoteSpeaking) {
          lastRemoteSpeaking = isSpeaking;
          if (element) element.classList.toggle('speaking', isSpeaking);
        }
      }, 80);

      if (peer) {
        if (peer.detectionTimer) clearInterval(peer.detectionTimer);
        peer.detectionTimer = remoteTimer;
      }
    }
  } catch (e) {
    console.warn('Error en detector de voz:', e);
  }
}

// =========================================================
// GESTIÓN DE TECLADO, ATAJOS PERSONALIZADOS Y WEBVIEW2
// =========================================================
window.addEventListener('keydown', (e) => {
  if (isRecordingKeybind) {
    e.preventDefault();
    e.stopPropagation();
    if (e.key === 'Escape') {
      finishKeybindRecording(null);
      return;
    }
    let code = e.code;
    let label = e.key.toUpperCase();
    if (code.startsWith('Key')) label = code.slice(3);
    else if (code === 'Space') label = 'Espacio';
    else if (code.startsWith('Digit')) label = code.slice(5);
    else if (code.startsWith('Numpad')) label = 'Num ' + code.slice(6);
    else if (code === 'CapsLock') label = 'Bloq Mayús';
    else if (code === 'Tab') label = 'Tab';

    finishKeybindRecording({ code, label });
    return;
  }

  // Si el usuario está escribiendo en un campo de texto realmente visible, respetar la escritura
  const activeEl = document.activeElement;
  if (activeEl && ['INPUT', 'SELECT', 'TEXTAREA'].includes(activeEl.tagName) && activeEl.offsetParent !== null) {
    return;
  }

  const isMatchingKey = (e.code && e.code === activeKeybind.code) ||
                        (e.key && e.key.toUpperCase() === activeKeybind.label.toUpperCase());

  if (isMatchingKey) {
    if (inputMode === 'vad') {
      if (!e.repeat) {
        toggleMic();
      }
    } else if (inputMode === 'ptt') {
      if (!e.repeat) {
        setPttState(true);
      }
    }
  }
});

window.addEventListener('keyup', (e) => {
  const activeEl = document.activeElement;
  if (activeEl && ['INPUT', 'SELECT', 'TEXTAREA'].includes(activeEl.tagName) && activeEl.offsetParent !== null) {
    return;
  }

  const isMatchingKey = (e.code && e.code === activeKeybind.code) ||
                        (e.key && e.key.toUpperCase() === activeKeybind.label.toUpperCase());

  if (inputMode === 'ptt' && isMatchingKey) {
    setPttState(false);
  }
});

// Receptor de mensajes del cliente nativo C# (Atajo global en juegos/apps de escritorio)
if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', (event) => {
    try {
      const msg = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
      if (msg.type === 'GLOBAL_KEYDOWN' || msg.type === 'HOTKEY_MUTE_TOGGLE') {
        // Solo ignorar si el usuario está tipeando dentro de un input visible de la app Lowcord
        const activeEl = document.activeElement;
        if (activeEl && ['INPUT', 'SELECT', 'TEXTAREA'].includes(activeEl.tagName) && activeEl.offsetParent !== null) {
          return;
        }
        if (inputMode === 'vad') {
          toggleMic();
        } else if (inputMode === 'ptt') {
          setPttState(true);
        }
      } else if (msg.type === 'GLOBAL_KEYUP') {
        if (inputMode === 'ptt') {
          setPttState(false);
        }
      }
    } catch (err) {}
  });
}

function syncHotkeyWithNativeClient(code) {
  if (window.chrome && window.chrome.webview) {
    try {
      window.chrome.webview.postMessage({
        type: 'UPDATE_HOTKEY',
        code: code
      });
    } catch (e) {}
  }
}

// Configuración avanzada de la UI
let advancedAudioSettingsInitialized = false;
function initAdvancedAudioSettingsUI() {
  if (advancedAudioSettingsInitialized || !btnModeVad) return;
  advancedAudioSettingsInitialized = true;

  setInputMode(inputMode);

  sliderSensitivity.value = vadThreshold;
  sensitivityValDisplay.innerText = `${vadThreshold}%`;
  sensitivityCutoffLine.style.left = `${vadThreshold}%`;

  keybindDisplayText.innerText = activeKeybind.label;
  chkNoiseSuppression.checked = noiseSuppressionEnabled;

  btnModeVad.addEventListener('click', () => setInputMode('vad'));
  btnModePtt.addEventListener('click', () => setInputMode('ptt'));

  sliderSensitivity.addEventListener('input', (e) => {
    vadThreshold = parseInt(e.target.value, 10);
    sensitivityValDisplay.innerText = `${vadThreshold}%`;
    sensitivityCutoffLine.style.left = `${vadThreshold}%`;
    localStorage.setItem('lowcord_vad_threshold', vadThreshold);
  });

  btnRecordKeybind.addEventListener('click', () => {
    if (isRecordingKeybind) {
      finishKeybindRecording(null);
    } else {
      isRecordingKeybind = true;
      btnRecordKeybind.classList.add('recording');
      keybindDisplayText.innerText = 'Presiona tecla...';
    }
  });

  chkNoiseSuppression.addEventListener('change', async (e) => {
    noiseSuppressionEnabled = e.target.checked;
    localStorage.setItem('lowcord_noise_suppression', noiseSuppressionEnabled ? 'true' : 'false');
    if (rawMicStream) {
      await reloadLocalAudio();
    }
  });

  // Notificar al cliente nativo el atajo actual
  syncHotkeyWithNativeClient(activeKeybind.code);
}

function setInputMode(mode) {
  inputMode = mode;
  localStorage.setItem('lowcord_input_mode', mode);

  btnModeVad.classList.toggle('active', mode === 'vad');
  btnModePtt.classList.toggle('active', mode === 'ptt');
  vadSettingsBox.style.display = (mode === 'vad') ? 'block' : 'none';

  if (mode === 'vad') {
    keybindTitleLabel.innerText = 'Atajo de Teclado para Silenciar';
    keybindStatusHint.innerText = 'Presiona para mutear o desmutear (funciona en juegos).';
    isPttActive = false;
    isGateOpen = true;
    applyMicTransmissionState();
  } else {
    keybindTitleLabel.innerText = 'Tecla para Pulsar para Hablar';
    keybindStatusHint.innerText = 'Mantén presionada esta tecla para hablar.';
    isPttActive = false;
    applyMicTransmissionState();
  }
}

function finishKeybindRecording(newKeybind) {
  isRecordingKeybind = false;
  btnRecordKeybind.classList.remove('recording');

  if (newKeybind) {
    activeKeybind = newKeybind;
    localStorage.setItem('lowcord_keybind_code', newKeybind.code);
    localStorage.setItem('lowcord_keybind_label', newKeybind.label);
    syncHotkeyWithNativeClient(newKeybind.code);
  }

  keybindDisplayText.innerText = activeKeybind.label;
}

async function reloadLocalAudio() {
  if (!currentInputDeviceId && !rawMicStream) return;
  try {
    const audioConstraints = {
      echoCancellation: { ideal: noiseSuppressionEnabled },
      noiseSuppression: { ideal: noiseSuppressionEnabled },
      autoGainControl: { ideal: true },
      googEchoCancellation: noiseSuppressionEnabled,
      googAutoGainControl: true,
      googNoiseSuppression: noiseSuppressionEnabled,
      googHighpassFilter: true,
      googTypingNoiseDetection: noiseSuppressionEnabled
    };
    if (currentInputDeviceId) {
      audioConstraints.deviceId = { exact: currentInputDeviceId };
    }
    const newStream = await navigator.mediaDevices.getUserMedia({
      audio: audioConstraints,
      video: false
    });
    if (rawMicStream) {
      rawMicStream.getTracks().forEach(t => t.stop());
    }
    rawMicStream = newStream;
    setupLocalAudioProcessing();

    // Reemplazar la pista en todas las conexiones peer activas
    const newTrack = newStream.getAudioTracks()[0];
    if (newTrack) {
      peers.forEach(peer => {
        const senders = peer.pc.getSenders();
        const audioSender = senders.find(s => s.track && s.track.kind === 'audio' && !peer.screenSenders.includes(s));
        if (audioSender) {
          audioSender.replaceTrack(newTrack).catch(err => {});
        }
      });
    }
  } catch(e) {
    console.warn('Error al recargar audio:', e);
  }
}

// =========================================================
// 8. PARTICIPANTES Y VOLUMEN INDIVIDUAL POR USUARIO
// =========================================================
function addParticipantCard(peerId, name, isMuted) {
  if (document.getElementById(`participant-${peerId}`)) return;

  // Limpiar cualquier tarjeta residual previa con el mismo nombre si proviene de una reconexión
  const existingCards = participantsGrid.querySelectorAll('.participant-card:not(.local-card)');
  existingCards.forEach(c => {
    const pName = c.querySelector('.participant-name');
    if (pName && pName.textContent.trim().toLowerCase() === (name || '').trim().toLowerCase()) {
      c.remove();
    }
  });

  const card = document.createElement('div');
  card.id = `participant-${peerId}`;
  card.className = 'participant-card';
  const initials = name ? name.slice(0, 2).toUpperCase() : 'US';

  card.innerHTML = `
    <div class="avatar-wrapper">
      <div class="avatar-initials">${initials}</div>
      <div class="speaking-ring"></div>
    </div>
    <div class="participant-meta">
      <span class="participant-name">${name}</span>
      <span class="mic-status-icon ${isMuted ? 'muted' : ''}">${isMuted ? ICONS.micOff : ICONS.micOn}</span>
    </div>
    <div class="participant-volume-box" title="Volumen de ${name}">
      <span class="vol-icon">${ICONS.vol}</span>
      <input type="range" class="participant-vol-slider" min="0" max="100" value="100">
      <span class="participant-vol-pct">100%</span>
    </div>`;

  const volSlider = card.querySelector('.participant-vol-slider');
  const volPct = card.querySelector('.participant-vol-pct');
  const volIcon = card.querySelector('.vol-icon');

  volSlider.addEventListener('input', (e) => {
    const val = parseInt(e.target.value, 10);
    volPct.innerText = `${val}%`;
    volIcon.innerHTML = val === 0 ? ICONS.volMute : ICONS.vol;

    const peer = peers.get(peerId);
    if (peer) {
      peer.userVolume = val / 100;
      if (peer.audioEl) {
        peer.audioEl.volume = Math.min(1.0, peer.userVolume * (currentOutputVolume / 100));
      }
    }
  });

  participantsGrid.appendChild(card);
}

function removeParticipantCard(peerId) {
  const card = document.getElementById(`participant-${peerId}`);
  if (card && card.parentNode) card.parentNode.removeChild(card);
}

// =========================================================
// 9. PANTALLAS COMPARTIDAS CON AUDIO Y VOLUMEN INDIVIDUAL
// =========================================================
function addScreenCard(id, title, stream) {
  let card = document.getElementById(`screen-${id}`);

  if (!card) {
    card = document.createElement('div');
    card.id = `screen-${id}`;
    card.className = 'screen-card';
    card.dataset.screenId = id;
    card.dataset.screenTitle = title;

    const isLocal = (id === 'local');
    const initialVol = isLocal ? 0 : 100;
    screenVolumes.set(id, initialVol / 100);

    card.innerHTML = `
      <div class="screen-card-header">
        ${ICONS.screen}
        <span>${title}</span>
        ${!isLocal ? `<span class="screen-audio-badge">Audio en espera</span>` : ''}
      </div>
      <video autoplay playsinline muted></video>
      <div class="screen-card-actions">
        ${isLocal ? `
        <button class="screen-action-btn btn-gpu-saver" id="btn-gpu-saver" title="Ocultar la vista previa local para ahorrar GPU y eliminar el efecto espejo">
          Ahorrar GPU (Ocultar)
        </button>` : ''}
        ${!isLocal ? `
        <div class="screen-volume-box" title="Volumen del sonido de la pantalla">
          <span class="screen-vol-icon">${ICONS.vol}</span>
          <input type="range" class="screen-vol-slider" min="0" max="100" value="100">
          <span class="screen-vol-pct">100%</span>
        </div>` : ''}
        <button class="screen-action-btn btn-hide" title="Ocultar esta pantalla">${ICONS.hide} Ocultar</button>
        <button class="screen-action-btn btn-pin" title="Fijar pantalla completa">${ICONS.pin} Fijar</button>
        <button class="screen-action-btn btn-fs" title="Pantalla completa">${ICONS.fullscreen}</button>
      </div>`;

    const video = card.querySelector('video');
    video.srcObject = stream;

    if (isLocal) {
      const btnGpu = card.querySelector('#btn-gpu-saver');
      if (btnGpu) {
        btnGpu.addEventListener('click', (e) => {
          e.stopPropagation();
          hideScreen('local', title);
        });
      }
    }

    if (!isLocal) {
      const volSlider = card.querySelector('.screen-vol-slider');
      const volPct = card.querySelector('.screen-vol-pct');
      const volIcon = card.querySelector('.screen-vol-icon');

      if (volSlider) {
        volSlider.addEventListener('input', (e) => {
          const val = parseInt(e.target.value, 10);
          volPct.innerText = `${val}%`;
          volIcon.innerHTML = val === 0 ? ICONS.volMute : ICONS.vol;
          const gain = val / 100;
          screenVolumes.set(id, gain);

          const peer = peers.get(id);
          if (peer && peer.screenGainNode) {
            const ctx = getAudioContext();
            peer.screenGainNode.gain.setValueAtTime(gain, ctx.currentTime);
          }

          const screenAudioEl = document.getElementById(`audio-screen-${id}`);
          if (screenAudioEl) {
            screenAudioEl.volume = gain;
          }
        });
      }
    }

    card.querySelector('.btn-hide').addEventListener('click', () => {
      hideScreen(id, title);
    });

    card.querySelector('.btn-pin').addEventListener('click', () => {
      const wasFocused = card.classList.contains('focused');
      document.querySelectorAll('.screen-card').forEach(c => c.classList.remove('focused'));
      if (!wasFocused) card.classList.add('focused');
    });

    card.querySelector('.btn-fs').addEventListener('click', () => {
      if (!document.fullscreenElement) {
        card.requestFullscreen().catch(e => {});
      } else {
        document.exitFullscreen();
      }
    });

    screenShareGrid.appendChild(card);
  } else {
    const video = card.querySelector('video');
    if (video.srcObject !== stream) {
      video.srcObject = stream;
    }
  }

  updateScreenLayout();
}

function hideScreen(id, title) {
  const card = document.getElementById(`screen-${id}`);
  if (!card) return;

  card.style.display = 'none';
  hiddenScreens.add(id);

  if (id === 'local') {
    renderLocalScreenPill();
  } else {
    let pill = document.getElementById(`pill-${id}`);
    if (!pill) {
      pill = document.createElement('div');
      pill.id = `pill-${id}`;
      pill.className = 'hidden-screen-pill';
      pill.innerHTML = `${ICONS.screen} <span>${title} (Oculta)</span> <strong>Mostrar</strong>`;
      pill.addEventListener('click', () => {
        restoreScreen(id);
      });
      hiddenScreensBar.appendChild(pill);
    }
  }

  updateScreenLayout();
}

function restoreScreen(id) {
  const card = document.getElementById(`screen-${id}`);
  if (card) {
    card.style.display = 'flex';
  }
  hiddenScreens.delete(id);

  const pill = document.getElementById(`pill-${id}`);
  if (pill && pill.parentNode) {
    pill.parentNode.removeChild(pill);
  }

  updateScreenLayout();
}

function removeScreenCard(id) {
  const card = document.getElementById(`screen-${id}`);
  if (card && card.parentNode) card.parentNode.removeChild(card);

  const pill = document.getElementById(`pill-${id}`);
  if (pill && pill.parentNode) pill.parentNode.removeChild(pill);

  const screenAudio = document.getElementById(`audio-screen-${id}`);
  if (screenAudio && screenAudio.parentNode) screenAudio.parentNode.removeChild(screenAudio);

  const peer = peers.get(id);
  if (peer && peer.screenGainNode) {
    try { peer.screenGainNode.disconnect(); } catch(e){}
    peer.screenGainNode = null;
  }

  hiddenScreens.delete(id);
  screenVolumes.delete(id);

  updateScreenLayout();
}

function updateScreenLayout() {
  const allCards = Array.from(screenShareGrid.querySelectorAll('.screen-card'));
  const visibleCards = allCards.filter(c => c.style.display !== 'none');
  const count = visibleCards.length;

  if (allCards.length > 0) {
    screenShareGrid.style.display = count > 0 ? 'grid' : 'none';
    stageContainer.classList.add('has-screens');
    btnToggleAvatars.style.display = 'inline-block';
  } else {
    screenShareGrid.style.display = 'none';
    stageContainer.classList.remove('has-screens');
    btnToggleAvatars.style.display = 'none';
  }

  hiddenScreensBar.style.display = hiddenScreensBar.children.length > 0 ? 'flex' : 'none';
  screenShareGrid.dataset.screens = Math.min(4, Math.max(1, count));
}

function updateUserCount() {
  userCount.innerText = 1 + peers.size;
}

// Inicializar eventos de miniatura PiP y sonidos
function initMiniPreviewAndSoundsUI() {
  if (chkMuteSound) {
    chkMuteSound.checked = muteSoundsEnabled;
    chkMuteSound.addEventListener('change', (e) => {
      muteSoundsEnabled = e.target.checked;
      localStorage.setItem('lowcord_mute_sounds', muteSoundsEnabled);
    });
  }

  if (btnCloseMiniPreview) {
    btnCloseMiniPreview.addEventListener('click', () => {
      toggleMiniPreview(false);
    });
  }
}

// Inicializar configuración avanzada de audio, calidad de pantalla y componentes
function initPageComponents() {
  initAdvancedAudioSettingsUI();
  initScreenQualityUI();
  initMiniPreviewAndSoundsUI();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initPageComponents);
} else {
  initPageComponents();
}



