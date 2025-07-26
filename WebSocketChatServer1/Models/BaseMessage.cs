using System;

namespace ChatSystem.Models;
public abstract class BaseMessage
{
    public string Type { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}