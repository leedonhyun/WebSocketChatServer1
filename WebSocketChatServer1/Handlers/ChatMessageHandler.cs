using ChatSystem.Interfaces;
using ChatSystem.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ChatSystem.Handlers;
public class ChatMessageHandler : IMessageHandler<ChatMessage>
{
    private readonly IClientManager _clientManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly ICommandLogger _commandLogger;
    private readonly ILogger<ChatMessageHandler> _logger;

    public ChatMessageHandler(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<ChatMessageHandler> logger)
    {
        _clientManager = clientManager;
        _broadcaster = broadcaster;
        _commandLogger = commandLogger;
        _logger = logger;
    }

    public async Task HandleAsync(string clientId, ChatMessage message)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = true;
        string? errorMessage = null;

        try
        {
            var client = await _clientManager.GetClientAsync(clientId);
            if (client == null)
            {
                _logger.LogWarning($"Message from unknown client: {clientId}");
                success = false;
                errorMessage = "Unknown client";
                return;
            }

            message.Username = client.Username;
            message.Timestamp = DateTime.UtcNow;

            _logger.LogInformation($"Broadcasting message from {client.Username}: {message.Message}");
            await _broadcaster.BroadcastAsync(message, clientId);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _logger.LogError(ex, "Error handling chat message");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var client = await _clientManager.GetClientAsync(clientId);
            await _commandLogger.LogCommandAsync(
                clientId,
                client?.Username ?? "Unknown",
                "ChatMessage",
                $"Message: {message.Message}, Length: {message.Message?.Length}",
                success,
                stopwatch.Elapsed.TotalMilliseconds,
                errorMessage);
        }
    }
}