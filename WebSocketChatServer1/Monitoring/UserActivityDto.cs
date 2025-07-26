using System;
using System.Collections.Generic;

namespace ChatSystem.Monitoring;

public class UserActivityDto
{
    public string Username { get; set; } = string.Empty;
    public long CommandCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> TopCommands { get; set; } = new();
}