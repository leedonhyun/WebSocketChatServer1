using System.Threading.Tasks;

namespace WebSocketChatServer1.Interfaces;

public interface ICommandProcessor
{
    Task<bool> CanProcessAsync(string command);
    Task ProcessAsync(string clientId, string command, string[] args);
}