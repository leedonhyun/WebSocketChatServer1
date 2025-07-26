using System.Threading.Tasks;

namespace ChatSystem.Monitoring;

// 인터페이스
public interface ICommandLogger
{
    Task LogCommandAsync(string clientId, string? username, string commandType,
        object? parameters, bool success, double executionTimeMs,
        string? errorMessage = null, object? responseData = null);

    Task LogSystemMetricsAsync(int activeConnections, int activeGroups,
        long totalMessages, long totalFiles, long totalErrors);
}