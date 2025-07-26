using System;

namespace WebSocketChatServer1.Models;
public abstract class BaseMessage
{
    public string Type { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}