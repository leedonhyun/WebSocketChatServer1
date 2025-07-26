using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ChatSystem.Monitoring;

// MongoDB ¸ðµ¨µé
[BsonIgnoreExtraElements]
public class CommandLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [BsonElement("username")]
    public string? Username { get; set; }

    [BsonElement("commandType")]
    public string CommandType { get; set; } = string.Empty;

    [BsonElement("parameters")]
    public BsonDocument Parameters { get; set; } = new();

    [BsonElement("success")]
    public bool Success { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [BsonElement("responseData")]
    public BsonDocument? ResponseData { get; set; }

    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }

    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
}