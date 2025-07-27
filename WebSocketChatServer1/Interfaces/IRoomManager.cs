using System.Collections.Concurrent;
using WebSocketChatServer1.Models;

namespace WebSocketChatServer1.Interfaces
{
    public interface IRoomManager
    {
        ConcurrentDictionary<string, Room> GetAllRooms();
        string CreateRoom(string roomName, string ownerId);
        bool JoinRoom(string roomId, string userId);
        bool LeaveRoom(string roomId, string userId);
        Room GetRoom(string roomId);
        IEnumerable<string> GetUsersInRoom(string roomId);
    }
}