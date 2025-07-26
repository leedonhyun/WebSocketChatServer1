using ChatSystem.Interfaces;
using ChatSystem.Models;
using ChatSystem.Telemetry;

using MongoDB.Bson;
using MongoDB.Driver;

using Nerdbank.Streams;

using StackExchange.Redis;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ChatSystem.Services;

public class GroupManager : IGroupManager
{
    private readonly ConcurrentDictionary<string, Group> _groups = new();
    private readonly ILogger<GroupManager> _logger;
    private readonly ITelemetryService _telemetry;

    public GroupManager(ILogger<GroupManager> logger, ITelemetryService telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<string> CreateGroupAsync(string groupName, string createdBy)
    {
        using var activity = ChatTelemetry.StartGroupOperationActivity("create", "");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var groupId = Guid.NewGuid().ToString();
            var group = new Group
            {
                Id = groupId,
                Name = groupName,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Members = new HashSet<string> { createdBy }
            };

            _groups[groupId] = group;
            activity?.SetTag("chat.group.id", groupId);
            activity?.SetTag("chat.group.name", groupName);
            activity?.SetTag("chat.group.created_by", createdBy);

            _telemetry.RecordGroupCreated("group");
            _telemetry.UpdateActiveGroups(_groups.Count);

            _logger.LogInformation($"Group '{groupName}' (ID: {groupId}) created by {createdBy}");

            return await Task.FromResult(groupId);
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<bool> AddMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            var added = group.Members.Add(username);
            if (added)
            {
                _logger.LogInformation($"User {username} added to group {group.Name} (ID: {groupId})");
            }
            return await Task.FromResult(added);
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> RemoveMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            var removed = group.Members.Remove(username);
            if (removed)
            {
                _logger.LogInformation($"User {username} removed from group {group.Name} (ID: {groupId})");

                // 그룹이 비어있으면 삭제
                if (group.Members.Count == 0)
                {
                    _groups.TryRemove(groupId, out _);
                    _logger.LogInformation($"Group {group.Name} (ID: {groupId}) deleted (no members left)");
                }
            }
            return await Task.FromResult(removed);
        }
        return await Task.FromResult(false);
    }

    public async Task<Group?> GetGroupAsync(string groupId)
    {
        _groups.TryGetValue(groupId, out var group);
        return await Task.FromResult(group);
    }

    public async Task<IEnumerable<Group>> GetGroupsByUserAsync(string username)
    {
        var userGroups = _groups.Values.Where(g => g.Members.Contains(username)).ToList();
        return await Task.FromResult(userGroups);
    }

    public async Task<IEnumerable<Group>> GetAllGroupsAsync()
    {
        var allGroups = _groups.Values.ToList();
        return await Task.FromResult(allGroups);
    }

    public async Task<bool> IsGroupMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            return await Task.FromResult(group.Members.Contains(username));
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> DeleteGroupAsync(string groupId)
    {
        var removed = _groups.TryRemove(groupId, out var group);
        if (removed && group != null)
        {
            _logger.LogInformation($"Group {group.Name} (ID: {groupId}) deleted");
        }
        return await Task.FromResult(removed);
    }

    public async Task<IEnumerable<string>> GetGroupMembersAsync(string groupId)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            return await Task.FromResult(group.Members.ToList());
        }
        return await Task.FromResult(Enumerable.Empty<string>());
    }
}
public class ClientManager : IClientManager
{
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly ILogger<ClientManager> _logger;
    private readonly ITelemetryService _telemetry;

    public ClientManager(ILogger<ClientManager> logger, ITelemetryService telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task AddClientAsync(string clientId, Client client)
    {
        using var activity = ChatTelemetry.StartActivity("ClientManager.AddClientAsync");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.client.username", client.Username);

        _clients[clientId] = client;
        _telemetry.IncrementActiveUsers();
        _telemetry.UpdateActiveConnections(_clients.Count);

        _logger.LogInformation($"Client {clientId} ({client.Username}) added");
        await Task.CompletedTask;
    }

    public async Task RemoveClientAsync(string clientId)
    {
        using var activity = ChatTelemetry.StartActivity("ClientManager.RemoveClientAsync");
        activity?.SetTag("chat.client.id", clientId);

        if (_clients.TryRemove(clientId, out var client))
        {
            _telemetry.DecrementActiveUsers();
            _telemetry.UpdateActiveConnections(_clients.Count);

            activity?.SetTag("chat.client.username", client.Username);
            _logger.LogInformation($"Client {clientId} ({client.Username}) removed");
        }
        await Task.CompletedTask;
    }

    public async Task<Client?> GetClientAsync(string clientId)
    {
        using var activity = ChatTelemetry.StartActivity("ClientManager.GetClientAsync");
        activity?.SetTag("chat.client.id", clientId);

        _clients.TryGetValue(clientId, out var client);
        activity?.SetTag("chat.client.found", client != null);

        return await Task.FromResult(client);
    }

    public async Task<IEnumerable<Client>> GetAllClientsAsync()
    {
        using var activity = ChatTelemetry.StartActivity("ClientManager.GetAllClientsAsync");
        var clients = _clients.Values.ToList();
        activity?.SetTag("chat.clients.count", clients.Count);

        return await Task.FromResult(clients);
    }

    public async Task UpdateClientUsernameAsync(string clientId, string newUsername)
    {
        using var activity = ChatTelemetry.StartActivity("ClientManager.UpdateClientUsernameAsync");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.client.new_username", newUsername);

        if (_clients.TryGetValue(clientId, out var client))
        {
            var oldUsername = client.Username;
            client.Username = newUsername;

            activity?.SetTag("chat.client.old_username", oldUsername);
            _logger.LogInformation($"Client {clientId} username changed: {oldUsername} → {newUsername}");
        }
        else
        {
            activity?.SetTag("chat.client.found", false);
        }

        await Task.CompletedTask;
    }
}

public class DistributedClientManager : IClientManager
{
    private readonly IDatabase _redisDatabase;
    private readonly ILogger<DistributedClientManager> _logger;
    private readonly ITelemetryService _telemetry;

    // Redis Keys
    private const string AllClientsSetKey = "clients:all"; // 모든 클라이언트 ID를 저장하는 Redis Set
    private const string ClientDetailKeyPrefix = "client_details:"; // 각 클라이언트의 상세 정보 저장 프리픽스

    public DistributedClientManager(
        IConnectionMultiplexer redis,
        ILogger<DistributedClientManager> logger,
        ITelemetryService telemetry)
    {
        _redisDatabase = redis.GetDatabase();
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task AddClientAsync(string clientId, Client client)
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.AddClient");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.client.username", client.Username);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var clientDetailKey = $"{ClientDetailKeyPrefix}{clientId}";
            var clientJson = JsonSerializer.Serialize(client);

            // 클라이언트 상세 정보 저장
            await _redisDatabase.StringSetAsync(clientDetailKey, clientJson);
            // 전체 클라이언트 ID 목록에 추가
            await _redisDatabase.SetAddAsync(AllClientsSetKey, clientId);

            _telemetry.IncrementClientConnections();

            _logger.LogInformation($"Client {clientId} ({client.Username}) added to distributed manager.");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to add client {ClientId} to distributed manager", clientId);
            throw;
        }
    }

    public async Task RemoveClientAsync(string clientId)
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.RemoveClient");
        activity?.SetTag("chat.client.id", clientId);

        try
        {
            var clientDetailKey = $"{ClientDetailKeyPrefix}{clientId}";

            // 클라이언트 상세 정보 삭제
            var removedDetails = await _redisDatabase.KeyDeleteAsync(clientDetailKey);
            // 전체 클라이언트 ID 목록에서 제거
            var removedFromSet = await _redisDatabase.SetRemoveAsync(AllClientsSetKey, clientId);

            if (removedDetails || removedFromSet)
            {
                _telemetry.DecrementClientConnections();
                _logger.LogInformation($"Client {clientId} removed from distributed manager.");
            }
            else
            {
                _logger.LogWarning($"Client {clientId} not found in distributed manager for removal.");
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to remove client {ClientId} from distributed manager", clientId);
            throw;
        }
    }

    public async Task<Client?> GetClientAsync(string clientId)
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.GetClient");
        activity?.SetTag("chat.client.id", clientId);

        try
        {
            var clientDetailKey = $"{ClientDetailKeyPrefix}{clientId}";
            var clientJson = await _redisDatabase.StringGetAsync(clientDetailKey);

            if (clientJson.IsNullOrEmpty)
            {
                _logger.LogDebug($"Client {clientId} not found in distributed manager.");
                return null;
            }

            var client = JsonSerializer.Deserialize<Client>(clientJson!);
            activity?.SetTag("chat.client.username", client?.Username ?? "unknown");
            return client;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to get client {ClientId} from distributed manager", clientId);
            throw;
        }
    }

    public async Task<IEnumerable<Client>> GetAllClientsAsync()
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.GetAllClients");

        try
        {
            var clientIds = await _redisDatabase.SetMembersAsync(AllClientsSetKey);
            var clients = new List<Client>();

            foreach (var clientId in clientIds)
            {
                var client = await GetClientAsync(clientId!);
                if (client != null)
                {
                    clients.Add(client);
                }
                else
                {
                    // 데이터 불일치 발생 시, Set에서 해당 ID를 제거하는 로직 추가 가능
                    _logger.LogWarning($"Client ID {clientId} found in set but details not found. Removing from set.");
                    await _redisDatabase.SetRemoveAsync(AllClientsSetKey, clientId);
                }
            }

            activity?.SetTag("chat.clients.count", clients.Count);
            _telemetry.UpdateActiveConnections(clients.Count);

            _logger.LogDebug($"Retrieved {clients.Count} clients from distributed manager.");
            return clients;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to get all clients from distributed manager");
            throw;
        }
    }

    public async Task UpdateClientUsernameAsync(string clientId, string newUsername)
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.UpdateClientUsername");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.client.new_username", newUsername);

        try
        {
            var client = await GetClientAsync(clientId);
            if (client != null)
            {
                var oldUsername = client.Username;
                client.Username = newUsername;
                await AddClientAsync(clientId, client); // 업데이트된 정보로 다시 저장 (Set은 유지)

                activity?.SetTag("chat.client.old_username", oldUsername);
                _logger.LogInformation($"Client {clientId} username changed: {oldUsername} → {newUsername} in distributed manager.");
            }
            else
            {
                _logger.LogWarning($"Could not update username for unknown client: {clientId}");
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to update username for client {ClientId}", clientId);
            throw;
        }
    }
}
public class FileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<FileStorageService> _logger;
    private readonly ITelemetryService _telemetry;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger, ITelemetryService telemetry)
    {
        _storagePath = configuration.GetValue<string>("FileStorage:Path") ??
                      Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _logger = logger;
        _telemetry = telemetry;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> SaveFileAsync(string fileId, string fileName, byte[] data, bool append = false)
    {
        using var activity = ChatTelemetry.StartFileOperationActivity("SaveFile", fileName, data.Length);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(_storagePath, $"{fileId}_{sanitizedFileName}");

            activity?.SetTag("chat.file.path", filePath);
            activity?.SetTag("chat.file.append", append);

            var mode = append ? FileMode.Append : FileMode.Create;
            using var fileStream = new FileStream(filePath, mode, FileAccess.Write);
            await fileStream.WriteAsync(data);
            await fileStream.FlushAsync();

            stopwatch.Stop();
            _telemetry.RecordFileOperation("save", stopwatch.Elapsed.TotalMilliseconds, data.Length);
            _telemetry.IncrementFileUploads();

            _logger.LogDebug($"File saved: {filePath} ({data.Length} bytes)");
            return filePath;
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_save_error", ex.Message);
            _logger.LogError(ex, $"Error saving file: {fileName}");
            throw;
        }
    }

    public async Task<byte[]> ReadFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("ReadFile", Path.GetFileName(filePath), fileSize);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var data = await File.ReadAllBytesAsync(filePath);

            stopwatch.Stop();
            _telemetry.RecordFileOperation("read", stopwatch.Elapsed.TotalMilliseconds, data.Length);

            activity?.SetTag("chat.file.size", data.Length);
            return data;
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_read_error", ex.Message);
            _logger.LogError(ex, $"Error reading file: {filePath}");
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("FileExists", Path.GetFileName(filePath), fileSize);
        var exists = await Task.FromResult(File.Exists(filePath));
        activity?.SetTag("chat.file.exists", exists);
        return exists;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("DeleteFile", Path.GetFileName(filePath), fileSize);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                stopwatch.Stop();
                _telemetry.RecordFileOperation("delete", stopwatch.Elapsed.TotalMilliseconds, 0);
                _logger.LogDebug($"File deleted: {filePath}");
            }
            else
            {
                activity?.SetTag("chat.file.found", false);
            }
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_delete_error", ex.Message);
            _logger.LogError(ex, $"Error deleting file: {filePath}");
            throw;
        }
        await Task.CompletedTask;
    }

    public string GetFilePath(string fileId, string fileName)
    {
        var sanitizedFileName = SanitizeFileName(fileName);
        return Path.Combine(_storagePath, $"{fileId}_{sanitizedFileName}");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
    }
}

public class MessageBroadcaster : IMessageBroadcaster
{
    private readonly IClientManager _clientManager;
    private readonly ConcurrentDictionary<string, IClientConnection> _connections = new();
    private readonly ILogger<MessageBroadcaster> _logger;
    private readonly ITelemetryService _telemetry;

    public MessageBroadcaster(IClientManager clientManager, ILogger<MessageBroadcaster> logger, ITelemetryService telemetry)
    {
        _clientManager = clientManager;
        _logger = logger;
        _telemetry = telemetry;
    }

    public void RegisterConnection(string clientId, IClientConnection connection)
    {
        _connections[clientId] = connection;
        _telemetry.IncrementClientConnections();
        _telemetry.UpdateActiveConnections(_connections.Count);
        _logger.LogDebug($"Connection registered for client: {clientId}");
    }

    public void UnregisterConnection(string clientId)
    {
        _connections.TryRemove(clientId, out _);
        _telemetry.DecrementClientConnections();
        _telemetry.UpdateActiveConnections(_connections.Count);
        _logger.LogDebug($"Connection unregistered for client: {clientId}");
    }

    public async Task BroadcastAsync<T>(T message, string? excludeClientId = null) where T : BaseMessage
    {
        using var activity = ChatTelemetry.StartBroadcastActivity(_connections.Count, message.Type);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tasks = new List<Task>();
            var clientCount = 0;

            foreach (var kvp in _connections.ToList())
            {
                if (kvp.Key == excludeClientId)
                    continue;

                clientCount++;
                var clientId = kvp.Key;
                var connection = kvp.Value;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await connection.SendAsync(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error broadcasting to client {clientId}");
                        _connections.TryRemove(clientId, out _);
                        _telemetry.RecordError("broadcast_error", ex.Message);
                    }
                }));
            }

            activity?.SetTag("chat.broadcast.actual_recipients", clientCount);
            _logger.LogDebug($"Broadcasting message to {clientCount} clients");

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            stopwatch.Stop();
            _telemetry.RecordMessageProcessed($"broadcast_{message.Type}", stopwatch.Elapsed.TotalMilliseconds, 0);
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            throw;
        }
    }

    public async Task SendToClientAsync<T>(string clientId, T message) where T : BaseMessage
    {
        if (_connections.TryGetValue(clientId, out var connection))
        {
            try
            {
                await connection.SendAsync(message);
                _logger.LogDebug($"Message sent to client: {clientId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message to client {clientId}");
                _connections.TryRemove(clientId, out _);
            }
        }
        else
        {
            _logger.LogWarning($"Client connection not found: {clientId}");
        }
    }

    public async Task SendToUsernameAsync<T>(string username, T message) where T : BaseMessage
    {
        // ROOM: 접두사가 있는 username은 무시
        if (username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug($"Skipping message to room identifier: {username}");
            return;
        }

        var clients = await _clientManager.GetAllClientsAsync();
        var targetClient = clients.FirstOrDefault(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
            && !c.Username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase));

        if (targetClient != null)
        {
            await SendToClientAsync(targetClient.Id, message);
        }
        else
        {
            _logger.LogWarning($"Client with username '{username}' not found");
        }
    }
}

public class RedisMessageBroadcaster : IMessageBroadcaster
{
    private readonly ISubscriber _redisSubscriber;
    private readonly IDatabase _redisDatabase; // 클라이언트-서버 매핑을 위한 Redis DB
    private readonly ConcurrentDictionary<string, IClientConnection> _localConnections = new();
    private readonly IClientManager _clientManager; // 클라이언트 정보 조회를 위함 (분산 버전)
    private readonly ILogger<RedisMessageBroadcaster> _logger;
    private readonly ITelemetryService _telemetry;

    // Redis Pub/Sub 채널 이름
    private const string ChatChannel = "chat_messages";
    private const string PrivateMessageChannelPrefix = "private_message:"; // 개인 메시지 채널 프리픽스

    public RedisMessageBroadcaster(
        IConnectionMultiplexer redis,
        IClientManager clientManager, // DistributedClientManager 주입
        ILogger<RedisMessageBroadcaster> logger,
        ITelemetryService telemetry)
    {
        _redisSubscriber = redis.GetSubscriber();
        _redisDatabase = redis.GetDatabase();
        _clientManager = clientManager;
        _logger = logger;
        _telemetry = telemetry;

        // 모든 RedisMessageBroadcaster 인스턴스는 'ChatChannel'을 구독하여 전체 브로드캐스트 메시지를 수신
        _redisSubscriber.Subscribe(RedisChannel.Literal(ChatChannel), async (channel, messageJson) =>
        {
            await HandleReceivedBroadcastMessage(messageJson.ToString());
        });

        // 각 서버 인스턴스 고유의 개인 메시지 채널을 구독
        // 이 서버 인스턴스에 연결된 특정 클라이언트에게 보낼 메시지가 여기에 들어옴.
        _redisSubscriber.Subscribe(RedisChannel.Literal($"{PrivateMessageChannelPrefix}{Environment.MachineName}"), async (channel, messageJson) =>
        {
            await HandleReceivedPrivateMessage(messageJson.ToString());
        });
    }

    // 현재 서버 인스턴스에 연결된 클라이언트만 관리
    public void RegisterConnection(string clientId, IClientConnection connection)
    {
        using var activity = ChatTelemetry.StartActivity("RedisMessageBroadcaster.RegisterConnection");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.server.instance", Environment.MachineName);

        try
        {
            _localConnections[clientId] = connection;
            _telemetry.IncrementClientConnections();

            _logger.LogInformation($"Local connection registered: {clientId}");
            // 클라이언트가 현재 서버에 연결되었음을 Redis에 저장
            // Key: client_location:{clientId}, Value: 현재 서버 인스턴스 이름 (예: Environment.MachineName)
            _redisDatabase.StringSetAsync($"client_location:{clientId}", Environment.MachineName).Wait();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to register connection for client {ClientId}", clientId);
            throw;
        }
    }

    public void UnregisterConnection(string clientId)
    {
        using var activity = ChatTelemetry.StartActivity("RedisMessageBroadcaster.UnregisterConnection");
        activity?.SetTag("chat.client.id", clientId);

        try
        {
            _localConnections.TryRemove(clientId, out _);
            _telemetry.DecrementClientConnections();

            _logger.LogInformation($"Local connection unregistered: {clientId}");
            // 클라이언트가 연결 해제되었으므로 Redis에서 위치 정보 제거
            _redisDatabase.KeyDeleteAsync($"client_location:{clientId}").Wait();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to unregister connection for client {ClientId}", clientId);
        }
    }

    // 전체 브로드캐스트 메시지
    public async Task BroadcastAsync<T>(T message, string? excludeClientId = null) where T : BaseMessage
    {
        using var activity = ChatTelemetry.StartActivity("RedisMessageBroadcaster.Broadcast");
        activity?.SetTag("chat.message.type", message.Type);
        activity?.SetTag("chat.message.exclude_client", excludeClientId ?? "none");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageSize = Encoding.UTF8.GetByteCount(messageJson);

            // 1. 현재 서버 인스턴스에 연결된 클라이언트에게 직접 전송
            var localSendTasks = new List<Task>();
            var localClientCount = 0;

            foreach (var kvp in _localConnections.ToList())
            {
                if (kvp.Key == excludeClientId)
                    continue;

                // 연결이 유효한지 확인 후 전송
                if (kvp.Value.IsConnected)
                {
                    localSendTasks.Add(kvp.Value.SendAsync(message));
                    localClientCount++;
                }
                else
                {
                    // 연결이 끊어진 경우 제거
                    UnregisterConnection(kvp.Key);
                }
            }
            await Task.WhenAll(localSendTasks);

            // 2. Redis Pub/Sub을 통해 다른 서버 인스턴스에게 메시지 발행
            _logger.LogDebug($"Publishing broadcast message to Redis: {message.Type}");
            await _redisSubscriber.PublishAsync(RedisChannel.Literal(ChatChannel), messageJson);

            activity?.SetTag("chat.local_clients_sent", localClientCount);
            activity?.SetTag("chat.message.size_bytes", messageSize);

            _telemetry.RecordMessageProcessed(message.Type, stopwatch.Elapsed.TotalMilliseconds, messageSize);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetry.RecordError("broadcast_message", ex.Message);
            _logger.LogError(ex, "Failed to broadcast message of type {MessageType}", message.Type);
            throw;
        }
    }

    // 특정 클라이언트 ID에게 메시지 전송
    public async Task SendToClientAsync<T>(string clientId, T message) where T : BaseMessage
    {
        using var activity = ChatTelemetry.StartActivity("RedisMessageBroadcaster.SendToClient");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.message.type", message.Type);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageSize = Encoding.UTF8.GetByteCount(messageJson);

            // 1. 현재 서버 인스턴스에 클라이언트가 연결되어 있는지 확인
            if (_localConnections.TryGetValue(clientId, out var connection) && connection.IsConnected)
            {
                _logger.LogDebug($"Sending message directly to local client: {clientId}");
                await connection.SendAsync(message);
                activity?.SetTag("chat.delivery.method", "local");
            }
            else
            {
                // 2. 클라이언트가 다른 서버 인스턴스에 연결되어 있을 가능성 확인 (Redis에서 조회)
                var serverInstanceName = await _redisDatabase.StringGetAsync($"client_location:{clientId}");
                if (!serverInstanceName.IsNullOrEmpty)
                {
                    _logger.LogDebug($"Client {clientId} found on instance: {serverInstanceName}. Sending via Redis.");
                    // 해당 서버 인스턴스 고유의 개인 메시지 채널로 메시지 발행
                    // 개인 메시지는 클라이언트 ID도 함께 포함하여, 수신 서버에서 해당 클라이언트에게만 전달하도록 함.
                    await _redisSubscriber.PublishAsync(
                        RedisChannel.Literal($"{PrivateMessageChannelPrefix}{serverInstanceName}"),
                        JsonSerializer.Serialize(new { ClientId = clientId, Message = messageJson }));
                    activity?.SetTag("chat.delivery.method", "redis");
                    activity?.SetTag("chat.target.server", serverInstanceName.ToString());
                }
                else
                {
                    _logger.LogWarning($"Client {clientId} not found on any active instance.");
                    activity?.SetTag("chat.delivery.method", "failed");
                }
            }

            activity?.SetTag("chat.message.size_bytes", messageSize);
            _telemetry.RecordMessageProcessed(message.Type, stopwatch.Elapsed.TotalMilliseconds, messageSize);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetry.RecordError("send_to_client", ex.Message);
            _logger.LogError(ex, "Failed to send message to client {ClientId}", clientId);
            throw;
        }
    }

    // 특정 사용자 이름에게 메시지 전송
    public async Task SendToUsernameAsync<T>(string username, T message) where T : BaseMessage
    {
        // ROOM: 접두사가 있는 username은 무시
        if (username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug($"Skipping message to room identifier: {username}");
            return;
        }

        // 클라이언트 매니저 (분산 버전)를 통해 사용자 이름으로 클라이언트 ID 조회
        var clients = await _clientManager.GetAllClientsAsync();
        var targetClient = clients.FirstOrDefault(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
            && !c.Username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase));

        if (targetClient != null)
        {
            await SendToClientAsync(targetClient.Id, message);
        }
        else
        {
            _logger.LogWarning($"Client with username '{username}' not found for sending message.");
        }
    }

    // Redis Pub/Sub으로 전체 브로드캐스트 메시지 수신 시 처리
    private async Task HandleReceivedBroadcastMessage(string messageJson)
    {
        try
        {
            // 메시지 타입을 먼저 확인
            using var document = JsonDocument.Parse(messageJson);
            var messageType = document.RootElement.GetProperty("Type").GetString();

            BaseMessage? message = null;

            // 메시지 타입에 따라 적절한 구체 타입으로 역직렬화
            switch (messageType)
            {
                case "ChatMessage":
                    message = JsonSerializer.Deserialize<ChatMessage>(messageJson);
                    break;
                case "FileTransferMessage":
                    message = JsonSerializer.Deserialize<FileTransferMessage>(messageJson);
                    break;
                default:
                    _logger.LogWarning($"Unknown message type received from Redis: {messageType}");
                    return;
            }

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message from Redis");
                return;
            }

            // 현재 서버 인스턴스에 연결된 클라이언트들에게만 전송
            var localSendTasks = new List<Task>();
            foreach (var kvp in _localConnections.ToList())
            {
                // Note: Redis Pub/Sub은 발행자 자신에게도 메시지를 보냅니다.
                // 따라서 이미 BroadcastAsync에서 로컬 클라이언트에게 보냈다면 중복 전송 방지를 위한 로직 필요.
                // 예: 메시지에 원본 서버 ID를 포함하고, 현재 서버 ID와 다를 때만 전송.
                // 이 예시에서는 모든 수신 메시지를 로컬 클라이언트에 다시 전송하는 것으로 단순화.
                if (kvp.Value.IsConnected)
                {
                    localSendTasks.Add(kvp.Value.SendAsync(message));
                }
                else
                {
                    UnregisterConnection(kvp.Key);
                }
            }
            await Task.WhenAll(localSendTasks);
            _logger.LogDebug($"Handled broadcast message from Redis: {messageType}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received broadcast message from Redis");
        }
    }

    // Redis Pub/Sub으로 특정 서버 인스턴스 대상 개인 메시지 수신 시 처리
    private async Task HandleReceivedPrivateMessage(string envelopeJson)
    {
        try
        {
            var envelope = JsonDocument.Parse(envelopeJson);
            var clientId = envelope.RootElement.GetProperty("ClientId").GetString();
            var messageJson = envelope.RootElement.GetProperty("Message").GetString();

            if (clientId == null || messageJson == null)
            {
                _logger.LogError("Received malformed private message envelope from Redis.");
                return;
            }

            // 해당 클라이언트 ID가 현재 서버 인스턴스에 연결되어 있는지 확인
            if (_localConnections.TryGetValue(clientId, out var connection) && connection.IsConnected)
            {
                _logger.LogDebug($"Received private message for local client {clientId} from Redis.");

                // 메시지 타입을 먼저 확인하여 적절한 구체 타입으로 역직렬화
                using var messageDocument = JsonDocument.Parse(messageJson);
                var messageType = messageDocument.RootElement.GetProperty("Type").GetString();

                BaseMessage? message = null;

                switch (messageType)
                {
                    case "ChatMessage":
                        message = JsonSerializer.Deserialize<ChatMessage>(messageJson);
                        break;
                    case "FileTransferMessage":
                        message = JsonSerializer.Deserialize<FileTransferMessage>(messageJson);
                        break;
                    default:
                        _logger.LogWarning($"Unknown message type in private message: {messageType}");
                        return;
                }

                if (message != null)
                {
                    await connection.SendAsync(message);
                }
            }
            else
            {
                _logger.LogWarning($"Received private message for {clientId}, but client not locally connected.");
                // 클라이언트가 이미 연결이 끊어졌거나 다른 서버로 재연결되었을 수 있음.
                // Redis에서 client_location 정보가 업데이트될 때까지 기다려야 함.
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received private message from Redis.");
        }
    }
}



public interface IClientConnection
{
    Task SendAsync<T>(T message) where T : BaseMessage;
    bool IsConnected { get; }
}

public class WebSocketClientConnection : IClientConnection
{
    private readonly MultiplexingStream.Channel _messageChannel;
    private readonly MultiplexingStream.Channel _fileChannel;
    private readonly ILogger<WebSocketClientConnection> _logger;
    private bool _isConnected = true;

    public bool IsConnected => _isConnected;

    public WebSocketClientConnection(
        MultiplexingStream.Channel messageChannel,
        MultiplexingStream.Channel fileChannel,
        ILogger<WebSocketClientConnection> logger)
    {
        _messageChannel = messageChannel;
        _fileChannel = fileChannel;
        _logger = logger;
    }

    public async Task SendAsync<T>(T message) where T : BaseMessage
    {
        try
        {
            var channel = message switch
            {
                ChatMessage => _messageChannel,
                FileTransferMessage => _fileChannel,
                _ => _messageChannel
            };

            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json + "\n");

            await channel.Output.WriteAsync(buffer.AsMemory());
            await channel.Output.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            _isConnected = false;
            throw;
        }
    }
}

public class MongoCommandLogger : ICommandLogger
{
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoCommandLogger> _logger;

    public MongoCommandLogger(IMongoDatabase? database, ILogger<MongoCommandLogger> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogWarning("MongoDB database is not available - command logging skipped");
                return;
            }

            var collection = _database.GetCollection<CommandLog>("command_logs");

            var commandLog = new CommandLog
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ClientId = clientId,
                Username = username,
                CommandType = commandType,
                Parameters = parameters,
                Success = success,
                ExecutionTimeMs = executionTimeMs,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            await collection.InsertOneAsync(commandLog);
            _logger.LogDebug($"Command logged to MongoDB: {commandType} by {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log command {commandType} to MongoDB for user {username}");
        }
    }
}

public class NullCommandLogger : ICommandLogger
{
    private readonly ILogger<NullCommandLogger> _logger;

    public NullCommandLogger(ILogger<NullCommandLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        _logger.LogDebug($"Command logged (no database): {commandType} by {username} - Success: {success}");
        await Task.CompletedTask;
    }
}
