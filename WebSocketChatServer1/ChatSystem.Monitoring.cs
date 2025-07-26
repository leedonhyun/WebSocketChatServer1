using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Text.Json;

namespace ChatSystem.Monitoring;

// MongoDB 모델들
[BsonIgnoreExtraElements]
public class CommandLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [BsonElement("username")]
    public string? Username { get; set; }

    [BsonElement("commandType")]
    public string CommandType { get; set; } = string.Empty;

    [BsonElement("parameters")]
    public BsonDocument Parameters { get; set; } = new();

    [BsonElement("success")]
    public bool Success { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [BsonElement("responseData")]
    public BsonDocument? ResponseData { get; set; }

    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }

    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
}

[BsonIgnoreExtraElements]
public class SystemMetrics
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("activeConnections")]
    public int ActiveConnections { get; set; }

    [BsonElement("activeGroups")]
    public int ActiveGroups { get; set; }

    [BsonElement("totalMessages")]
    public long TotalMessages { get; set; }

    [BsonElement("totalFiles")]
    public long TotalFiles { get; set; }

    [BsonElement("totalErrors")]
    public long TotalErrors { get; set; }

    [BsonElement("serverInstance")]
    public string ServerInstance { get; set; } = Environment.MachineName;
}

// 모니터링 API용 DTO들
public class CommandStatsDto
{
    public string CommandType { get; set; } = string.Empty;
    public long Count { get; set; }
    public double AvgExecutionTimeMs { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastExecuted { get; set; }
}

public class UserActivityDto
{
    public string Username { get; set; } = string.Empty;
    public long CommandCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> TopCommands { get; set; } = new();
}

public class SystemStatusDto
{
    public DateTime Timestamp { get; set; }
    public int CurrentActiveConnections { get; set; }
    public int CurrentActiveGroups { get; set; }
    public long TotalCommandsToday { get; set; }
    public long TotalErrorsToday { get; set; }
    public double ErrorRate { get; set; }
    public List<CommandStatsDto> TopCommands { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
}

// 인터페이스
public interface ICommandLogger
{
    Task LogCommandAsync(string clientId, string? username, string commandType,
        object? parameters, bool success, double executionTimeMs,
        string? errorMessage = null, object? responseData = null);

    Task LogSystemMetricsAsync(int activeConnections, int activeGroups,
        long totalMessages, long totalFiles, long totalErrors);
}

public interface IMonitoringService
{
    Task<List<CommandStatsDto>> GetCommandStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<UserActivityDto>> GetUserActivityAsync(int limit = 20);
    Task<SystemStatusDto> GetSystemStatusAsync();
    Task<List<CommandLog>> GetRecentCommandsAsync(int limit = 50);
    Task<List<CommandLog>> GetCommandsByUserAsync(string username, int limit = 50);
    Task<List<CommandLog>> GetErrorCommandsAsync(DateTime? fromDate = null, int limit = 50);
}

// MongoDB 서비스 구현
public class MongoCommandLogger : ICommandLogger
{
    private readonly IMongoCollection<CommandLog> _commandLogs;
    private readonly IMongoCollection<SystemMetrics> _systemMetrics;
    private readonly ILogger<MongoCommandLogger> _logger;

    public MongoCommandLogger(IMongoDatabase database, ILogger<MongoCommandLogger> logger)
    {
        _commandLogs = database.GetCollection<CommandLog>("command_logs");
        _systemMetrics = database.GetCollection<SystemMetrics>("system_metrics");
        _logger = logger;

        // 인덱스 생성
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            var commandIndexKeys = Builders<CommandLog>.IndexKeys
                .Ascending(x => x.Timestamp)
                .Ascending(x => x.CommandType)
                .Ascending(x => x.Username);

            var commandIndexOptions = new CreateIndexOptions
            {
                Name = "timestamp_commandtype_username",
                Background = true
            };

            _commandLogs.Indexes.CreateOne(new CreateIndexModel<CommandLog>(commandIndexKeys, commandIndexOptions));

            var metricsIndexKeys = Builders<SystemMetrics>.IndexKeys
                .Ascending(x => x.Timestamp)
                .Ascending(x => x.ServerInstance);

            var metricsIndexOptions = new CreateIndexOptions
            {
                Name = "timestamp_server",
                Background = true
            };

            _systemMetrics.Indexes.CreateOne(new CreateIndexModel<SystemMetrics>(metricsIndexKeys, metricsIndexOptions));

            _logger.LogInformation("MongoDB indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MongoDB indexes");
        }
    }

    public async Task LogCommandAsync(string clientId, string? username, string commandType,
        object? parameters, bool success, double executionTimeMs,
        string? errorMessage = null, object? responseData = null)
    {
        try
        {
            var commandLog = new CommandLog
            {
                ClientId = clientId,
                Username = username,
                CommandType = commandType,
                Parameters = ToBsonDocument(parameters),
                Success = success,
                ErrorMessage = errorMessage,
                ExecutionTimeMs = executionTimeMs,
                ResponseData = ToBsonDocument(responseData)
            };

            await _commandLogs.InsertOneAsync(commandLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging command to MongoDB");
        }
    }

    public async Task LogSystemMetricsAsync(int activeConnections, int activeGroups,
        long totalMessages, long totalFiles, long totalErrors)
    {
        try
        {
            var metrics = new SystemMetrics
            {
                ActiveConnections = activeConnections,
                ActiveGroups = activeGroups,
                TotalMessages = totalMessages,
                TotalFiles = totalFiles,
                TotalErrors = totalErrors
            };

            await _systemMetrics.InsertOneAsync(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging system metrics to MongoDB");
        }
    }

    private static BsonDocument ToBsonDocument(object? obj)
    {
        if (obj == null) return new BsonDocument();

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return BsonDocument.Parse(json);
        }
        catch
        {
            return new BsonDocument("data", obj.ToString());
        }
    }
}

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

// MongoDB를 사용할 수 없는 경우의 더미 구현
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
        // MongoDB가 사용 불가능하므로 로깅 스킵
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
            CurrentActiveGroups = 0,
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
