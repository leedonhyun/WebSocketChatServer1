using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatSystem.Monitoring;

public interface IMonitoringService
{
    Task<List<CommandStatsDto>> GetCommandStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<UserActivityDto>> GetUserActivityAsync(int limit = 20);
    Task<SystemStatusDto> GetSystemStatusAsync();
    Task<List<CommandLog>> GetRecentCommandsAsync(int limit = 50);
    Task<List<CommandLog>> GetCommandsByUserAsync(string username, int limit = 50);
    Task<List<CommandLog>> GetErrorCommandsAsync(DateTime? fromDate = null, int limit = 50);
}