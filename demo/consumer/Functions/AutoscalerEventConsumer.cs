using Azure.Messaging.ServiceBus;
using CicdEad.Demo.Consumer.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CicdEad.Demo.Consumer.Functions;

/// <summary>
/// Generic consumer for the <c>autoscaler</c> Service Bus topic.
/// Handles: autoscaler.pool.scaled-out, autoscaler.pool.scaled-in.
/// </summary>
public class AutoscalerEventConsumer(ILogger<AutoscalerEventConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function(nameof(AutoscalerEventConsumer))]
    public void Run(
        [ServiceBusTrigger("autoscaler", "generic-consumer", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogInformation(
            "Received message on [autoscaler] topic. MessageId={MessageId}, EnqueuedTime={EnqueuedTime}",
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
            case "autoscaler.pool.scaled-out":
                logger.LogInformation(
                    "[autoscaler.pool.scaled-out] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "autoscaler.pool.scaled-in":
                logger.LogInformation(
                    "[autoscaler.pool.scaled-in] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            default:
                logger.LogWarning(
                    "Unexpected event type '{EventType}' received on autoscaler topic. MessageId={MessageId}",
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
