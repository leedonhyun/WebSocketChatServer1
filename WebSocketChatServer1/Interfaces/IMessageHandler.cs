using WebSocketChatShared.Models;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Interfaces;
public interface IMessageHandler<T> where T : BaseMessage
{
    Task HandleAsync(string clientId, T message, CancellationToken cancellationToken = default);
}