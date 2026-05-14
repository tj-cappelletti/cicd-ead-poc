using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace CicdEad.Demo.Producer;

internal static class Program
{
    // Mapping of eventType prefix to topic name (matches service-bus.config.json)
    private static readonly Dictionary<string, string> TopicMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "automation", "automation" },
        { "monitoring", "monitoring" },
        { "agent",      "agent"      },
        { "autoscaler", "autoscaler" },
        { "membership", "membership" },
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    internal static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: EventProducer <connectionString> <eventType> [count]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Supported event types:");
            Console.Error.WriteLine("  automation.run.started");
            Console.Error.WriteLine("  automation.run.completed");
            Console.Error.WriteLine("  automation.run.failed");
            Console.Error.WriteLine("  monitoring.alert.opened");
            Console.Error.WriteLine("  monitoring.alert.resolved");
            Console.Error.WriteLine("  monitoring.queue.depth-changed");
            Console.Error.WriteLine("  agent.instance.provisioned");
            Console.Error.WriteLine("  agent.instance.ready");
            Console.Error.WriteLine("  agent.instance.failed");
            Console.Error.WriteLine("  autoscaler.pool.scaled-out");
            Console.Error.WriteLine("  autoscaler.pool.scaled-in");
            return 1;
        }

        var connectionString = args[0];
        var eventType = args[1];
        var count = args.Length >= 3 && int.TryParse(args[2], out var n) ? n : 1;

        var topicPrefix = eventType.Split('.')[0];
        if (!TopicMap.TryGetValue(topicPrefix, out var topicName))
        {
            Console.Error.WriteLine($"Unknown event type prefix '{topicPrefix}'. Cannot determine target topic.");
            return 1;
        }

        Console.WriteLine($"Sending {count} '{eventType}' event(s) to topic '{topicName}'...");

        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(topicName);

        for (var i = 0; i < count; i++)
        {
            var envelope = BuildEnvelope(eventType, i + 1);
            var body = JsonSerializer.Serialize(envelope, JsonOptions);
            var message = new ServiceBusMessage(body)
            {
                ContentType = "application/json",
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
            };

            // Set application properties so consumers can filter without parsing the body
            message.ApplicationProperties["eventType"] = envelope.EventType;
            message.ApplicationProperties["source"] = envelope.Source;
            message.ApplicationProperties["importance"] = envelope.Importance ?? "normal";

            await sender.SendMessageAsync(message);
            Console.WriteLine($"  [{i + 1}/{count}] Sent {eventType} (messageId={envelope.MessageId})");
        }

        Console.WriteLine("Done.");
        return 0;
    }

    private static EventEnvelope BuildEnvelope(string eventType, int sequence)
    {
        var messageId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        return new EventEnvelope
        {
            CorrelationId  = $"demo:{eventType}:{sequence}",
            MessageId      = messageId,
            EventType      = eventType,
            Source         = ResolveSource(eventType),
            SpecVersion    = "1.0",
            SchemaVersion  = "1.0",
            Importance     = ResolveImportance(eventType),
            Timestamp      = now.ToString("o"),
            Payload        = BuildPayload(eventType, sequence, now),
        };
    }

    private static string ResolveSource(string eventType) => eventType switch
    {
        var t when t.StartsWith("automation") => "github.workflow",
        var t when t.StartsWith("monitoring") => "azuredevops.agentpool",
        var t when t.StartsWith("agent")      => "github.runner",
        var t when t.StartsWith("autoscaler") => "github.runner",
        var t when t.StartsWith("membership") => "azuredevops.membership",
        _                                     => "demo.source",
    };

    private static string? ResolveImportance(string eventType) => eventType switch
    {
        "automation.run.failed"      => "high",
        "monitoring.alert.opened"    => "high",
        "agent.instance.failed"      => "high",
        "monitoring.alert.resolved"  => "normal",
        _                            => "normal",
    };

    private static object BuildPayload(string eventType, int sequence, DateTimeOffset now) => eventType switch
    {
        "automation.run.started" => new
        {
            runId    = $"run-{now:yyyyMMdd}-{sequence:D5}",
            resource = new { kind = "github.workflow", id = "build-test.yml", name = "Build and Test" },
            trigger  = new { type = "event", actor = "demo-user@contoso.com", details = new { eventType = "push", branch = "main" } },
            startTime = now.ToString("o"),
        },
        "automation.run.completed" => new
        {
            runId     = $"run-{now:yyyyMMdd}-{sequence:D5}",
            resource  = new { kind = "github.workflow", id = "build-test.yml", name = "Build and Test" },
            status    = "succeeded",
            startTime = now.AddMinutes(-3).ToString("o"),
            endTime   = now.ToString("o"),
        },
        "automation.run.failed" => new
        {
            runId    = $"run-{now:yyyyMMdd}-{sequence:D5}",
            resource = new { kind = "github.workflow", id = "deploy.yml", name = "Deploy to Production" },
            status   = "failed",
            startTime = now.AddMinutes(-5).ToString("o"),
            endTime   = now.ToString("o"),
            errors   = new[] { "Health check failed for LoadBalancer endpoint" },
        },
        "monitoring.alert.opened" => new
        {
            alertId   = $"agentpool:prod-linux:offline-percent>50",
            alertType = "agent.pool.capacity.exhausted",
            resource  = new { kind = "agent-pool", id = "prod-linux", name = "Production Linux Agent Pool" },
            condition = new
            {
                name           = "Offline Agent Percentage Threshold Exceeded",
                metric         = "offline-percent",
                @operator      = "gt",
                threshold      = 50,
                observed       = 75,
                aggregation    = "avg",
                windowMinutes  = 5,
            },
            openedTimestamp = now.ToString("o"),
        },
        "monitoring.alert.resolved" => new
        {
            alertId          = $"agentpool:prod-linux:offline-percent>50",
            alertType        = "agent.pool.capacity.exhausted",
            resource         = new { kind = "agent-pool", id = "prod-linux", name = "Production Linux Agent Pool" },
            condition        = new
            {
                name           = "Offline Agent Percentage Threshold Exceeded",
                metric         = "offline-percent",
                @operator      = "lt",
                threshold      = 50,
                observed       = 12,
                aggregation    = "avg",
                windowMinutes  = 5,
            },
            openedTimestamp   = now.AddMinutes(-35).ToString("o"),
            resolvedTimestamp = now.ToString("o"),
            resolutionReason  = "condition-cleared",
        },
        "monitoring.queue.depth-changed" => new
        {
            queueId    = "prod-build-queue",
            queueName  = "Production Build Queue",
            previous   = 5,
            current    = 42,
            changeType = "increased",
            sampledAt  = now.ToString("o"),
        },
        "agent.instance.provisioned" => new
        {
            poolId        = "prod-linux",
            poolName      = "Production Linux Agent Pool",
            instanceId    = $"agent-vm-{sequence:D3}",
            instanceName  = $"agent-vm-{sequence:D3}.contoso.com",
            imageReference = "ubuntu-22.04-lts-2026-04-v1",
            requestedBy   = "autoscaler-service",
        },
        "agent.instance.ready" => new
        {
            poolId       = "prod-linux",
            instanceId   = $"agent-vm-{sequence:D3}",
            instanceName = $"agent-vm-{sequence:D3}.contoso.com",
            readyAt      = now.ToString("o"),
        },
        "agent.instance.failed" => new
        {
            poolId       = "prod-linux",
            instanceId   = $"agent-vm-{sequence:D3}",
            instanceName = $"agent-vm-{sequence:D3}.contoso.com",
            failedAt     = now.ToString("o"),
            reason       = "Unexpected process exit with code 1",
        },
        "autoscaler.pool.scaled-out" => new
        {
            poolId               = "prod-linux",
            poolName             = "Production Linux Agent Pool",
            previousCapacity     = 5,
            newCapacity          = 10,
            instancesProvisioned = 5,
            queueDepth           = 42,
            queueDepthThreshold  = 10,
        },
        "autoscaler.pool.scaled-in" => new
        {
            poolId              = "prod-linux",
            poolName            = "Production Linux Agent Pool",
            previousCapacity    = 10,
            newCapacity         = 5,
            instancesReleased   = 5,
            queueDepth          = 2,
            queueDepthThreshold = 10,
        },
        _ => new { message = $"Sample payload for event type '{eventType}'." },
    };
}

internal sealed class EventEnvelope
{
    public string CorrelationId { get; set; } = string.Empty;
    public string MessageId     { get; set; } = string.Empty;
    public string EventType     { get; set; } = string.Empty;
    public string Source        { get; set; } = string.Empty;
    public string SpecVersion   { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string? Importance   { get; set; }
    public string Timestamp     { get; set; } = string.Empty;
    public object Payload       { get; set; } = new();
}
