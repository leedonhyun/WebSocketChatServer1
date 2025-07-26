using System.Threading;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Interfaces;

public interface IChannelManager
{
    Task<IChannel> CreateChannelAsync(string name, CancellationToken cancellationToken);
    Task<IChannel> AcceptChannelAsync(string name, CancellationToken cancellationToken);
}