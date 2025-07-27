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
    private const string AllClientsSetKey = "clients:all"; // ��� Ŭ���̾�Ʈ ID�� �����ϴ� Redis Set
    private const string ClientDetailKeyPrefix = "client_details:"; // �� Ŭ���̾�Ʈ�� �� ���� ���� �����Ƚ�

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

            // Ŭ���̾�Ʈ �� ���� ����
            await _redisDatabase.StringSetAsync(clientDetailKey, clientJson);
            // ��ü Ŭ���̾�Ʈ ID ��Ͽ� �߰�
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

            // Ŭ���̾�Ʈ �� ���� ����
            var removedDetails = await _redisDatabase.KeyDeleteAsync(clientDetailKey);
            // ��ü Ŭ���̾�Ʈ ID ��Ͽ��� ����
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
                    // ������ ����ġ �߻� ��, Set���� �ش� ID�� �����ϴ� ���� �߰� ����
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
                await AddClientAsync(clientId, client); // ������Ʈ�� ������ �ٽ� ���� (Set�� ����)

                activity?.SetTag("chat.client.old_username", oldUserName);
                _logger.LogInformation($"Client {clientId} username changed: {oldUserName} �� {newUsername} in distributed manager.");
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