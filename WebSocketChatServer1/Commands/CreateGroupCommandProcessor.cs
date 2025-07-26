using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Services;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ChatSystem.Commands;

public class CreateGroupCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IRoomActivityService _roomActivityService;

    public CreateGroupCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        IRoomActivityService roomActivityService,
        ILogger<CreateGroupCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _groupManager = groupManager;
        _broadcaster = broadcaster;
        _roomActivityService = roomActivityService;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("createGroup", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("createRoom", StringComparison.OrdinalIgnoreCase));
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
                var entityName = command.Equals("createRoom", StringComparison.OrdinalIgnoreCase) ? "Room" : "Group";
                errorMessage = $"Usage: {command} <{entityName.ToLower()}Name>";
                await SendErrorMessage(clientId, $"Usage: {command} <{entityName.ToLower()}Name>\nExample: {command} My{entityName}");
                return;
            }

            var client = await ClientManager.GetClientAsync(clientId);
            if (client == null)
            {
                errorMessage = "Client not found";
                return;
            }

            var groupName = string.Join(" ", args);
            var groupId = await _groupManager.CreateGroupAsync(groupName, client.Username);

            // 룸/그룹 생성 활동 로그
            await _roomActivityService.LogRoomCreatedAsync(groupId, groupName, client.Username);

            var isRoom = command.Equals("createRoom", StringComparison.OrdinalIgnoreCase);

            // 그룹/룸 생성 메트릭 기록
            var operationType = isRoom ? "create_room" : "create_group";
            ChatTelemetry.GroupOperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operationType),
                new KeyValuePair<string, object?>("name", groupName));

            var entityType = isRoom ? "Room" : "Group";
            var responseType = isRoom ? "roomCreated" : "groupCreated";

            var response = new ChatMessage
            {
                Type = responseType,
                Username = "System",
                Message = $"{entityType} '{groupName}' created successfully. ID: {groupId}",
                GroupId = groupId,
                Timestamp = DateTime.UtcNow
            };

            await _broadcaster.SendToClientAsync(clientId, response);
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