using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LinksApi.Models;

public class Link
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("links")]
    [BsonRequired]
    public string Links { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("reason")]
    public string? Reason { get; set; }

    [BsonElement("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedat")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
