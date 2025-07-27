using System;
using System.Collections.Generic;

namespace WebSocketChatShared.Models;

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