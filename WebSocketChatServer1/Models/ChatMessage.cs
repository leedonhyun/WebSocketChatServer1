using System;

namespace WebSocketChatServer1.Models;

public class ChatMessage : BaseMessage
{
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public string ToUsername { get; set; } = ""; // 1:1 채팅용
    public string[] ToUsernames { get; set; } = Array.Empty<string>(); // 1:N 채팅용
    public string ChatType { get; set; } = "public"; // "public", "private", "group"
    public string RoomId { get; set; } = ""; // 그룹 채팅용 ID
}