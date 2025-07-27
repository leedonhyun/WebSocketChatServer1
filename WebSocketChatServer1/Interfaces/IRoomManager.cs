using WebSocketChatShared.Models;

namespace WebSocketChatServer1.Interfaces;

public interface IRoomManager
{
    Task<string> CreateRoomAsync(string roomName, string createdBy);
    Task<bool> AddMemberAsync(string roomId, string username);
    Task<bool> RemoveMemberAsync(string roomId, string username);
    Task<Room?> GetRoomAsync(string roomId);
    Task<IEnumerable<Room>> GetRoomsByUserAsync(string username);
    Task<IEnumerable<Room>> GetAllRoomsAsync();
    Task<bool> IsRoomMemberAsync(string roomId, string username);
    Task<bool> DeleteRoomAsync(string roomId);
    Task<IEnumerable<string>> GetRoomMembersAsync(string roomId);

    Task<bool> AddClientToRoomAsync(string roomId, string clientId);
    Task<bool> RemoveClientFromRoomAsync(string roomId, string clientId);
    Task<IEnumerable<string>> GetClientIdsInRoomAsync(string roomId);
    Task<Room?> GetRoomForClientAsync(string clientId);
    Task<bool> IsClientInRoomAsync(string roomId, string clientId);

}
