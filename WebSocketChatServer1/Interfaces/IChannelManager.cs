using System.Threading;
using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface IChannelManager
{
    Task<IChannel> CreateChannelAsync(string name, CancellationToken cancellationToken);
    Task<IChannel> AcceptChannelAsync(string name, CancellationToken cancellationToken);
}