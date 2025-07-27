using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using WebSocketChatShared;

namespace WebSocketChatServer1.Handlers;
public class ChatMessageHandler : IMessageHandler<ChatMessage>
{
    private readonly IClientManager _clientManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IRoomManager _roomManager;
    private readonly ICommandLogger _commandLogger;
    private readonly ILogger<ChatMessageHandler> _logger;

    public ChatMessageHandler(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        IRoomManager roomManager,
        ICommandLogger commandLogger,
        ILogger<ChatMessageHandler> logger)
    {
        _clientManager = clientManager;
        _broadcaster = broadcaster;
        _roomManager = roomManager;
        _commandLogger = commandLogger;
        _logger = logger;
    }

    public async Task HandleAsync(string clientId, ChatMessage message,CancellationToken cancellationToken)
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

            var room = await _roomManager.GetRoomForClientAsync(clientId);
            if (room != null)
            {
                // Client is in a room, broadcast to room members only
                var memberIds = await _roomManager.GetClientIdsInRoomAsync(room.Id);
                message.RoomId = room.Id;
                message.Type = ChatConstants.MessageTypes.RoomMessage;// "roomChat"; // Set message type to indicate it's a room message
                _logger.LogInformation($"Broadcasting message from {client.Username} to room {room.Id}: {message.Message}");
                await _broadcaster.SendToClientAsync(memberIds, message, cancellationToken);
            }
            else
            {
                // Client is not in a room, broadcast to everyone
                _logger.LogInformation($"Broadcasting public message from {client.Username}: {message.Message}");
                await _broadcaster.BroadcastAsync(message, clientId, cancellationToken);
            }
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