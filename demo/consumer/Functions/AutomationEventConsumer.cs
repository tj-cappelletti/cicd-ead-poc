using Azure.Messaging.ServiceBus;
using CicdEad.Demo.Consumer.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CicdEad.Demo.Consumer.Functions;

/// <summary>
/// Generic consumer for the <c>automation</c> Service Bus topic.
/// Handles: automation.run.started, automation.run.completed, automation.run.failed.
/// </summary>
public class AutomationEventConsumer(ILogger<AutomationEventConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function(nameof(AutomationEventConsumer))]
    public void Run(
        [ServiceBusTrigger("automation", "generic-consumer", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogInformation(
            "Received message on [automation] topic. MessageId={MessageId}, EnqueuedTime={EnqueuedTime}",
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
            case "automation.run.started":
                logger.LogInformation(
                    "[automation.run.started] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "automation.run.completed":
                logger.LogInformation(
                    "[automation.run.completed] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "automation.run.failed":
                logger.LogWarning(
                    "[automation.run.failed] Source={Source} CorrelationId={CorrelationId} Importance={Importance} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Importance ?? "normal",
                    envelope.Payload.ToString());
                break;

            default:
                logger.LogWarning(
                    "Unexpected event type '{EventType}' received on automation topic. MessageId={MessageId}",
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
