using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Telemetry;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using System.Diagnostics;
using System.Text.Json;

namespace WebSocketChatServer1.Services;

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

    public async Task<string> UpdateClientUserNameAsync(string clientId, string newUsername)
    {
        using var activity = ChatTelemetry.StartActivity("DistributedClientManager.UpdateClientUsername");
        activity?.SetTag("chat.client.id", clientId);
        activity?.SetTag("chat.client.new_username", newUsername);

        string oldUserName = string.Empty;
        try
        {

            var client = await GetClientAsync(clientId);
            if (client != null)
            {
                oldUserName = client.Username;
                client.Username = newUsername;
                await AddClientAsync(clientId, client); // 업데이트된 정보로 다시 저장 (Set은 유지)

                activity?.SetTag("chat.client.old_username", oldUserName);
                _logger.LogInformation($"Client {clientId} username changed: {oldUserName} → {newUsername} in distributed manager.");
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
        return oldUserName;
    }
}