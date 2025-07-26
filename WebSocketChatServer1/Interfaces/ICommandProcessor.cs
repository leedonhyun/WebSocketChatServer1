using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface ICommandProcessor
{
    Task<bool> CanProcessAsync(string command);
    Task ProcessAsync(string clientId, string command, string[] args);
}