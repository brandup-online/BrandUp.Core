using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BrandUp.Events.MongoDB
{
    /// <summary>
    /// A deferred domain event persisted in the outbox collection. Element names are shortened
    /// for storage; dates are stored as BSON UTC dates.
    /// </summary>
    public sealed class OutboxEventDocument
    {
        /// <summary>Document identifier.</summary>
        [BsonId]
        public ObjectId Id { get; set; }

        /// <summary>Stored type name of the event (see <see cref="IOutboxEventSerializer.GetTypeName"/>).</summary>
        [BsonElement("t"), BsonRequired]
        public required string EventType { get; set; }

        /// <summary>Serialized event payload.</summary>
        [BsonElement("p"), BsonRequired]
        public required string Payload { get; set; }

        /// <summary>When the event was enqueued (UTC).</summary>
        [BsonElement("ts"), BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
        public DateTime CreatedAt { get; set; }

        /// <summary>Delivery attempts made so far.</summary>
        [BsonElement("a")]
        public int Attempts { get; set; }

        /// <summary>Until when the event is claimed by a processor; expired leases are reclaimable.</summary>
        [BsonElement("l"), BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
        public DateTime? LockedUntil { get; set; }

        /// <summary>Token of the claim pass that stamped the current lease (see <see cref="MongoEventOutbox.ClaimPendingAsync"/>).</summary>
        [BsonElement("c"), BsonIgnoreIfNull]
        public ObjectId? ClaimToken { get; set; }

        /// <summary>When the event was successfully delivered; delivered events are never retried.</summary>
        [BsonElement("d"), BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
        public DateTime? DeliveredAt { get; set; }

        /// <summary>When the event was marked poison after exhausting its attempts.</summary>
        [BsonElement("dd"), BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
        public DateTime? DeadAt { get; set; }

        /// <summary>Message of the last delivery failure.</summary>
        [BsonElement("e")]
        public string? LastError { get; set; }
    }
}
