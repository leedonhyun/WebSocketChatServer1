using System;
using System.Collections.Generic;

namespace WebSocketChatShared.Models;

public class Room
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public HashSet<string> Members { get; set; } = new();
}