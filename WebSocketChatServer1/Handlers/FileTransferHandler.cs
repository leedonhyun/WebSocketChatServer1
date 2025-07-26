using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Handlers;

public class FileTransferHandler : IMessageHandler<FileTransferMessage>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IClientManager _clientManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IFileTransferStateService _transferStateService;
    private readonly ILogger<FileTransferHandler> _logger;

    public FileTransferHandler(
        IFileStorageService fileStorage,
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        IFileTransferStateService transferStateService,
        ILogger<FileTransferHandler> logger)
    {
        _fileStorage = fileStorage;
        _clientManager = clientManager;
        _broadcaster = broadcaster;
        _transferStateService = transferStateService;
        _logger = logger;
    }

    public async Task HandleAsync(string clientId, FileTransferMessage message)
    {
        switch (message.Type)
        {
            case "fileUpload":
                await HandleFileUploadAsync(clientId, message);
                break;
            case "fileUploadComplete":
                await HandleFileUploadCompleteAsync(clientId, message);
                break;
            case "fileOffer":
                await HandleFileOfferAsync(clientId, message);
                break;
            case "fileOfferAuto":
                await HandleFileOfferAutoAsync(clientId, message);
                break;
            case "fileAccept":
                await HandleFileAcceptAsync(clientId, message);
                break;
            case "fileReject":
                await HandleFileRejectAsync(clientId, message);
                break;
            default:
                _logger.LogWarning($"Unknown file transfer message type: {message.Type}");
                break;
        }
    }

    private async Task HandleFileUploadAsync(string clientId, FileTransferMessage message)
    {
        if (message.FileInfo == null || message.Data == null) return;

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        if (message.ChunkIndex == 0)
        {
            message.FileInfo.FromUsername = client.Username;
            _transferStateService.TryAddTransfer(message.FileId, message.FileInfo);
            _logger.LogInformation($"Starting file upload: {message.FileInfo.FileName} from {client.Username}");
        }

        await _fileStorage.SaveFileAsync(message.FileId, message.FileInfo.FileName, message.Data, true);
        _logger.LogDebug($"Upload chunk {message.ChunkIndex + 1}/{message.TotalChunks} for {message.FileInfo.FileName}");
    }

    private async Task HandleFileOfferAutoAsync(string clientId, FileTransferMessage message)
    {
        if (message.FileInfo == null) return;

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        var filePath = _fileStorage.GetFilePath(message.FileId, message.FileInfo.FileName);
        if (!await _fileStorage.FileExistsAsync(filePath))
        {
            _logger.LogError($"Auto file offer for non-existent file: {filePath}");
            return;
        }

        message.FromUsername = client.Username;
        message.FileInfo.FromUsername = client.Username;

        // 파일 정보를 _activeTransfers에 추가
        _transferStateService.TryAddTransfer(message.FileId, message.FileInfo);
        // 자동 다운로드 시작 알림
        var autoMessage = new ChatMessage
        {
            Type = "system",
            Message = $"{client.Username} is sharing file '{message.FileInfo.FileName}' - auto-downloading to all users...",
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.BroadcastAsync(autoMessage, clientId);

        // 모든 클라이언트에게 자동 전송
        var allClients = await _clientManager.GetAllClientsAsync();
        var transferTasks = allClients
            .Where(c => c.Id != clientId)
            .Select(c => StartFileTransferAsync(message.FileId, c.Username))
            .ToList();

        await Task.WhenAll(transferTasks);

        var completeMessage = new ChatMessage
        {
            Type = "system",
            Message = $"Auto-download completed: '{message.FileInfo.FileName}' sent to {transferTasks.Count} users",
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.BroadcastAsync(completeMessage);
    }

    private async Task StartFileTransferAsync(string fileId, string targetUsername)
    {
        if (!_transferStateService.TryGetTransfer(fileId, out var fileInfo)) return;

        var filePath = _fileStorage.GetFilePath(fileId, fileInfo.FileName);
        if (!await _fileStorage.FileExistsAsync(filePath)) return;

        try
        {
            const int chunkSize = 4096;
            var fileData = await _fileStorage.ReadFileAsync(filePath);
            var totalChunks = (int)Math.Ceiling((double)fileData.Length / chunkSize);

            for (int i = 0; i < totalChunks; i++)
            {
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, fileData.Length - offset);
                var chunkData = new byte[length];
                Array.Copy(fileData, offset, chunkData, 0, length);

                var dataMessage = new FileTransferMessage
                {
                    Type = "fileData",
                    FileId = fileId,
                    FileInfo = fileInfo,
                    Data = chunkData,
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    FromUsername = fileInfo.FromUsername,
                    ToUsername = targetUsername,
                    Timestamp = DateTime.UtcNow
                };

                await _broadcaster.SendToUsernameAsync(targetUsername, dataMessage);
                await Task.Delay(50); // 전송 속도 조절
            }

            var completeMessage = new FileTransferMessage
            {
                Type = "fileComplete",
                FileId = fileId,
                FileInfo = fileInfo,
                FromUsername = fileInfo.FromUsername,
                ToUsername = targetUsername,
                Timestamp = DateTime.UtcNow
            };

            await _broadcaster.SendToUsernameAsync(targetUsername, completeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error transferring file to {targetUsername}");
        }
    }

    // 다른 핸들러 메서드들도 유사하게 구현...
    private async Task HandleFileUploadCompleteAsync(string clientId, FileTransferMessage message)
    {
        if (message.FileInfo == null)
        {
            _logger.LogWarning("File upload complete received with null FileInfo");
            return;
        }

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        var filePath = _fileStorage.GetFilePath(message.FileId, message.FileInfo.FileName);
        _logger.LogInformation($"File upload completed: {filePath}");

        if (await _fileStorage.FileExistsAsync(filePath))
        {
            var fileData = await _fileStorage.ReadFileAsync(filePath);
            var actualSize = fileData.Length;

            _logger.LogInformation($"File {message.FileInfo.FileName} uploaded successfully - Expected: {message.FileInfo.FileSize}, Actual: {actualSize} bytes");

            if (actualSize == message.FileInfo.FileSize)
            {
                _logger.LogInformation($"File upload verified: {message.FileInfo.FileName}");
            }
            else
            {
                _logger.LogWarning($"File size mismatch for {message.FileInfo.FileName}");
            }

            // 업로드 완료 알림 메시지
            var systemMessage = new ChatMessage
            {
                Type = "system",
                Username = "System",
                Message = $"{client.Username} uploaded file: {message.FileInfo.FileName} - ready for download",
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.BroadcastAsync(systemMessage);
        }
        else
        {
            _logger.LogError($"File {message.FileInfo.FileName} was not uploaded properly to {filePath}");
        }
    }

    private async Task HandleFileOfferAsync(string clientId, FileTransferMessage message)
    {
        if (message.FileInfo == null)
        {
            _logger.LogWarning("File offer received with null FileInfo");
            return;
        }

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        var filePath = _fileStorage.GetFilePath(message.FileId, message.FileInfo.FileName);
        if (!await _fileStorage.FileExistsAsync(filePath))
        {
            _logger.LogError($"File offer for non-existent file: {filePath}");

            // 에러 메시지를 송신자에게 전송
            var errorMessage = new FileTransferMessage
            {
                Type = "fileError",
                FileId = message.FileId,
                FromUsername = "System",
                ToUsername = client.Username,
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.SendToClientAsync(clientId, errorMessage);
            return;
        }

        message.FromUsername = client.Username;
        message.FileInfo.FromUsername = client.Username;

        // 파일 정보를 _activeTransfers에 추가
        _transferStateService.TryAddTransfer(message.FileId, message.FileInfo);

        _logger.LogInformation($"File offer from {client.Username}: {message.FileInfo.FileName} ({message.FileInfo.FileSize} bytes)");

        // 특정 사용자에게만 전송하거나 모든 사용자에게 브로드캐스트
        if (string.IsNullOrEmpty(message.ToUsername))
        {
            _logger.LogInformation($"Broadcasting file offer to all users");
            await _broadcaster.BroadcastAsync(message, clientId);
        }
        else
        {
            _logger.LogInformation($"Sending file offer to {message.ToUsername}");
            await _broadcaster.SendToUsernameAsync(message.ToUsername, message);
        }

        // 파일 제안 알림 메시지
        var systemMessage = new ChatMessage
        {
            Type = "system",
            Username = "System",
            Message = string.IsNullOrEmpty(message.ToUsername)
                ? $"{client.Username} offered file '{message.FileInfo.FileName}' to everyone"
                : $"{client.Username} offered file '{message.FileInfo.FileName}' to {message.ToUsername}",
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.BroadcastAsync(systemMessage, clientId);
    }

    private async Task HandleFileAcceptAsync(string clientId, FileTransferMessage message)
    {
        if (!_transferStateService.TryGetTransfer(message.FileId, out var fileInfo))
        {
            _logger.LogWarning($"File accept for unknown file ID: {message.FileId}");

            // 에러 메시지를 수신자에게 전송
            var errorMessage = new FileTransferMessage
            {
                Type = "fileError",
                FileId = message.FileId,
                FromUsername = "System",
                ToUsername = "",
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.SendToClientAsync(clientId, errorMessage);
            return;
        }

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        _logger.LogInformation($"File accepted: {fileInfo.FileName} by {client.Username}");

        // 송신자에게 수락 알림
        var acceptNotification = new FileTransferMessage
        {
            Type = "fileAccept",
            FileId = message.FileId,
            FromUsername = client.Username,
            ToUsername = fileInfo.FromUsername,
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.SendToUsernameAsync(fileInfo.FromUsername, acceptNotification);

        // 채팅에 수락 알림
        var systemMessage = new ChatMessage
        {
            Type = "system",
            Username = "System",
            Message = $"{client.Username} accepted file '{fileInfo.FileName}' from {fileInfo.FromUsername}",
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.BroadcastAsync(systemMessage);

        // 즉시 파일 전송 시작
        await StartFileTransferAsync(message.FileId, client.Username);
    }

    private async Task HandleFileRejectAsync(string clientId, FileTransferMessage message)
    {
        if (!_transferStateService.TryGetTransfer(message.FileId, out var fileInfo))
        {
            _logger.LogWarning($"File reject for unknown file ID: {message.FileId}");
            return;
        }

        var client = await _clientManager.GetClientAsync(clientId);
        if (client == null) return;

        _logger.LogInformation($"File rejected: {fileInfo.FileName} by {client.Username}");

        // 송신자에게 거절 알림
        var rejectNotification = new FileTransferMessage
        {
            Type = "fileReject",
            FileId = message.FileId,
            FromUsername = client.Username,
            ToUsername = fileInfo.FromUsername,
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.SendToUsernameAsync(fileInfo.FromUsername, rejectNotification);

        // 채팅에 거절 알림
        var systemMessage = new ChatMessage
        {
            Type = "system",
            Username = "System",
            Message = $"{client.Username} rejected file '{fileInfo.FileName}' from {fileInfo.FromUsername}",
            Timestamp = DateTime.UtcNow
        };
        await _broadcaster.BroadcastAsync(systemMessage);

        // 전송 정보에서 제거 (다른 사용자들은 여전히 수락 가능)
        // 완전 제거하지 않고 특정 사용자만 거절 처리하려면 별도 거절 목록 관리
        // 예: _transferStateService.TryRemoveTransfer(message.FileId);
    }
}