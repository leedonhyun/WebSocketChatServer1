using WebSocketChatShared.Models;

using System.Threading.Tasks;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Interfaces;

public interface IMessageBroadcaster
{
    void RegisterConnection(string clientId, IClientConnection connection);
    void UnregisterConnection(string clientId);
    Task BroadcastAsync<T>(T message, string? excludeClientId = null, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToClientAsync<T>(string clientId, T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToClientAsync<T>(IEnumerable<string>? clientIds, T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToUsernameAsync<T>(string username, T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    Task SendToRoomAsync<T>(string roomId, T message, string? excludeUsername = null, CancellationToken cancellationToken = default) where T : BaseMessage;
}