using System;
using System.Collections.Generic;

namespace WebSocketChatServer1.Monitoring;

public class SystemStatusDto
{
    public DateTime Timestamp { get; set; }
    public int CurrentActiveConnections { get; set; }
    public int CurrentActiveGroups { get; set; }
    public long TotalCommandsToday { get; set; }
    public long TotalErrorsToday { get; set; }
    public double ErrorRate { get; set; }
    public List<CommandStatsDto> TopCommands { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
}