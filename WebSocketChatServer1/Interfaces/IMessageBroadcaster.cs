using WebSocketChatServer1.Models;

using System.Threading.Tasks;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Interfaces;

public interface IMessageBroadcaster
{
    Task BroadcastAsync<T>(T message, string? excludeClientId = null) where T : BaseMessage;
    Task SendToClientAsync<T>(string clientId, T message) where T : BaseMessage;
    Task SendToUsernameAsync<T>(string username, T message) where T : BaseMessage;
    void RegisterConnection(string clientId, IClientConnection connection);
    void UnregisterConnection(string clientId);

}