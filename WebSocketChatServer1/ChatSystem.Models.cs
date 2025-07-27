namespace WebSocketChatClient1.Models;
public abstract class BaseMessage
{
    public string Type { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ChatMessage : BaseMessage
{
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public string ToUsername { get; set; } = ""; // 1:1 채팅용
    public string[] ToUsernames { get; set; } = Array.Empty<string>(); // 1:N 채팅용
    public string ChatType { get; set; } = "public"; // "public", "private", "group"
    public string GroupId { get; set; } = ""; // 그룹 채팅용 ID
}

public class FileTransferInfo
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "";
    public string FromUsername { get; set; } = "";
    public string ToUsername { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class FileTransferMessage : BaseMessage
{
    public string FileId { get; set; } = "";
    public FileTransferInfo? FileInfo { get; set; }
    public byte[]? Data { get; set; }
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string FromUsername { get; set; } = "";
    public string ToUsername { get; set; } = "";
}

public class Client
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public ClientStatus Status { get; set; } = ClientStatus.Connected;
}

public enum ClientStatus
{
    Connected,
    Disconnected,
    Away
}

public class Group
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public HashSet<string> Members { get; set; } = new();
}

// Entity Framework Core 엔티티들
public class UserActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string ActivityType { get; set; } = ""; // "connected", "disconnected", "username_changed", etc.
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? AdditionalData { get; set; } // JSON 형태의 추가 데이터
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CommandLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string CommandType { get; set; } = "";
    public string? Parameters { get; set; }
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public string ClientId { get; set; } = "";
    public DateTime FirstConnected { get; set; } = DateTime.UtcNow;
    public DateTime LastConnected { get; set; } = DateTime.UtcNow;
    public int TotalConnections { get; set; } = 1;
    public int TotalMessagessent { get; set; } = 0;
    public int TotalCommandsExecuted { get; set; } = 0;
    public List<string> JoinedRooms { get; set; } = new();
}

public class RoomActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string ActivityType { get; set; } = ""; // "created", "deleted", "user_joined", "user_left", "message_sent"
    public string Username { get; set; } = "";
    public string? AdditionalData { get; set; } // JSON 형태의 추가 데이터
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
