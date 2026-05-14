using Azure.Messaging.ServiceBus;
using CicdEad.Demo.Consumer.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CicdEad.Demo.Consumer.Functions;

/// <summary>
/// Generic consumer for the <c>agent</c> Service Bus topic.
/// Handles: agent.instance.provisioned, agent.instance.ready, agent.instance.job-accepted,
///          agent.instance.job-released, agent.instance.stopping, agent.instance.stopped,
///          agent.instance.replaced, agent.instance.failed.
/// </summary>
public class AgentEventConsumer(ILogger<AgentEventConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function(nameof(AgentEventConsumer))]
    public void Run(
        [ServiceBusTrigger("agent", "generic-consumer", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogInformation(
            "Received message on [agent] topic. MessageId={MessageId}, EnqueuedTime={EnqueuedTime}",
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
            case "agent.instance.provisioned":
                logger.LogInformation(
                    "[agent.instance.provisioned] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.ready":
                logger.LogInformation(
                    "[agent.instance.ready] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.job-accepted":
                logger.LogInformation(
                    "[agent.instance.job-accepted] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.job-released":
                logger.LogInformation(
                    "[agent.instance.job-released] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.stopping":
                logger.LogInformation(
                    "[agent.instance.stopping] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.stopped":
                logger.LogInformation(
                    "[agent.instance.stopped] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.replaced":
                logger.LogInformation(
                    "[agent.instance.replaced] Source={Source} CorrelationId={CorrelationId} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Payload.ToString());
                break;

            case "agent.instance.failed":
                logger.LogWarning(
                    "[agent.instance.failed] Source={Source} CorrelationId={CorrelationId} Importance={Importance} Payload={Payload}",
                    envelope.Source,
                    envelope.CorrelationId,
                    envelope.Importance ?? "normal",
                    envelope.Payload.ToString());
                break;

            default:
                logger.LogWarning(
                    "Unexpected event type '{EventType}' received on agent topic. MessageId={MessageId}",
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
