using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Commands;

public class UserListCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;

    public UserListCommandProcessor(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<UserListCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("listUsers", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;

        try
        {
            var clients = await ClientManager.GetAllClientsAsync();
            // ROOM: 접두사가 있는 사용자 제외
            var usernames = clients
                .Where(c => !c.Username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Username)
                .ToList();

            var userListMessage = new ChatMessage
            {
                Type = "userList",
                Message = string.Join(",", usernames),
                Timestamp = DateTime.UtcNow
            };

            await _broadcaster.SendToClientAsync(clientId, userListMessage, cancellationToken);
            success = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            await LogCommandAsync(clientId, command, args, stopwatch.Elapsed.TotalMilliseconds, success, errorMessage);
        }
    }

    public override async Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        await this.ProcessAsync(clientId, chatMessage.Message, Array.Empty<string>(), cancellationToken);
    }
}