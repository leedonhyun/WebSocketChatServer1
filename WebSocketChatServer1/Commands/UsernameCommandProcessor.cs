using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Services;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSystem.Commands;

public class UsernameCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IUserActivityService _userActivityService;

    public UsernameCommandProcessor(
        IClientManager clientManager,
        ICommandLogger commandLogger,
        IMessageBroadcaster broadcaster,
        IUserActivityService userActivityService,
        ILogger<UsernameCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _broadcaster = broadcaster;
        _userActivityService = userActivityService;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("setUsername", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;

        try
        {
            if (args.Length == 0)
            {
                errorMessage = "Username is required";
                return;
            }

            var client = await ClientManager.GetClientAsync(clientId);
            if (client == null)
            {
                errorMessage = "Client not found";
                return;
            }

            var oldUsername = client.Username;
            var newUsername = string.Join(" ", args);

            // Username 검증 (ROOM: 접두사 금지)
            if (newUsername.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Username cannot start with 'ROOM:' - this prefix is reserved for room identifiers";
                var error = new ChatMessage
                {
                    Type = "error",
                    Username = "System",
                    Message = errorMessage,
                    Timestamp = DateTime.UtcNow
                };
                await _broadcaster.SendToClientAsync(clientId, error);
                return;
            }

            // 중복 username 검증
            var allClients = await ClientManager.GetAllClientsAsync();
            if (allClients.Any(c => c.Id != clientId && c.Username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"Username '{newUsername}' is already taken";
                var error = new ChatMessage
                {
                    Type = "error",
                    Username = "System",
                    Message = errorMessage,
                    Timestamp = DateTime.UtcNow
                };
                await _broadcaster.SendToClientAsync(clientId, error);
                return;
            }

            await ClientManager.UpdateClientUsernameAsync(clientId, newUsername);

            // 사용자 이름 변경 활동 로그
            await _userActivityService.LogUsernameChangedAsync(clientId, oldUsername, newUsername);

            // 사용자명 변경 메트릭 기록
            ChatTelemetry.UsernameChangesTotal.Add(1,
                new KeyValuePair<string, object?>("old.username", oldUsername),
                new KeyValuePair<string, object?>("new.username", newUsername));

            var systemMessage = new ChatMessage
            {
                Type = "system",
                Message = $"{oldUsername} changed name to {newUsername}",
                Timestamp = DateTime.UtcNow
            };

            await _broadcaster.BroadcastAsync(systemMessage, clientId);
            success = true;
        }
        finally
        {
            stopwatch.Stop();
            await LogCommandAsync(clientId, command, args, stopwatch.Elapsed.TotalMilliseconds, success, errorMessage);
        }
    }
}