using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Commands;

public class SendFileCommandProcessor : BaseCommandProcessor
{
    //private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IFileStorageService _fileStorage;

    public SendFileCommandProcessor(
        IClientManager clientManager,
        //IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        IFileStorageService fileStorage,
        ICommandLogger commandLogger,
        ILogger<SendFileCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        //_groupManager = groupManager;
        _broadcaster = broadcaster;
        _fileStorage = fileStorage;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("send", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length < 1)
        {
            await SendErrorMessage(clientId, "Usage: send <filepath> [username|roomId]\nExample: send C:\\file.txt john or send C:\\file.txt room-uuid");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        var filePath = args[0];

        // ���� ���� Ȯ��
        if (!File.Exists(filePath))
        {
            await SendErrorMessage(clientId, $"File not found: {filePath}");
            return;
        }

        var fileName = Path.GetFileName(filePath);
        var fileId = Guid.NewGuid().ToString();

        try
        {
            // ������ ������ ����
            var fileData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var savedPath = await _fileStorage.SaveFileAsync(fileId, fileName, fileData);

            if (args.Length == 1)
            {
                // ��ü ��ε�ĳ��Ʈ
                await BroadcastFileOffer(clientId, fileId, fileName, fileData.Length, null, cancellationToken);
            }
            else
            {
                var target = args[1];

                // Room ID���� Ȯ�� (UUID ����)
                if (Guid.TryParse(target, out _))
                {
                    // Room�� ���� ����
                    await SendFileToRoom(clientId, fileId, fileName, fileData.Length, target, cancellationToken);
                }
                else
                {
                    // ����ڿ��� ���� ����
                    await SendFileToUser(clientId, fileId, fileName, fileData.Length, target, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error processing file send: {filePath}");
            await SendErrorMessage(clientId, $"Error processing file: {ex.Message}");
        }
    }

    private async Task BroadcastFileOffer(string senderId, string fileId, string fileName, long fileSize, string? excludeUsername, CancellationToken cancellationToken = default)
    {
        var message = new ChatMessage
        {
            Type = "fileOffer",
            Username = "System",
            Message = $"File available for download: {fileName} ({fileSize} bytes)",
            Timestamp = DateTime.UtcNow
        };

        await _broadcaster.BroadcastAsync(message, senderId, cancellationToken);
    }

    private async Task SendFileToRoom(string senderId, string fileId, string fileName, long fileSize, string roomId, CancellationToken cancellationToken = default)
    {
        var client = await ClientManager.GetClientAsync(senderId);
        if (client == null) return;

        // Room ����� Ȯ��
        //if (!await _groupManager.IsGroupMemberAsync(roomId, client.Username))
        //{
        //    await SendErrorMessage(senderId, $"You are not a member of room '{roomId}'");
        //    return;
        //}

        //// Room ����鿡�� ���� ���� �˸�
        //var members = await _groupManager.GetGroupMembersAsync(roomId);
        var clients = await ClientManager.GetAllClientsAsync();

        var message = new ChatMessage
        {
            Type = "fileOffer",
            Username = client.Username,
            Message = $"File shared in room: {fileName} ({fileSize} bytes)",
            GroupId = roomId,
            ChatType = "room",
            Timestamp = DateTime.UtcNow
        };

        var tasks = new List<Task>();
        //foreach (var member in members)
        //{
        //    if (member == client.Username) continue; // �۽��� ����

        //    var memberClient = clients.FirstOrDefault(c => c.Username == member);
        //    if (memberClient != null)
        //    {
        //        tasks.Add(_broadcaster.SendToClientAsync(memberClient.Id, message, cancellationToken));
        //    }
        //}

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        Logger.LogInformation($"File {fileName} offered to {tasks.Count} members in room {roomId}");
    }

    private async Task SendFileToUser(string senderId, string fileId, string fileName, long fileSize, string targetUsername, CancellationToken cancellationToken = default)
    {
        var client = await ClientManager.GetClientAsync(senderId);
        if (client == null) return;

        // ��� ����� ����
        if (targetUsername.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
        {
            await SendErrorMessage(senderId, $"Invalid username '{targetUsername}' - cannot send file to room identifier");
            return;
        }

        var clients = await ClientManager.GetAllClientsAsync();
        var targetClient = clients.FirstOrDefault(c => c.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase)
            && !c.Username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase));

        if (targetClient == null)
        {
            await SendErrorMessage(senderId, $"User '{targetUsername}' not found or not online");
            return;
        }

        var message = new ChatMessage
        {
            Type = "fileOffer",
            Username = client.Username,
            Message = $"File sent to you: {fileName} ({fileSize} bytes)",
            ToUsername = targetUsername,
            ChatType = "private",
            Timestamp = DateTime.UtcNow
        };

        await _broadcaster.SendToClientAsync(targetClient.Id, message, cancellationToken);
        Logger.LogInformation($"File {fileName} offered to user {targetUsername}");
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