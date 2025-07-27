using Microsoft.AspNetCore.SignalR;

using System.Diagnostics;

using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Services;
using WebSocketChatServer1.Telemetry;

using WebSocketChatShared;
using WebSocketChatShared.Models;

namespace WebSocketChatServer1.Commands;
public class CreateRoomCommandProcessor : BaseCommandProcessor
{
    private readonly IRoomManager _roomManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IRoomActivityService _roomActivityService;

    public CreateRoomCommandProcessor(
        IClientManager clientManager,
        IRoomManager roomManager,
        IMessageBroadcaster broadcaster,
        ICommandLogger commandLogger,
        IRoomActivityService roomActivityService,
        ILogger<CreateRoomCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _roomManager = roomManager;
        _broadcaster = broadcaster;
        _roomActivityService = roomActivityService;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("createRoom", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;

        try
        {
            if (args.Length == 0)
            {
                var entityName = command.Equals("createRoom", StringComparison.OrdinalIgnoreCase) ? "Room" : "";
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
            //   Message = $"{roomName}{ChatConstants.CommandArgSeparator}{description}{ChatConstants.CommandArgSeparator}{isPrivate}{ChatConstants.CommandArgSeparator}{password ?? ""}", Timestamp = DateTi
            var message = args[0].Split(ChatConstants.CommandArgSeparator);
            var roomName = message[0];// string.Join(" ", args);
            var roomId = await _roomManager.CreateRoomAsync(roomName, client.Username);

            // 룸/그룹 생성 활동 로그
            await _roomActivityService.LogRoomCreatedAsync(roomId, roomName, client.Username);

            var isRoom = command.Equals("createRoom", StringComparison.OrdinalIgnoreCase);

            // 그룹/룸 생성 메트릭 기록
            var operationType = isRoom ? "create_room" : "";
            ChatTelemetry.RoomOperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operationType),
                new KeyValuePair<string, object?>("name", roomName));

            var entityType = isRoom ? "Room" : "";
            var responseType = isRoom ? "roomCreated" : "";

            var response = new ChatMessage
            {
                Type = responseType,
                Username = "System",
                Message = $"{entityType} '{roomName}' created successfully. ID: {roomId}",
                RoomId = roomId,
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

    public override async Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        await this.ProcessAsync(clientId, chatMessage.Type, chatMessage.Message.Split(' '), cancellationToken);
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
