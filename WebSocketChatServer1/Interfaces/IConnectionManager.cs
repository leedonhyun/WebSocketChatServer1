using System.Collections.Concurrent;
using WebSocketChatServer1.Models;

namespace WebSocketChatServer1.Interfaces
{
    public interface IConnectionManager
    {
        ConcurrentDictionary<string, UserInfo> GetAllUsers();
        bool AddUser(string connectionId, UserInfo userInfo);
        UserInfo GetUser(string connectionId);
        string GetConnectionIdByUsername(string username);
        bool RemoveUser(string connectionId);
        void SetUsername(string connectionId, string username);
    }
}