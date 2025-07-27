using System;

namespace WebSocketChatServer1.Models;

public class ChatMessage : BaseMessage
{
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public string ToUsername { get; set; } = ""; // 1:1 ä�ÿ�
    public string[] ToUsernames { get; set; } = Array.Empty<string>(); // 1:N ä�ÿ�
    public string ChatType { get; set; } = "public"; // "public", "private", "group"
    public string RoomId { get; set; } = ""; // �׷� ä�ÿ� ID
}