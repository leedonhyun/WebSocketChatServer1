using System;

namespace WebSocketChatShared.Models;

public class ChatMessage : BaseMessage
{
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public string ToUsername { get; set; } = ""; // 1:1 채팅용
    public string[] ToUsernames { get; set; } = Array.Empty<string>(); // 1:N 채팅용
    public string ChatType { get; set; } = "public"; // "public", "private", "room"
    public string RoomId { get; set; } = ""; // 그룹 채팅용 ID
}