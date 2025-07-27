using System;

namespace WebSocketChatShared.Models;

// Entity Framework Core ��ƼƼ��
public class UserActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string ActivityType { get; set; } = ""; // "connected", "disconnected", "username_changed", etc.
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? AdditionalData { get; set; } // JSON ������ �߰� ������
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}