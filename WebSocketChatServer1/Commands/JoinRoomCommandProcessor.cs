using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSocketChatServer1.Commands;

namespace WebSocketChatServer1.Commands;

public class JoinRoomCommandProcessor : BaseCommandProcessor
{
    private readonly IRoomManager _roomManager;
    private readonly IMessageBroadcaster _broadcaster;
    
    public JoinRoomCommandProcessor(
        IClientManager clientManager,
        IRoomManager roomManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<JoinRoomCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _roomManager = roomManager    ;
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            var entityName = command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase) ? "Room" : "";
            await SendErrorMessage(clientId, $"Usage: {command} <{entityName.ToLower()}Id>\nExample: {command} abc-123-def-456");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        // Room ID 정리 (파이프 문자 제거)
        var roomId = args[0].Trim('|', ' ');
        var room= await _roomManager.GetRoomAsync(roomId);

        var isRoom = command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase);
        var entityType = isRoom ? "Room" : "";

        if (room == null)
        {
            await SendErrorMessage(clientId, $"{entityType} '{roomId}' does not exist");
            return;
        }

        if (await _roomManager.IsRoomMemberAsync(roomId, client.Username))
        {
            await SendErrorMessage(clientId, $"You are already a member of {entityType.ToLower()} '{room.Name}'");
            return;
        }

        await _roomManager.AddMemberAsync(roomId, client.Username);

        // 그룹/룸 참가 메트릭 기록
        var operationType = isRoom ? "join_room" : "";
        ChatTelemetry.RoomOperationsTotal.Add(1,
            new KeyValuePair<string, object?>("operation", operationType),
            new KeyValuePair<string, object?>("room.id", roomId),
            new KeyValuePair<string, object?>("room.name", room.Name));

        var responseType = isRoom ? "roomJoined" : "";
        var response = new ChatMessage
        {
            Type = responseType,
            Username = "System",
            Message = $"Successfully joined {entityType.ToLower()} '{room.Name}'",
            RoomId = roomId,
            Timestamp = DateTime.UtcNow
        };

        await _broadcaster.SendToClientAsync(clientId, response, cancellationToken);

        // 그룹 멤버들에게 새 멤버 알림
        await BroadcastToRoomMembers(roomId, new ChatMessage
        {
            Type = "system",
            Username = "System",
            Message = $"{client.Username} joined the {entityType.ToLower()}",
            RoomId = roomId,
            ChatType = isRoom ? "room" : "",
            Timestamp = DateTime.UtcNow
        }, client.Username);
    }

    private async Task SendErrorMessage(string clientId, string errorMessage)
    {
        var error = new ChatMessage
        {
            Type = "error",
            Username = "System",
            Message = errorMessage,
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.SendToClientAsync(clientId, error);
    }

    private async Task BroadcastToRoomMembers(string roomId, ChatMessage message, string? excludeUsername = null, CancellationToken  cancellationToken = default)
    {
        var members = await _roomManager.GetRoomMembersAsync(roomId);
        var clients = await ClientManager.GetAllClientsAsync();

        var tasks = new List<Task>();
        foreach (var member in members.Where(m => m != excludeUsername))
        {
            var memberClient = clients.FirstOrDefault(c => c.Username == member);
            if (memberClient != null)
            {
                tasks.Add(_broadcaster.SendToClientAsync(memberClient.Id, message, cancellationToken));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public override async Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        await this.ProcessAsync(clientId, chatMessage.Type, chatMessage.Message.Split(' '), cancellationToken);
    }
}