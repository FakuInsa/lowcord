using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Lowcord.Server.Hubs;

public class UserSession
{
    public required string ConnectionId { get; set; }
    public required string UserName { get; set; }
    public required string RoomId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsScreenSharing { get; set; }
}

public class RoomState
{
    public required string RoomId { get; set; }
    public required string Password { get; set; }
    public ConcurrentDictionary<string, UserSession> Users { get; } = new();
}

public class SignalingHub : Hub
{
    // Almacén en memoria concurrente para las salas y usuarios conectados
    private static readonly ConcurrentDictionary<string, RoomState> Rooms = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionToRoom = new();

    /// <summary>
    /// Intenta conectar al usuario a una sala validando la contraseña de acceso.
    /// </summary>
    public async Task JoinRoom(string roomId, string password, string userName)
    {
        roomId = string.IsNullOrWhiteSpace(roomId) ? "general" : roomId.Trim().ToLowerInvariant();
        userName = string.IsNullOrWhiteSpace(userName) ? $"Usuario-{Context.ConnectionId[..4]}" : userName.Trim();
        password = password?.Trim() ?? "";

        // Si la sala no existe, la crea el primer usuario que llega y fija la contraseña
        var room = Rooms.GetOrAdd(roomId, id => new RoomState
        {
            RoomId = id,
            Password = password
        });

        // Validar contraseña de sala
        if (!string.IsNullOrEmpty(room.Password) && room.Password != password)
        {
            await Clients.Caller.SendAsync("JoinFailed", "Contraseña incorrecta para esta sala.");
            return;
        }

        var session = new UserSession
        {
            ConnectionId = Context.ConnectionId,
            UserName = userName,
            RoomId = roomId
        };

        room.Users[Context.ConnectionId] = session;
        ConnectionToRoom[Context.ConnectionId] = roomId;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Enviar al nuevo usuario la lista de compañeros existentes en la sala
        var existingUsers = room.Users.Values
            .Where(u => u.ConnectionId != Context.ConnectionId)
            .Select(u => new
            {
                u.ConnectionId,
                u.UserName,
                u.IsMuted,
                u.IsScreenSharing
            })
            .ToList();

        await Clients.Caller.SendAsync("JoinedSuccessfully", Context.ConnectionId, existingUsers);

        // Notificar a los demás que un nuevo usuario se unió para que preparen el enlace P2P
        await Clients.OthersInGroup(roomId).SendAsync("UserJoined", Context.ConnectionId, userName);
    }

    /// <summary>
    /// Reenvía la oferta SDP generada por WebRTC al compañero destino
    /// </summary>
    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, sdp);
    }

    /// <summary>
    /// Reenvía la respuesta SDP de vuelta al compañero iniciador
    /// </summary>
    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, sdp);
    }

    /// <summary>
    /// Ping ligero para mantener activo el túnel Cloudflare y la conexión SignalR en períodos de silencio
    /// </summary>
    public Task Ping()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Intercambia los candidatos de red ICE para establecer el túnel P2P directo
    /// </summary>
    public async Task SendIceCandidate(string targetConnectionId, string candidate)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);
    }

    /// <summary>
    /// Notifica a la sala cambios en el estado del micrófono o pantalla compartida
    /// </summary>
    public async Task UpdateMediaState(bool isMuted, bool isScreenSharing)
    {
        if (ConnectionToRoom.TryGetValue(Context.ConnectionId, out var roomId) &&
            Rooms.TryGetValue(roomId, out var room) &&
            room.Users.TryGetValue(Context.ConnectionId, out var user))
        {
            user.IsMuted = isMuted;
            user.IsScreenSharing = isScreenSharing;

            await Clients.OthersInGroup(roomId).SendAsync("UserMediaStateChanged", Context.ConnectionId, isMuted, isScreenSharing);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToRoom.TryRemove(Context.ConnectionId, out var roomId) &&
            Rooms.TryGetValue(roomId, out var room))
        {
            room.Users.TryRemove(Context.ConnectionId, out _);

            // Si la sala queda vacía, la eliminamos de memoria
            if (room.Users.IsEmpty)
            {
                Rooms.TryRemove(roomId, out _);
            }
            else
            {
                await Clients.Group(roomId).SendAsync("UserLeft", Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
