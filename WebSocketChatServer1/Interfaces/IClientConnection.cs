using WebSocketChatServer1.Models;

namespace WebSocketChatServer1.Interfaces;

public interface IClientConnection
{
    Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage;
    bool IsConnected { get; }
}