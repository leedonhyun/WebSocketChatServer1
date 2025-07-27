
using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;

namespace WebSocketChatServer1.Commands;

public class GroupChatCommandProcessor : ICommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IGroupManager _groupManager;
    private readonly IClientManager _clientManager;
    private readonly ILogger<GroupChatCommandProcessor> _logger;

    public GroupChatCommandProcessor(
        IMessageBroadcaster broadcaster,
        IGroupManager groupManager,
        IClientManager clientManager,
        ILogger<GroupChatCommandProcessor> logger)
    {
        _broadcaster = broadcaster;
        _groupManager = groupManager;
        _clientManager = clientManager;
        _logger = logger;
    }

    public string Command => "/groupchat";

    public Task<bool> CanProcessAsync(string command)
    {
        throw new NotImplementedException();
    }

    public async Task ProcessAsync(string clientId,string command, string[] args, CancellationToken cancellationToken = default)
    {
        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null)
        {
            _logger.LogWarning("Client not found for ID: {ClientId}", clientId);
            return;
        }

        var parts = args?.Split(' ', 2);
        if (parts == null || parts.Length < 2)
        {
            await _broadcaster.SendToClientAsync(clientId, new ChatMessage
            {
                Type = "error",
                Username = "System",
                Message = "Usage: /groupchat <group_id> <message>"
            });
            return;
        }

        var groupId = parts[0];
        var messageContent = parts[1];

        var isMember = await _groupManager.IsGroupMemberAsync(groupId, client.Username);
        if (!isMember)
        {
            await _broadcaster.SendToClientAsync(clientId, new ChatMessage
            {
                Type = "error",
                Username = "System",
                Message = "You are not a member of this group."
            }, cancellationToken);
            return;
        }

        var message = new ChatMessage
        {
            Type = "chat",
            ChatType = "group",
            Username = client.Username,
            Message = messageContent,
            GroupId = groupId,
            Timestamp = DateTime.UtcNow
        };

        // Send to all members of the group, excluding the sender
        await _broadcaster.SendToGroupAsync(groupId, message, client.Username, cancellationToken);
        _logger.LogInformation("User {Username} sent a message to group {GroupId}", client.Username, groupId);
    }
}