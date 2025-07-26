using System;

namespace WebSocketChatServer1.Monitoring;

// 모니터링 API용 DTO들
public class CommandStatsDto
{
    public string CommandType { get; set; } = string.Empty;
    public long Count { get; set; }
    public double AvgExecutionTimeMs { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastExecuted { get; set; }
}