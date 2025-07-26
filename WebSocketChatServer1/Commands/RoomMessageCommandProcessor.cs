using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSystem.Commands;

public class RoomMessageCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;

    public RoomMessageCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<RoomMessageCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _groupManager = groupManager;
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("roomMessage", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
    {
        if (args.Length < 2)
        {
            await SendErrorMessage(clientId, $"Usage: roomMessage <roomId> <message>\nExample: roomMessage abc-123 Hello room!");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        // Room ID 정리 (파이프 문자 제거)
        var roomId = args[0].Trim('|', ' ');
        var message = string.Join(" ", args.Skip(1));

        if (!await _groupManager.IsGroupMemberAsync(roomId, client.Username))
        {
            await SendErrorMessage(clientId, $"You are not a member of room '{roomId}'");
            return;
        }

        var roomMessage = new ChatMessage
        {
            Type = "roomMessage",
            Username = client.Username,
            Message = message,
            GroupId = roomId,
            ChatType = "room",
            Timestamp = DateTime.UtcNow
        };

        // 룸 멤버들에게 메시지 전송
        await BroadcastToRoomMembers(roomId, roomMessage);
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

    private async Task BroadcastToRoomMembers(string roomId, ChatMessage message)
    {
        var members = await _groupManager.GetGroupMembersAsync(roomId);
        var clients = await ClientManager.GetAllClientsAsync();

        var tasks = new List<Task>();
        foreach (var member in members)
        {
            var memberClient = clients.FirstOrDefault(c => c.Username == member);
            if (memberClient != null)
            {
                tasks.Add(_broadcaster.SendToClientAsync(memberClient.Id, message));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        Logger.LogInformation($"Room message sent to {tasks.Count} members in room {roomId}");
    }
}