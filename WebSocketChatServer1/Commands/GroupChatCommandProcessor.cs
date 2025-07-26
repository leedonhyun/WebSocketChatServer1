using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSystem.Commands;

public class GroupChatCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;

    public GroupChatCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<GroupChatCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _groupManager = groupManager;
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("groupChat", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("roomChat", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
    {
        if (args.Length < 2)
        {
            var entityName = command.Equals("roomChat", StringComparison.OrdinalIgnoreCase) ? "room" : "group";
            await SendErrorMessage(clientId, $"Usage: {command} <{entityName}Id> <message>\nExample: {command} abc-123 Hello everyone!");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        // Group/Room ID ���� (������ ���� ����)
        var groupId = args[0].Trim('|', ' ');
        var message = string.Join(" ", args.Skip(1));

        if (!await _groupManager.IsGroupMemberAsync(groupId, client.Username))
        {
            var entityName = command.Equals("roomChat", StringComparison.OrdinalIgnoreCase) ? "room" : "group";
            await SendErrorMessage(clientId, $"You are not a member of {entityName} '{groupId}'");
            return;
        }

        var isRoom = command.Equals("roomChat", StringComparison.OrdinalIgnoreCase);
        var messageType = isRoom ? "roomChat" : "groupChat";
        var chatType = isRoom ? "room" : "group";

        var groupMessage = new ChatMessage
        {
            Type = messageType,
            Username = client.Username,
            Message = message,
            GroupId = groupId,
            ChatType = chatType,
            Timestamp = DateTime.UtcNow
        };

        // �׷� ����鿡�� �޽��� ����
        await BroadcastToGroupMembers(groupId, groupMessage);
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

    private async Task BroadcastToGroupMembers(string groupId, ChatMessage message)
    {
        var members = await _groupManager.GetGroupMembersAsync(groupId);
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

        Logger.LogInformation($"Group message sent to {tasks.Count} members in group {groupId}");
    }
}