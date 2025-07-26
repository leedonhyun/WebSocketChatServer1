using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Telemetry;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Services;

public class RedisMessageBroadcaster : IMessageBroadcaster
{
    private readonly ISubscriber _redisSubscriber;
    private readonly IDatabase _redisDatabase; // Ŭ���̾�Ʈ-���� ������ ���� Redis DB
    private readonly ConcurrentDictionary<string, IClientConnection> _localConnections = new();
    private readonly IClientManager _clientManager; // Ŭ���̾�Ʈ ���� ��ȸ�� ���� (�л� ����)
    private readonly ILogger<RedisMessageBroadcaster> _logger;
    private readonly ITelemetryService _telemetry;

    // Redis Pub/Sub ä�� �̸�
    private const string ChatChannel = "chat_messages";
    private const string PrivateMessageChannelPrefix = "private_message:"; // ���� �޽��� ä�� �����Ƚ�

    public RedisMessageBroadcaster(
        IConnectionMultiplexer redis,
        IClientManager clientManager, // DistributedClientManager ����
        ILogger<RedisMessageBroadcaster> logger,
        ITelemetryService telemetry)
    {
        _redisSubscriber = redis.GetSubscriber();
        _redisDatabase = redis.GetDatabase();
        _clientManager = clientManager;
        _logger = logger;
        _telemetry = telemetry;

        // ��� RedisMessageBroadcaster �ν��Ͻ��� 'ChatChannel'�� �����Ͽ� ��ü ��ε�ĳ��Ʈ �޽����� ����
        _redisSubscriber.Subscribe(RedisChannel.Literal(ChatChannel), async (channel, messageJson) =>
        {
            await HandleReceivedBroadcastMessage(messageJson.ToString());
        });

        // �� ���� �ν��Ͻ� ������ ���� �޽��� ä���� ����
        // �� ���� �ν��Ͻ��� ����� Ư�� Ŭ���̾�Ʈ���� ���� �޽����� ���⿡ ����.
        _redisSubscriber.Subscribe(RedisChannel.Literal($"{PrivateMessageChannelPrefix}{Environment.MachineName}"), async (channel, messageJson) =>
        {
            await HandleReceivedPrivateMessage(messageJson.ToString());
        });
    }

    // ���� ���� �ν��Ͻ��� ����� Ŭ���̾�Ʈ�� ����
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
            // Ŭ���̾�Ʈ�� ���� ������ ����Ǿ����� Redis�� ����
            // Key: client_location:{clientId}, Value: ���� ���� �ν��Ͻ� �̸� (��: Environment.MachineName)
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
            // Ŭ���̾�Ʈ�� ���� �����Ǿ����Ƿ� Redis���� ��ġ ���� ����
            _redisDatabase.KeyDeleteAsync($"client_location:{clientId}").Wait();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to unregister connection for client {ClientId}", clientId);
        }
    }

    // ��ü ��ε�ĳ��Ʈ �޽���
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

            // 1. ���� ���� �ν��Ͻ��� ����� Ŭ���̾�Ʈ���� ���� ����
            var localSendTasks = new List<Task>();
            var localClientCount = 0;

            foreach (var kvp in _localConnections.ToList())
            {
                if (kvp.Key == excludeClientId)
                    continue;

                // ������ ��ȿ���� Ȯ�� �� ����
                if (kvp.Value.IsConnected)
                {
                    localSendTasks.Add(kvp.Value.SendAsync(message));
                    localClientCount++;
                }
                else
                {
                    // ������ ������ ��� ����
                    UnregisterConnection(kvp.Key);
                }
            }
            await Task.WhenAll(localSendTasks);

            // 2. Redis Pub/Sub�� ���� �ٸ� ���� �ν��Ͻ����� �޽��� ����
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

    // Ư�� Ŭ���̾�Ʈ ID���� �޽��� ����
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

            // 1. ���� ���� �ν��Ͻ��� Ŭ���̾�Ʈ�� ����Ǿ� �ִ��� Ȯ��
            if (_localConnections.TryGetValue(clientId, out var connection) && connection.IsConnected)
            {
                _logger.LogDebug($"Sending message directly to local client: {clientId}");
                await connection.SendAsync(message);
                activity?.SetTag("chat.delivery.method", "local");
            }
            else
            {
                // 2. Ŭ���̾�Ʈ�� �ٸ� ���� �ν��Ͻ��� ����Ǿ� ���� ���ɼ� Ȯ�� (Redis���� ��ȸ)
                var serverInstanceName = await _redisDatabase.StringGetAsync($"client_location:{clientId}");
                if (!serverInstanceName.IsNullOrEmpty)
                {
                    _logger.LogDebug($"Client {clientId} found on instance: {serverInstanceName}. Sending via Redis.");
                    // �ش� ���� �ν��Ͻ� ������ ���� �޽��� ä�η� �޽��� ����
                    // ���� �޽����� Ŭ���̾�Ʈ ID�� �Բ� �����Ͽ�, ���� �������� �ش� Ŭ���̾�Ʈ���Ը� �����ϵ��� ��.
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

    // Ư�� ����� �̸����� �޽��� ����
    public async Task SendToUsernameAsync<T>(string username, T message) where T : BaseMessage
    {
        // ROOM: ���λ簡 �ִ� username�� ����
        if (username.StartsWith("ROOM:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug($"Skipping message to room identifier: {username}");
            return;
        }

        // Ŭ���̾�Ʈ �Ŵ��� (�л� ����)�� ���� ����� �̸����� Ŭ���̾�Ʈ ID ��ȸ
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

    // Redis Pub/Sub���� ��ü ��ε�ĳ��Ʈ �޽��� ���� �� ó��
    private async Task HandleReceivedBroadcastMessage(string messageJson)
    {
        try
        {
            // �޽��� Ÿ���� ���� Ȯ��
            using var document = JsonDocument.Parse(messageJson);
            var messageType = document.RootElement.GetProperty("Type").GetString();

            BaseMessage? message = null;

            // �޽��� Ÿ�Կ� ���� ������ ��ü Ÿ������ ������ȭ
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

            // ���� ���� �ν��Ͻ��� ����� Ŭ���̾�Ʈ�鿡�Ը� ����
            var localSendTasks = new List<Task>();
            foreach (var kvp in _localConnections.ToList())
            {
                // Note: Redis Pub/Sub�� ������ �ڽſ��Ե� �޽����� �����ϴ�.
                // ���� �̹� BroadcastAsync���� ���� Ŭ���̾�Ʈ���� ���´ٸ� �ߺ� ���� ������ ���� ���� �ʿ�.
                // ��: �޽����� ���� ���� ID�� �����ϰ�, ���� ���� ID�� �ٸ� ���� ����.
                // �� ���ÿ����� ��� ���� �޽����� ���� Ŭ���̾�Ʈ�� �ٽ� �����ϴ� ������ �ܼ�ȭ.
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

    // Redis Pub/Sub���� Ư�� ���� �ν��Ͻ� ��� ���� �޽��� ���� �� ó��
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

            // �ش� Ŭ���̾�Ʈ ID�� ���� ���� �ν��Ͻ��� ����Ǿ� �ִ��� Ȯ��
            if (_localConnections.TryGetValue(clientId, out var connection) && connection.IsConnected)
            {
                _logger.LogDebug($"Received private message for local client {clientId} from Redis.");

                // �޽��� Ÿ���� ���� Ȯ���Ͽ� ������ ��ü Ÿ������ ������ȭ
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
                // Ŭ���̾�Ʈ�� �̹� ������ �������ų� �ٸ� ������ �翬��Ǿ��� �� ����.
                // Redis���� client_location ������ ������Ʈ�� ������ ��ٷ��� ��.
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received private message from Redis.");
        }
    }
}