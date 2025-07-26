using System.Threading.Tasks;

namespace WebSocketChatServer1.Interfaces;

public interface ICommandLogger
{
    Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null);
    Task LogCommandAsync(string clientId, string? username, string commandType,
    object? parameters, bool success, double executionTimeMs,
    string? errorMessage = null, object? responseData = null);

    Task LogSystemMetricsAsync(int activeConnections, int activeGroups,
        long totalMessages, long totalFiles, long totalErrors);
}