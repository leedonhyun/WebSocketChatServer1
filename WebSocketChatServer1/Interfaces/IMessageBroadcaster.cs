using WebSocketChatServer1.Models;

using System.Threading.Tasks;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Interfaces;

public interface IMessageBroadcaster
{
    Task BroadcastAsync<T>(T message, string? excludeClientId = null, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToClientAsync<T>(string clientId, T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToUsernameAsync<T>(string username, T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToGroupAsync<T>(string groupId, T message, string? excludeUsername = null, CancellationToken cancellationToken = default) where T : BaseMessage;
    void RegisterConnection(string clientId, IClientConnection connection);
    void UnregisterConnection(string clientId);
}