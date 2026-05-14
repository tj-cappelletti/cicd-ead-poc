using System.Text.Json;
using System.Text.Json.Serialization;

namespace CicdEad.Demo.Consumer.Models;

/// <summary>
/// Represents the canonical CI/CD event bus envelope as defined in
/// schema/envelope.schema.json. Every message on the bus is wrapped in
/// this envelope; the <see cref="Payload"/> object varies by
/// <see cref="EventType"/> and is validated against the corresponding
/// payload schema in schema/payloads/.
/// </summary>
public sealed class EventEnvelope
{
    /// <summary>
    /// Groups related messages together across a workflow or incident.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this specific message instance (UUID).
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The type of event in the format &lt;capability&gt;.&lt;subject&gt;.&lt;verb&gt;
    /// (e.g., automation.run.failed, monitoring.alert.opened).
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The origin of the event in the format &lt;domain&gt;.&lt;resource&gt;
    /// (e.g., azuredevops.agentpool, github.workflow).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Envelope specification version. Always "1.0".
    /// </summary>
    [JsonPropertyName("specVersion")]
    public string SpecVersion { get; set; } = string.Empty;

    /// <summary>
    /// Version of the payload schema for this event type.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Optional importance level: critical, high, normal, or low.
    /// Defaults to "normal" when null.
    /// </summary>
    [JsonPropertyName("importance")]
    public string? Importance { get; set; }

    /// <summary>
    /// RFC 3339 timestamp when the event was detected.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Event-specific payload. Validate against the schema identified by
    /// <see cref="EventType"/> and <see cref="SchemaVersion"/>.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
