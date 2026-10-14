using System.Collections.Generic;
using System.Threading.Tasks;
using Lowcord.Client.Models;

namespace Lowcord.Client.Services.Contracts;

public interface IRoomRegistryService
{
    Task<bool> RegisterRoomAsync(string roomName, string tunnelUrl, bool hasPassword, string hostUser);
    Task<bool> UnregisterRoomAsync(string roomName);
    Task<string?> ResolveRoomToUrlAsync(string input);
    Task<List<RoomInfo>> GetActiveRoomsAsync();
}
