using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSystem.Commands;

public class PrivateMessageCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;

    public PrivateMessageCommandProcessor(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        ILogger<PrivateMessageCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("privateMessage", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("pm", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("dm", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;

        try
        {
            if (args.Length < 2)
            {
                errorMessage = $"Usage: {command} <username> <message>";
                await SendErrorMessage(clientId, $"Usage: {command} <username> <message>\nExample: {command} John Hello there!");
                return;
            }

            var client = await ClientManager.GetClientAsync(clientId);
            if (client == null)
            {
                errorMessage = "Client not found";
                return;
            }

            var targetUsername = args[0];
            var message = string.Join(" ", args.Skip(1));

            // Username ���� (ROOM: ���λ簡 �ִ� ��� ���͸�)
            if (targetUsername.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Invalid username '{targetUsername}' - cannot send private message to room identifier";
                await SendErrorMessage(clientId, errorMessage);
                return;
            }

            // ��� ����� ã��
            var allClients = await ClientManager.GetAllClientsAsync();
            var targetClient = allClients.FirstOrDefault(c => c.Username == targetUsername && !c.Username.StartsWith("ROOM:"));

            if (targetClient == null)
            {
                errorMessage = $"User '{targetUsername}' not found or not online";
                await SendErrorMessage(clientId, errorMessage);
                return;
            }

            // �޽��� ����
            var privateMessage = new ChatMessage
            {
                Type = "privateMessage",
                Username = client.Username,
                Message = message,
                ToUsername = targetUsername,
                ChatType = "private",
                Timestamp = DateTime.UtcNow
            };

            // �����ڿ��� �޽��� ����
            await _broadcaster.SendToClientAsync(targetClient.Id, privateMessage, cancellationToken);

            // �۽��ڿ��� Ȯ�� �޽��� ����
            var confirmMessage = new ChatMessage
            {
                Type = "privateMessage",
                Username = client.Username,
                Message = message,
                ToUsername = targetUsername,
                ChatType = "private",
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.SendToClientAsync(clientId, confirmMessage, cancellationToken);

            // ���� �޽��� ��Ʈ�� ���
            ChatTelemetry.PrivateMessagesTotal.Add(1,
                new KeyValuePair<string, object?>("from.username", client.Username),
                new KeyValuePair<string, object?>("to.username", targetUsername));

            Logger.LogInformation($"Private message sent from {client.Username} to {targetUsername}");
            success = true;
        }
        finally
        {
            stopwatch.Stop();
            await LogCommandAsync(clientId, command, args, stopwatch.Elapsed.TotalMilliseconds, success, errorMessage);
        }
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
}