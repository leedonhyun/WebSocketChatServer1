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
}
