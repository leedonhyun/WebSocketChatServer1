using WebSocketChatServer1.Interfaces;

using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Monitoring;

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

    public Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        throw new NotImplementedException();
    }
}