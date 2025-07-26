using WebSocketChatServer1.Interfaces;

using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Monitoring;

// MongoDB�� ����� �� ���� ����� ���� ����
public class NullCommandLogger : ICommandLogger
{
    private readonly ILogger<NullCommandLogger> _logger;

    public NullCommandLogger(ILogger<NullCommandLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string? username, string commandType,
        object? parameters, bool success, double executionTimeMs,
        string? errorMessage = null, object? responseData = null)
    {
        // MongoDB�� ��� �Ұ����ϹǷ� �α� ��ŵ
        _logger.LogDebug($"Command logging skipped - MongoDB unavailable. Command: {commandType}, User: {username}, Success: {success}");
        await Task.CompletedTask;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType,
        string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        _logger.LogDebug($"Command logging skipped - MongoDB unavailable. Command: {commandType}, User: {username}, Success: {success}");
        await Task.CompletedTask;
    }

    public async Task LogSystemMetricsAsync(int activeConnections, int activeGroups,
        long totalMessages, long totalFiles, long totalErrors)
    {
        _logger.LogDebug($"System metrics logging skipped - MongoDB unavailable. Connections: {activeConnections}, Groups: {activeGroups}");
        await Task.CompletedTask;
    }
}