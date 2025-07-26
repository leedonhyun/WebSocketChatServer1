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