using System;

namespace WebSocketChatServer1.Monitoring;

// ����͸� API�� DTO��
public class CommandStatsDto
{
    public string CommandType { get; set; } = string.Empty;
    public long Count { get; set; }
    public double AvgExecutionTimeMs { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastExecuted { get; set; }
}