using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface ICommandLogger
{
    Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null);
}