using ChatSystem.Interfaces;
using ChatSystem.Models;

using Microsoft.Extensions.Logging;

using MongoDB.Bson;
using MongoDB.Driver;

namespace WebSocketChatServer1.Services;

public class MongoCommandLogger : ICommandLogger
{
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoCommandLogger> _logger;

    public MongoCommandLogger(IMongoDatabase? database, ILogger<MongoCommandLogger> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogWarning("MongoDB database is not available - command logging skipped");
                return;
            }

            var collection = _database.GetCollection<CommandLog>("command_logs");

            var commandLog = new CommandLog
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ClientId = clientId,
                Username = username,
                CommandType = commandType,
                Parameters = parameters,
                Success = success,
                ExecutionTimeMs = executionTimeMs,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            await collection.InsertOneAsync(commandLog);
            _logger.LogDebug($"Command logged to MongoDB: {commandType} by {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log command {commandType} to MongoDB for user {username}");
        }
    }
}