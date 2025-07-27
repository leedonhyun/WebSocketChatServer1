using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;

using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Telemetry;


namespace WebSocketChatServer1.Services;

public class MessageBroadcaster : IMessageBroadcaster
{
    private readonly IClientManager _clientManager;
    private readonly ConcurrentDictionary<string, IClientConnection> _connections = new();
    private readonly ILogger<MessageBroadcaster> _logger;
    private readonly ITelemetryService _telemetry;
    private readonly IRoomManager _roomManager;
    public MessageBroadcaster(IClientManager clientManager, IRoomManager roomManager, ILogger<MessageBroadcaster> logger, ITelemetryService telemetry)
    {
        _clientManager = clientManager;
        _roomManager = roomManager;
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

    public async Task BroadcastAsync<T>(T message, string? excludeClientId = null, CancellationToken cancellationToken = default) where T : BaseMessage
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
                        await connection.SendAsync(message, cancellationToken);
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

    public async Task SendToClientAsync<T>(string clientId, T message, CancellationToken cancellationToken) where T : BaseMessage
    {
        if (_connections.TryGetValue(clientId, out var connection))
        {
            try
            {
                await connection.SendAsync(message, cancellationToken);
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

    public async Task SendToUsernameAsync<T>(string username, T message, CancellationToken cancellationToken) where T : BaseMessage
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
            await SendToClientAsync(targetClient.Id, message, cancellationToken);
        }
        else
        {
            _logger.LogWarning($"Client with username '{username}' not found");
        }
    }

    public async Task SendToRoomAsync<T>(string roomId, T message, string? excludeUsername = null, CancellationToken cancellationToken = default) where T : BaseMessage
    {
        var members = await _roomManager.GetRoomMembersAsync(roomId);
        if (members == null || !members.Any())
        {
            _logger.LogWarning($"Attempted to send message to empty or non-existent room: {roomId}");
            return;
        }

        var allClients = await _clientManager.GetAllClientsAsync();
        var memberClients = allClients
            .Where(c => members.Contains(c.Username) && c.Username != excludeUsername)
            .ToList();

        var tasks = memberClients.Select(client => SendToClientAsync(client.Id, message, cancellationToken));
        await Task.WhenAll(tasks);


        _logger.LogInformation($"Message sent to room {roomId} with {memberClients.Count} members.");
    }

    public async Task SendToClientAsync<T>(IEnumerable<string>? clientIds, T message, CancellationToken cancellationToken = default) where T : BaseMessage
    {
        if (clientIds == null || !clientIds.Any()){

            _logger.LogWarning("No client IDs provided for sending message.");
            return;
        }
        var tasks = clientIds.Select(async clientId => await SendToClientAsync(clientId, message, cancellationToken));
        await Task.WhenAll(tasks);
    }
}
