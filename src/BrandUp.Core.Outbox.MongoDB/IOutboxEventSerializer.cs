using System.Text.Json;

namespace BrandUp.Events.MongoDB
{
    /// <summary>
    /// Serializes domain events for the outbox store, round-tripping the event's runtime type.
    /// The default is <see cref="JsonOutboxEventSerializer"/>.
    /// </summary>
    public interface IOutboxEventSerializer
    {
        /// <summary>
        /// Stable name of the event's runtime type, stored next to the payload and passed back to
        /// <see cref="Deserialize"/>.
        /// </summary>
        /// <param name="event">Event being stored.</param>
        string GetTypeName(IDomainEvent @event);

        /// <summary>
        /// Serializes the event payload.
        /// </summary>
        /// <param name="event">Event being stored.</param>
        string Serialize(IDomainEvent @event);

        /// <summary>
        /// Rebuilds the event from a stored payload; <see langword="null"/> when the type name no
        /// longer resolves — the processor marks such an event failed instead of crashing.
        /// </summary>
        /// <param name="typeName">Stored type name.</param>
        /// <param name="payload">Stored payload.</param>
        IDomainEvent? Deserialize(string typeName, string payload);
    }

    /// <summary>
    /// System.Text.Json <see cref="IOutboxEventSerializer"/>. The type name is
    /// <c>Namespace.Type, AssemblyName</c> — stable across assembly versions, breaking only when
    /// the event type is renamed or moved (deploy such renames only after the outbox is drained).
    /// </summary>
    public sealed class JsonOutboxEventSerializer(JsonSerializerOptions? options = null) : IOutboxEventSerializer
    {
        readonly JsonSerializerOptions options = options ?? JsonSerializerOptions.Web;

        /// <inheritdoc/>
        public string GetTypeName(IDomainEvent @event)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var type = @event.GetType();

            return $"{type.FullName}, {type.Assembly.GetName().Name}";
        }

        /// <inheritdoc/>
        public string Serialize(IDomainEvent @event)
        {
            ArgumentNullException.ThrowIfNull(@event);

            return JsonSerializer.Serialize(@event, @event.GetType(), options);
        }

        /// <inheritdoc/>
        public IDomainEvent? Deserialize(string typeName, string payload)
        {
            ArgumentException.ThrowIfNullOrEmpty(typeName);
            ArgumentNullException.ThrowIfNull(payload);

            var type = Type.GetType(typeName, throwOnError: false);
            if (type == null || !typeof(IDomainEvent).IsAssignableFrom(type))
                return null;

            return (IDomainEvent?)JsonSerializer.Deserialize(payload, type, options);
        }
    }
}
