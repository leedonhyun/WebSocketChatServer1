using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace WebSocketChatServer1.Monitoring;

[BsonIgnoreExtraElements]
public class SystemMetrics
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("activeConnections")]
    public int ActiveConnections { get; set; }

    [BsonElement("activeGroups")]
    public int ActiveGroups { get; set; }

    [BsonElement("totalMessages")]
    public long TotalMessages { get; set; }

    [BsonElement("totalFiles")]
    public long TotalFiles { get; set; }

    [BsonElement("totalErrors")]
    public long TotalErrors { get; set; }

    [BsonElement("serverInstance")]
    public string ServerInstance { get; set; } = Environment.MachineName;
}