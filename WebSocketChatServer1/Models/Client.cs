using System;

namespace ChatSystem.Models;

public class Client
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public ClientStatus Status { get; set; } = ClientStatus.Connected;
}