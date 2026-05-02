using Azure.Messaging.ServiceBus;
using CicdEad.Demo.Consumer.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CicdEad.Demo.Consumer.Functions;

/// <summary>
/// Generic consumer for the <c>monitoring</c> Service Bus topic.
/// Handles: monitoring.alert.opened, monitoring.alert.resolved, monitoring.queue.depth-changed.
/// </summary>
public class MonitoringEventConsumer(ILogger<MonitoringEventConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function(nameof(MonitoringEventConsumer))]
    public void Run(
        [ServiceBusTrigger("monitoring", "generic-consumer", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogInformation(
            "Received message on [monitoring] topic. MessageId={MessageId}, EnqueuedTime={EnqueuedTime}",
            message.MessageId,
            message.EnqueuedTime);

        var envelope = DeserializeEnvelope(message);
        if (envelope is null)
        {
            return;
        }

        LogEnvelopeHeader(envelope);

        switch (envelope.EventType)
        {
            case "monitoring.alert.opened":
                logger.LogWarning(
                    "[monitoring.alert.opened] Source={Source} CorrelationId={CorrelationId} Importance={Importance} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Importance ?? "normal",
                    envelope.Payload.ToString());
                break;

            case "monitoring.alert.resolved":
                logger.LogInformation(
                    "[monitoring.alert.resolved] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "monitoring.queue.depth-changed":
                logger.LogInformation(
                    "[monitoring.queue.depth-changed] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            default:
                logger.LogWarning(
                    "Unexpected event type '{EventType}' received on monitoring topic. MessageId={MessageId}",
                    envelope.EventType,
                    message.MessageId);
                break;
        }
    }

    private EventEnvelope? DeserializeEnvelope(ServiceBusReceivedMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<EventEnvelope>(message.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Failed to deserialize message body as EventEnvelope. MessageId={MessageId}",
                message.MessageId);
            return null;
        }
    }

    private void LogEnvelopeHeader(EventEnvelope envelope) =>
        logger.LogInformation(
            "Envelope: EventType={EventType} Source={Source} Importance={Importance} CorrelationId={CorrelationId} MessageId={MessageId}",
            envelope.EventType,
            envelope.Source,
            envelope.Importance ?? "normal",
            envelope.CorrelationId,
            envelope.MessageId);
}
