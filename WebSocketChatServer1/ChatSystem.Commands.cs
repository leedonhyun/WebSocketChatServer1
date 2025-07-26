using ChatSystem.Interfaces;
using ChatSystem.Models;
using ChatSystem.Services;
using ChatSystem.Telemetry;
using System.Diagnostics;

namespace ChatSystem.Commands;
public abstract class BaseCommandProcessor : ICommandProcessor
{
    protected readonly IClientManager ClientManager;
    protected readonly ChatSystem.Interfaces.ICommandLogger CommandLogger;
    protected readonly ILogger Logger;

    protected BaseCommandProcessor(IClientManager clientManager, ChatSystem.Interfaces.ICommandLogger commandLogger, ILogger logger)
    {
        ClientManager = clientManager;
        CommandLogger = commandLogger;
        Logger = logger;
    }

    public abstract Task<bool> CanProcessAsync(string command);
    public abstract Task ProcessAsync(string clientId, string command, string[] args);

    protected async Task LogCommandAsync(string clientId, string command, string[] args, double executionTimeMs = 0, bool success = true, string? errorMessage = null)
    {
        try
        {
            var client = await ClientManager.GetClientAsync(clientId);
            var username = client?.Username ?? "Unknown";

            // OpenTelemetry 메트릭 기록
            ChatTelemetry.CommandsExecutedTotal.Add(1,
                new KeyValuePair<string, object?>("command.type", command),
                new KeyValuePair<string, object?>("command.success", success));

            ChatTelemetry.CommandExecutionDuration.Record(executionTimeMs / 1000.0, // Convert to seconds
                new KeyValuePair<string, object?>("command.type", command),
                new KeyValuePair<string, object?>("command.success", success));

            if (!success)
            {
                ChatTelemetry.CommandErrorsTotal.Add(1,
                    new KeyValuePair<string, object?>("command.type", command),
                    new KeyValuePair<string, object?>("error.message", errorMessage ?? "Unknown"));
            }

            // MongoDB 로그 기록
            await CommandLogger.LogCommandAsync(
                clientId: clientId,
                username: username,
                commandType: command,
                parameters: args.Length > 0 ? string.Join(", ", args) : null,
                success: success,
                executionTimeMs: executionTimeMs,
                errorMessage: errorMessage
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to log command execution to MongoDB");
        }
    }
}

public class UsernameCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IUserActivityService _userActivityService;

    public UsernameCommandProcessor(
        IClientManager clientManager,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
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

public class UserListCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;

    public UserListCommandProcessor(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
        ILogger<UserListCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("listUsers", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
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

            await _broadcaster.SendToClientAsync(clientId, userListMessage);
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
}

// 개인 메시지 명령 처리기
public class PrivateMessageCommandProcessor : BaseCommandProcessor
{
    private readonly IMessageBroadcaster _broadcaster;

    public PrivateMessageCommandProcessor(
        IClientManager clientManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
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

    public override async Task ProcessAsync(string clientId, string command, string[] args)
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

            // Username 검증 (ROOM: 접두사가 있는 경우 필터링)
            if (targetUsername.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Invalid username '{targetUsername}' - cannot send private message to room identifier";
                await SendErrorMessage(clientId, errorMessage);
                return;
            }

            // 대상 사용자 찾기
            var allClients = await ClientManager.GetAllClientsAsync();
            var targetClient = allClients.FirstOrDefault(c => c.Username == targetUsername && !c.Username.StartsWith("ROOM:"));

            if (targetClient == null)
            {
                errorMessage = $"User '{targetUsername}' not found or not online";
                await SendErrorMessage(clientId, errorMessage);
                return;
            }

            // 메시지 생성
            var privateMessage = new ChatMessage
            {
                Type = "privateMessage",
                Username = client.Username,
                Message = message,
                ToUsername = targetUsername,
                ChatType = "private",
                Timestamp = DateTime.UtcNow
            };

            // 수신자에게 메시지 전송
            await _broadcaster.SendToClientAsync(targetClient.Id, privateMessage);

            // 송신자에게 확인 메시지 전송
            var confirmMessage = new ChatMessage
            {
                Type = "privateMessage",
                Username = client.Username,
                Message = message,
                ToUsername = targetUsername,
                ChatType = "private",
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.SendToClientAsync(clientId, confirmMessage);

            // 개인 메시지 메트릭 기록
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

// 그룹 생성 명령 처리기
public class CreateGroupCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IRoomActivityService _roomActivityService;

    public CreateGroupCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
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

// 그룹 참가 명령 처리기
public class JoinGroupCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;

    public JoinGroupCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
        ILogger<JoinGroupCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _groupManager = groupManager;
        _broadcaster = broadcaster;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(
            command.Equals("joinGroup", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
    {
        if (args.Length == 0)
        {
            var entityName = command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase) ? "Room" : "Group";
            await SendErrorMessage(clientId, $"Usage: {command} <{entityName.ToLower()}Id>\nExample: {command} abc-123-def-456");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        // Room ID 정리 (파이프 문자 제거)
        var groupId = args[0].Trim('|', ' ');
        var group = await _groupManager.GetGroupAsync(groupId);

        var isRoom = command.Equals("joinRoom", StringComparison.OrdinalIgnoreCase);
        var entityType = isRoom ? "Room" : "Group";

        if (group == null)
        {
            await SendErrorMessage(clientId, $"{entityType} '{groupId}' does not exist");
            return;
        }

        if (await _groupManager.IsGroupMemberAsync(groupId, client.Username))
        {
            await SendErrorMessage(clientId, $"You are already a member of {entityType.ToLower()} '{group.Name}'");
            return;
        }

        await _groupManager.AddMemberAsync(groupId, client.Username);

        // 그룹/룸 참가 메트릭 기록
        var operationType = isRoom ? "join_room" : "join_group";
        ChatTelemetry.GroupOperationsTotal.Add(1,
            new KeyValuePair<string, object?>("operation", operationType),
            new KeyValuePair<string, object?>("group.id", groupId),
            new KeyValuePair<string, object?>("group.name", group.Name));

        var responseType = isRoom ? "roomJoined" : "groupJoined";
        var response = new ChatMessage
        {
            Type = responseType,
            Username = "System",
            Message = $"Successfully joined {entityType.ToLower()} '{group.Name}'",
            GroupId = groupId,
            Timestamp = DateTime.UtcNow
        };

        await _broadcaster.SendToClientAsync(clientId, response);

        // 그룹 멤버들에게 새 멤버 알림
        await BroadcastToGroupMembers(groupId, new ChatMessage
        {
            Type = "system",
            Username = "System",
            Message = $"{client.Username} joined the {entityType.ToLower()}",
            GroupId = groupId,
            ChatType = isRoom ? "room" : "group",
            Timestamp = DateTime.UtcNow
        }, client.Username);
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

    private async Task BroadcastToGroupMembers(string groupId, ChatMessage message, string? excludeUsername = null)
    {
        var members = await _groupManager.GetGroupMembersAsync(groupId);
        var clients = await ClientManager.GetAllClientsAsync();

        var tasks = new List<Task>();
        foreach (var member in members.Where(m => m != excludeUsername))
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
    }
}

// 그룹 채팅 명령 처리기
public class GroupChatCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;

    public GroupChatCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
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

        // Group/Room ID 정리 (파이프 문자 제거)
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

        // 그룹 멤버들에게 메시지 전송
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

// 룸 메시지 명령 처리기 (roomMessage 전용)
public class RoomMessageCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;

    public RoomMessageCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
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

// 파일 전송 명령 처리기
public class SendFileCommandProcessor : BaseCommandProcessor
{
    private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IFileStorageService _fileStorage;

    public SendFileCommandProcessor(
        IClientManager clientManager,
        IGroupManager groupManager,
        IMessageBroadcaster broadcaster,
        IFileStorageService fileStorage,
        ChatSystem.Interfaces.ICommandLogger commandLogger,
        ILogger<SendFileCommandProcessor> logger) : base(clientManager, commandLogger, logger)
    {
        _groupManager = groupManager;
        _broadcaster = broadcaster;
        _fileStorage = fileStorage;
    }

    public override async Task<bool> CanProcessAsync(string command)
    {
        return await Task.FromResult(command.Equals("send", StringComparison.OrdinalIgnoreCase));
    }

    public override async Task ProcessAsync(string clientId, string command, string[] args)
    {
        if (args.Length < 1)
        {
            await SendErrorMessage(clientId, "Usage: send <filepath> [username|roomId]\nExample: send C:\\file.txt john or send C:\\file.txt room-uuid");
            return;
        }

        var client = await ClientManager.GetClientAsync(clientId);
        if (client == null) return;

        var filePath = args[0];

        // 파일 존재 확인
        if (!File.Exists(filePath))
        {
            await SendErrorMessage(clientId, $"File not found: {filePath}");
            return;
        }

        var fileName = Path.GetFileName(filePath);
        var fileId = Guid.NewGuid().ToString();

        try
        {
            // 파일을 서버에 저장
            var fileData = await File.ReadAllBytesAsync(filePath);
            var savedPath = await _fileStorage.SaveFileAsync(fileId, fileName, fileData);

            if (args.Length == 1)
            {
                // 전체 브로드캐스트
                await BroadcastFileOffer(clientId, fileId, fileName, fileData.Length, null);
            }
            else
            {
                var target = args[1];

                // Room ID인지 확인 (UUID 형태)
                if (Guid.TryParse(target, out _))
                {
                    // Room에 파일 전송
                    await SendFileToRoom(clientId, fileId, fileName, fileData.Length, target);
                }
                else
                {
                    // 사용자에게 파일 전송
                    await SendFileToUser(clientId, fileId, fileName, fileData.Length, target);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error processing file send: {filePath}");
            await SendErrorMessage(clientId, $"Error processing file: {ex.Message}");
        }
    }

    private async Task BroadcastFileOffer(string senderId, string fileId, string fileName, long fileSize, string? excludeUsername)
    {
        var message = new ChatMessage
        {
            Type = "fileOffer",
            Username = "System",
            Message = $"File available for download: {fileName} ({fileSize} bytes)",
            Timestamp = DateTime.UtcNow
        };

        await _broadcaster.BroadcastAsync(message, senderId);
    }

    private async Task SendFileToRoom(string senderId, string fileId, string fileName, long fileSize, string roomId)
    {
        var client = await ClientManager.GetClientAsync(senderId);
        if (client == null) return;

        // Room 멤버십 확인
        if (!await _groupManager.IsGroupMemberAsync(roomId, client.Username))
        {
            await SendErrorMessage(senderId, $"You are not a member of room '{roomId}'");
            return;
        }

        // Room 멤버들에게 파일 제공 알림
        var members = await _groupManager.GetGroupMembersAsync(roomId);
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
        foreach (var member in members)
        {
            if (member == client.Username) continue; // 송신자 제외

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

        Logger.LogInformation($"File {fileName} offered to {tasks.Count} members in room {roomId}");
    }

    private async Task SendFileToUser(string senderId, string fileId, string fileName, long fileSize, string targetUsername)
    {
        var client = await ClientManager.GetClientAsync(senderId);
        if (client == null) return;

        // 대상 사용자 검증
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

        await _broadcaster.SendToClientAsync(targetClient.Id, message);
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