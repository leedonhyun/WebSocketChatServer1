using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatSystem.Commands;
public abstract class BaseCommandProcessor : ICommandProcessor
{
    protected readonly IClientManager ClientManager;
    protected readonly ICommandLogger CommandLogger;
    protected readonly ILogger Logger;

    protected BaseCommandProcessor(IClientManager clientManager, ICommandLogger commandLogger, ILogger logger)
    {
        ClientManager = clientManager;
        CommandLogger = commandLogger;
        Logger = logger;
    }

    public abstract Task<bool> CanProcessAsync(string command);
    public abstract Task ProcessAsync(string clientId, string command, string[] args);

    protected async Task LogCommandAsync(string clientId, string command, string[] args, double executionTimeMs = 0, bool success = true, string? errorMessage = null)
    {
        try
        {
            var client = await ClientManager.GetClientAsync(clientId);
            var username = client?.Username ?? "Unknown";

            // OpenTelemetry 메트릭 기록
            ChatTelemetry.CommandsExecutedTotal.Add(1,
                new KeyValuePair<string, object?>("command.type", command),
                new KeyValuePair<string, object?>("command.success", success));

            ChatTelemetry.CommandExecutionDuration.Record(executionTimeMs / 1000.0, // Convert to seconds
                new KeyValuePair<string, object?>("command.type", command),
                new KeyValuePair<string, object?>("command.success", success));

            if (!success)
            {
                ChatTelemetry.CommandErrorsTotal.Add(1,
                    new KeyValuePair<string, object?>("command.type", command),
                    new KeyValuePair<string, object?>("error.message", errorMessage ?? "Unknown"));
            }

            // MongoDB 로그 기록
            await CommandLogger.LogCommandAsync(
                clientId: clientId,
                username: username,
                commandType: command,
                parameters: args.Length > 0 ? string.Join(", ", args) : null,
                success: success,
                executionTimeMs: executionTimeMs,
                errorMessage: errorMessage
            );
        }
        catch (System.Exception ex)
        {
            Logger.LogError(ex, "Failed to log command execution to MongoDB");
        }
    }
}