using System.Threading.Tasks;

using WebSocketChatShared.Models;

namespace WebSocketChatServer1.Interfaces;

public interface ICommandProcessor
{
    Task<bool> CanProcessAsync(string command);
    Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default);
    Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default);
}