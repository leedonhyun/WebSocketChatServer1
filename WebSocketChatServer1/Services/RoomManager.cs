using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Telemetry;

using WebSocketChatShared.Models;

namespace WebSocketChatServer1.Services;

public class RoomManager : IRoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _clientToRoomMap = new();
    private readonly ILogger<RoomManager> _logger;
    private readonly ITelemetryService _telemetry;

    public RoomManager(ILogger<RoomManager> logger, ITelemetryService telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<string> CreateRoomAsync(string roomName, string createdBy)
    {
        using var activity = ChatTelemetry.StartRoomOperationActivity("create", "");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var roomid = Guid.NewGuid().ToString();
            var room = new Room 
            {
                Id = roomid,
                Name = roomName,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Members = new HashSet<string> { createdBy }
            };

            _rooms[roomid] = room;
            activity?.SetTag("chat.room.id", roomid);
            activity?.SetTag("chat.room.name", roomName);
            activity?.SetTag("chat.room.created_by", createdBy);

            _telemetry.RecordRoomCreated("room");
            _telemetry.UpdateActiveRooms(_rooms.Count);

            _logger.LogInformation($"Room '{roomName}' (ID: {roomid}) created by {createdBy}");

            return await Task.FromResult(roomid);
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
    public Task<bool> AddClientToRoomAsync(string roomId, string clientId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            // Fix: Use TryAdd for ConcurrentDictionary
            if (room.ClientIds.TryAdd(clientId, string.Empty))
            {
                _clientToRoomMap[clientId] = roomId;
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
    public Task<bool> RemoveClientFromRoomAsync(string roomId, string clientId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            if (room.ClientIds.TryRemove(clientId, out _)) // Fix: Use overload that takes key and out value
            {
                _clientToRoomMap.TryRemove(clientId, out _);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
    public Task<IEnumerable<string>> GetClientIdsInRoomAsync(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            // Fix: Select only the keys (client IDs) from the dictionary
            return Task.FromResult<IEnumerable<string>>(room.ClientIds.Keys.ToList());
        }
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public async Task<bool> AddMemberAsync(string roomId, string username)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            var added = room.Members.Add(username);
            if (added)
            {
                _logger.LogInformation($"User {username} added to room {room.Name} (ID: {roomId})");
            }
            return await Task.FromResult(added);
        }
        return await Task.FromResult(false);
    }
    public Task<Room?> GetRoomForClientAsync(string clientId)
    {
        if (_clientToRoomMap.TryGetValue(clientId, out var roomId))
        {
            return GetRoomAsync(roomId);
        }
        return Task.FromResult<Room?>(null);
    }

    public Task<bool> IsClientInRoomAsync(string roomId, string clientId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            return Task.FromResult(room.ClientIds.ContainsKey(clientId));
        }
        return Task.FromResult(false);
    }
    public async Task<bool> RemoveMemberAsync(string roomId, string username)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            var removed = room.Members.Remove(username);
            if (removed)
            {
                _logger.LogInformation($"User {username} removed from room {room.Name} (ID: {roomId})");

                // 그룹이 비어있으면 삭제
                if (room.Members.Count == 0)
                {
                    _rooms.TryRemove(roomId, out _);
                    _logger.LogInformation($"Room {room.Name} (ID: {roomId}) deleted (no members left)");
                }
            }
            return await Task.FromResult(removed);
        }
        return await Task.FromResult(false);
    }

    public async Task<Room?> GetRoomAsync(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return await Task.FromResult(room);
    }

    public async Task<IEnumerable<Room>> GetRoomsByUserAsync(string username)
    {
        var userRooms = _rooms.Values.Where(g => g.Members.Contains(username)).ToList();
        return await Task.FromResult(userRooms);
    }

    public async Task<IEnumerable<Room>> GetAllRoomsAsync()
    {
        var allRooms = _rooms.Values.ToList();
        return await Task.FromResult(allRooms);
    }

    public async Task<bool> IsRoomMemberAsync(string roomId, string username)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            return await Task.FromResult(room.Members.Contains(username));
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> DeleteRoomAsync(string roomId)
    {
        var removed = _rooms.TryRemove(roomId, out var room);
        if (removed && room != null)
        {
            foreach (var clientId in room.ClientIds)
            {
                _clientToRoomMap.TryRemove(clientId);
            }
            _logger.LogInformation($"Room {room.Name} (ID: {roomId}) deleted");
        }
        return await Task.FromResult(removed);
    }

    public async Task<IEnumerable<string>> GetRoomMembersAsync(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            return await Task.FromResult(room.Members.ToList());
        }
        return await Task.FromResult(Enumerable.Empty<string>());
    }
}
