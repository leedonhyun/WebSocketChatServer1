using System;

namespace WebSocketChatShared.Models;

public class FileTransferInfo
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "";
    public string FromUsername { get; set; } = "";
    public string ToUsername { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}