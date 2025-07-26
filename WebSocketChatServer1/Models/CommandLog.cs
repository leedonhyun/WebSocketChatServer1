using System;

namespace ChatSystem.Models;

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