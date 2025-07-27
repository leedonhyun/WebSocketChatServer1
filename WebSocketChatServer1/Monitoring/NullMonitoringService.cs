using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Monitoring;

public class NullMonitoringService : IMonitoringService
{
    private readonly ILogger<NullMonitoringService> _logger;

    public NullMonitoringService(ILogger<NullMonitoringService> logger)
    {
        _logger = logger;
    }

    public async Task<List<CommandStatsDto>> GetCommandStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        _logger.LogWarning("MongoDB unavailable - returning empty command stats");
        return await Task.FromResult(new List<CommandStatsDto>());
    }

    public async Task<List<UserActivityDto>> GetUserActivityAsync(int limit = 20)
    {
        _logger.LogWarning("MongoDB unavailable - returning empty user activity");
        return await Task.FromResult(new List<UserActivityDto>());
    }

    public async Task<SystemStatusDto> GetSystemStatusAsync()
    {
        _logger.LogWarning("MongoDB unavailable - returning default system status");
        return await Task.FromResult(new SystemStatusDto
        {
            Timestamp = DateTime.UtcNow,
            CurrentActiveConnections = 0,
            CurrentActiveRooms = 0,
            TotalCommandsToday = 0,
            TotalErrorsToday = 0,
            ErrorRate = 0,
            TopCommands = new List<CommandStatsDto>(),
            RecentErrors = new List<string> { "MongoDB connection unavailable" }
        });
    }

    public async Task<List<CommandLog>> GetRecentCommandsAsync(int limit = 50)
    {
        _logger.LogWarning("MongoDB unavailable - returning empty recent commands");
        return await Task.FromResult(new List<CommandLog>());
    }

    public async Task<List<CommandLog>> GetCommandsByUserAsync(string username, int limit = 50)
    {
        _logger.LogWarning("MongoDB unavailable - returning empty user commands");
        return await Task.FromResult(new List<CommandLog>());
    }

    public async Task<List<CommandLog>> GetErrorCommandsAsync(DateTime? fromDate = null, int limit = 50)
    {
        _logger.LogWarning("MongoDB unavailable - returning empty error commands");
        return await Task.FromResult(new List<CommandLog>());
    }
}