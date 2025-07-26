using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Telemetry;

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace WebSocketChatServer1.Services;

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
            _logger.LogInformation($"Client {clientId} username changed: {oldUsername} ¡æ {newUsername}");
        }
        else
        {
            activity?.SetTag("chat.client.found", false);
        }

        await Task.CompletedTask;
    }
}