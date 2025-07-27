using WebSocketChatServer1.Data;
using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace WebSocketChatServer1.Services;

public class EfCoreCommandLogger : ICommandLogger
{
    private readonly ChatDbContext _context;
    private readonly ILogger<EfCoreCommandLogger> _logger;

    public EfCoreCommandLogger(ChatDbContext context, ILogger<EfCoreCommandLogger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null)
    {
        try
        {
            var commandLog = new CommandLog
            {
                ClientId = clientId,
                Username = username,
                CommandType = commandType,
                Parameters = parameters,
                Success = success,
                ExecutionTimeMs = executionTimeMs,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            _context.CommandLogs.Add(commandLog);
            await _context.SaveChangesAsync();

            _logger.LogDebug($"Command logged to database: {commandType} by {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log command {commandType} to database for user {username}");
        }
    }

    public Task LogCommandAsync(string clientId, string? username, string commandType, object? parameters, bool success, double executionTimeMs, string? errorMessage = null, object? responseData = null)
    {
        throw new NotImplementedException();
    }

    public Task LogSystemMetricsAsync(int activeConnections, int activeRooms, long totalMessages, long totalFiles, long totalErrors)
    {
        throw new NotImplementedException();
    }
}

public interface IUserActivityService
{
    Task LogUserConnectedAsync(string clientId, string username);
    Task LogUserDisconnectedAsync(string clientId, string username);
    Task LogUsernameChangedAsync(string clientId, string oldUsername, string newUsername);
    Task UpdateUserProfileAsync(string clientId, string username);
}

public class UserActivityService : IUserActivityService
{
    private readonly ChatDbContext _context;
    private readonly ILogger<UserActivityService> _logger;

    public UserActivityService(ChatDbContext context, ILogger<UserActivityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogUserConnectedAsync(string clientId, string username)
    {
        try
        {
            // 사용자 활동 로그
            var activity = new UserActivity
            {
                ClientId = clientId,
                Username = username,
                ActivityType = "connected",
                Timestamp = DateTime.UtcNow
            };

            _context.UserActivities.Add(activity);

            // 사용자 프로필 업데이트
            await UpdateUserProfileAsync(clientId, username);

            await _context.SaveChangesAsync();
            _logger.LogDebug($"User connected activity logged: {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log user connected activity: {username}");
        }
    }

    public async Task LogUserDisconnectedAsync(string clientId, string username)
    {
        try
        {
            var activity = new UserActivity
            {
                ClientId = clientId,
                Username = username,
                ActivityType = "disconnected",
                Timestamp = DateTime.UtcNow
            };

            _context.UserActivities.Add(activity);
            await _context.SaveChangesAsync();
            _logger.LogDebug($"User disconnected activity logged: {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log user disconnected activity: {username}");
        }
    }

    public async Task LogUsernameChangedAsync(string clientId, string oldUsername, string newUsername)
    {
        try
        {
            var activity = new UserActivity
            {
                ClientId = clientId,
                Username = newUsername,
                ActivityType = "username_changed",
                PreviousValue = oldUsername,
                NewValue = newUsername,
                Timestamp = DateTime.UtcNow
            };

            _context.UserActivities.Add(activity);
            await _context.SaveChangesAsync();
            _logger.LogDebug($"Username change activity logged: {oldUsername} -> {newUsername}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log username change activity: {oldUsername} -> {newUsername}");
        }
    }

    public async Task UpdateUserProfileAsync(string clientId, string username)
    {
        try
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Username == username);

            if (profile == null)
            {
                profile = new UserProfile
                {
                    Username = username,
                    ClientId = clientId,
                    FirstConnected = DateTime.UtcNow,
                    LastConnected = DateTime.UtcNow,
                    TotalConnections = 1
                };
                _context.UserProfiles.Add(profile);
            }
            else
            {
                profile.LastConnected = DateTime.UtcNow;
                profile.TotalConnections++;
                profile.ClientId = clientId; // 최신 클라이언트 ID로 업데이트
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update user profile: {username}");
        }
    }
}

public interface IRoomActivityService
{
    Task LogRoomCreatedAsync(string roomId, string roomName, string createdBy);
    Task LogRoomDeletedAsync(string roomId, string roomName, string deletedBy);
    Task LogUserJoinedRoomAsync(string roomId, string roomName, string username);
    Task LogUserLeftRoomAsync(string roomId, string roomName, string username);
    Task LogRoomMessageSentAsync(string roomId, string roomName, string username, string message);
}

public class RoomActivityService : IRoomActivityService
{
    private readonly ChatDbContext _context;
    private readonly ILogger<RoomActivityService> _logger;

    public RoomActivityService(ChatDbContext context, ILogger<RoomActivityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogRoomCreatedAsync(string roomId, string roomName, string createdBy)
    {
        try
        {
            var activity = new RoomActivity
            {
                RoomId = roomId,
                RoomName = roomName,
                ActivityType = "created",
                Username = createdBy,
                AdditionalData = JsonSerializer.Serialize(new { CreatedBy = createdBy }),
                Timestamp = DateTime.UtcNow
            };

            _context.RoomActivities.Add(activity);
            await _context.SaveChangesAsync();
            _logger.LogDebug($"Room created activity logged: {roomName} by {createdBy}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log room created activity: {roomName}");
        }
    }

    public async Task LogRoomDeletedAsync(string roomId, string roomName, string deletedBy)
    {
        try
        {
            var activity = new RoomActivity
            {
                RoomId = roomId,
                RoomName = roomName,
                ActivityType = "deleted",
                Username = deletedBy,
                AdditionalData = JsonSerializer.Serialize(new { DeletedBy = deletedBy }),
                Timestamp = DateTime.UtcNow
            };

            _context.RoomActivities.Add(activity);
            await _context.SaveChangesAsync();
            _logger.LogDebug($"Room deleted activity logged: {roomName} by {deletedBy}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log room deleted activity: {roomName}");
        }
    }

    public async Task LogUserJoinedRoomAsync(string roomId, string roomName, string username)
    {
        try
        {
            var activity = new RoomActivity
            {
                RoomId = roomId,
                RoomName = roomName,
                ActivityType = "user_joined",
                Username = username,
                Timestamp = DateTime.UtcNow
            };

            _context.RoomActivities.Add(activity);

            // 사용자 프로필의 참여한 룸 목록 업데이트
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Username == username);
            if (userProfile != null && !userProfile.JoinedRooms.Contains(roomId))
            {
                userProfile.JoinedRooms.Add(roomId);
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug($"User joined room activity logged: {username} joined {roomName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log user joined room activity: {username} -> {roomName}");
        }
    }

    public async Task LogUserLeftRoomAsync(string roomId, string roomName, string username)
    {
        try
        {
            var activity = new RoomActivity
            {
                RoomId = roomId,
                RoomName = roomName,
                ActivityType = "user_left",
                Username = username,
                Timestamp = DateTime.UtcNow
            };

            _context.RoomActivities.Add(activity);

            // 사용자 프로필의 참여한 룸 목록에서 제거
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Username == username);
            if (userProfile != null && userProfile.JoinedRooms.Contains(roomId))
            {
                userProfile.JoinedRooms.Remove(roomId);
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug($"User left room activity logged: {username} left {roomName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log user left room activity: {username} -> {roomName}");
        }
    }

    public async Task LogRoomMessageSentAsync(string roomId, string roomName, string username, string message)
    {
        try
        {
            var activity = new RoomActivity
            {
                RoomId = roomId,
                RoomName = roomName,
                ActivityType = "message_sent",
                Username = username,
                AdditionalData = JsonSerializer.Serialize(new { Message = message, MessageLength = message.Length }),
                Timestamp = DateTime.UtcNow
            };

            _context.RoomActivities.Add(activity);

            // 사용자 프로필의 메시지 수 증가
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Username == username);
            if (userProfile != null)
            {
                userProfile.TotalMessagessent++;
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug($"Room message activity logged: {username} sent message in {roomName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to log room message activity: {username} -> {roomName}");
        }
    }
}
