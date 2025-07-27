using System;

namespace WebSocketChatShared.Models;

public class RoomActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string ActivityType { get; set; } = ""; // "created", "deleted", "user_joined", "user_left", "message_sent"
    public string Username { get; set; } = "";
    public string? AdditionalData { get; set; } // JSON ������ �߰� ������
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}