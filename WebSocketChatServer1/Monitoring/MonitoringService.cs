using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Monitoring;

public class MonitoringService : IMonitoringService
{
    private readonly IMongoCollection<CommandLog> _commandLogs;
    private readonly IMongoCollection<SystemMetrics> _systemMetrics;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(IMongoDatabase database, ILogger<MonitoringService> logger)
    {
        _commandLogs = database.GetCollection<CommandLog>("command_logs");
        _systemMetrics = database.GetCollection<SystemMetrics>("system_metrics");
        _logger = logger;
    }

    public async Task<List<CommandStatsDto>> GetCommandStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-7);
        var to = toDate ?? DateTime.UtcNow;

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                ["timestamp"] = new BsonDocument
                {
                    ["$gte"] = from,
                    ["$lte"] = to
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$commandType",
                ["count"] = new BsonDocument("$sum", 1),
                ["avgExecutionTime"] = new BsonDocument("$avg", "$executionTimeMs"),
                ["successCount"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$success", 1, 0 })),
                ["lastExecuted"] = new BsonDocument("$max", "$timestamp")
            }),
            new BsonDocument("$project", new BsonDocument
            {
                ["commandType"] = "$_id",
                ["count"] = 1,
                ["avgExecutionTimeMs"] = "$avgExecutionTime",
                ["successRate"] = new BsonDocument("$divide", new BsonArray { "$successCount", "$count" }),
                ["lastExecuted"] = 1,
                ["_id"] = 0
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        var results = await _commandLogs.Aggregate<BsonDocument>(pipeline).ToListAsync();

        return results.Select(doc => new CommandStatsDto
        {
            CommandType = doc["commandType"].AsString,
            Count = doc["count"].AsInt64,
            AvgExecutionTimeMs = doc["avgExecutionTimeMs"].AsDouble,
            SuccessRate = doc["successRate"].AsDouble,
            LastExecuted = doc["lastExecuted"].ToUniversalTime()
        }).ToList();
    }

    public async Task<List<UserActivityDto>> GetUserActivityAsync(int limit = 20)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("username", new BsonDocument("$ne", BsonNull.Value))),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$username",
                ["commandCount"] = new BsonDocument("$sum", 1),
                ["firstSeen"] = new BsonDocument("$min", "$timestamp"),
                ["lastSeen"] = new BsonDocument("$max", "$timestamp"),
                ["commands"] = new BsonDocument("$push", "$commandType")
            }),
            new BsonDocument("$project", new BsonDocument
            {
                ["username"] = "$_id",
                ["commandCount"] = 1,
                ["firstSeen"] = 1,
                ["lastSeen"] = 1,
                ["topCommands"] = new BsonDocument("$slice", new BsonArray
                {
                    new BsonDocument("$setUnion", "$commands"), 5
                }),
                ["_id"] = 0
            }),
            new BsonDocument("$sort", new BsonDocument("commandCount", -1)),
            new BsonDocument("$limit", limit)
        };

        var results = await _commandLogs.Aggregate<BsonDocument>(pipeline).ToListAsync();

        return results.Select(doc => new UserActivityDto
        {
            Username = doc["username"].AsString,
            CommandCount = doc["commandCount"].AsInt64,
            FirstSeen = doc["firstSeen"].ToUniversalTime(),
            LastSeen = doc["lastSeen"].ToUniversalTime(),
            TopCommands = doc["topCommands"].AsBsonArray.Select(x => x.AsString).ToList()
        }).ToList();
    }

    public async Task<SystemStatusDto> GetSystemStatusAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // 최신 시스템 메트릭
        var latestMetrics = await _systemMetrics
            .Find(Builders<SystemMetrics>.Filter.Empty)
            .SortByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync();

        // 오늘의 명령어 통계
        var todayCommands = await _commandLogs
            .CountDocumentsAsync(x => x.Timestamp >= today);

        var todayErrors = await _commandLogs
            .CountDocumentsAsync(x => x.Timestamp >= today && !x.Success);

        // 최근 에러 메시지
        var recentErrors = await _commandLogs
            .Find(x => !x.Success && x.ErrorMessage != null)
            .SortByDescending(x => x.Timestamp)
            .Limit(10)
            .Project(x => x.ErrorMessage!)
            .ToListAsync();

        // 상위 명령어
        var topCommands = await GetCommandStatsAsync(today, now);

        return new SystemStatusDto
        {
            Timestamp = now,
            CurrentActiveConnections = latestMetrics?.ActiveConnections ?? 0,
            CurrentActiveGroups = latestMetrics?.ActiveGroups ?? 0,
            TotalCommandsToday = todayCommands,
            TotalErrorsToday = todayErrors,
            ErrorRate = todayCommands > 0 ? (double)todayErrors / todayCommands : 0,
            TopCommands = topCommands.Take(5).ToList(),
            RecentErrors = recentErrors
        };
    }

    public async Task<List<CommandLog>> GetRecentCommandsAsync(int limit = 50)
    {
        return await _commandLogs
            .Find(Builders<CommandLog>.Filter.Empty)
            .SortByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<CommandLog>> GetCommandsByUserAsync(string username, int limit = 50)
    {
        return await _commandLogs
            .Find(x => x.Username == username)
            .SortByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<CommandLog>> GetErrorCommandsAsync(DateTime? fromDate = null, int limit = 50)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-1);

        return await _commandLogs
            .Find(x => !x.Success && x.Timestamp >= from)
            .SortByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }
}