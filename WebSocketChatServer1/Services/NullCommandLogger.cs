using ChatSystem.Interfaces;

using Microsoft.Extensions.Logging;

namespace WebSocketChatServer1.Services;

public class NullCommandLogger : ICommandLogger
{
    private readonly ILogger<NullCommandLogger> _logger;

    public NullCommandLogger(ILogger<NullCommandLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        _logger.LogDebug($"Command logged (no database): {commandType} by {username} - Success: {success}");
        await Task.CompletedTask;
    }
}