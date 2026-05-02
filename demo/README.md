# CI/CD EAD Demo

This local demo shows how systems emit messages and how generic consumers can process those messages using **Azure Service Bus** as the transport layer.

- **Transport:** Azure Service Bus (local emulator via Docker, or a real namespace)
- **Producer:** PowerShell script (`Send-Events.ps1`) backed by a C# console app
- **Consumer:** Azure Functions (.NET 8, isolated worker) — one function per topic

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Azure Service Bus                           │
│                                                                 │
│  Topics          automation  monitoring  agent  autoscaler      │
│                       │           │        │        │           │
│  Subscriptions  generic-consumer (one per topic)                │
└─────────────────────────────────────────────────────────────────┘
        ▲                                          │
        │ AMQP                                     │ AMQP
        │                                          ▼
┌───────────────┐                        ┌─────────────────────┐
│   Producer    │                        │  Azure Functions    │
│  (PowerShell  │                        │  Consumer           │
│  + C# CLI)    │                        │  (.NET 8 isolated)  │
└───────────────┘                        └─────────────────────┘
```

### Topics and Event Types

| Topic        | Event Types                                                                                 |
|--------------|---------------------------------------------------------------------------------------------|
| `automation` | `automation.run.started`, `automation.run.completed`, `automation.run.failed`               |
| `monitoring` | `monitoring.alert.opened`, `monitoring.alert.resolved`, `monitoring.queue.depth-changed`    |
| `agent`      | `agent.instance.provisioned`, `agent.instance.ready`, `agent.instance.failed`, and more     |
| `autoscaler` | `autoscaler.pool.scaled-out`, `autoscaler.pool.scaled-in`                                   |
| `membership` | `membership.user.added`, `membership.user.removed`, and more *(extend producer as needed)*  |

Every message is wrapped in the **canonical envelope** defined in [`schema/envelope.schema.json`](../schema/envelope.schema.json).

---

## Prerequisites

| Tool | Minimum Version | Purpose |
|------|----------------|---------|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | Runs the Service Bus Emulator and Azurite |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 | Builds and runs the producer and consumer |
| [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) | v4 | Runs Azure Functions locally |
| [PowerShell](https://github.com/PowerShell/PowerShell) | 7.x | Runs the producer script |

---

## Quick Start

### 1. Start the local infrastructure

```bash
cd demo
docker-compose up -d
```

This starts:
- **Azure Service Bus Emulator** on port `5672` (AMQP)
- **Azurite** (Azure Storage emulator) on ports `10000-10002` (required by Azure Functions local runtime)

Wait ~15 seconds for the Service Bus Emulator to finish initializing. You can check its logs with:

```bash
docker logs cicd-ead-servicebus -f
```

### 2. Configure the consumer

Copy the settings template and fill in the emulator connection string:

```bash
cp demo/consumer/local.settings.json.template demo/consumer/local.settings.json
```

Edit `demo/consumer/local.settings.json` and replace `<YOUR_EMULATOR_KEY>` with the key printed in the emulator container logs, or find it in the output of:

```bash
docker logs cicd-ead-servicebus 2>&1 | grep -i "SharedAccessKey"
```

The full `ServiceBusConnection` value looks like:

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;UseDevelopmentEmulator=true;
```

> **Tip — using a real Azure Service Bus namespace:**  
> Set `ServiceBusConnection` to your namespace's connection string from the Azure portal. The emulator and cloud namespace use the same connection string format (minus `UseDevelopmentEmulator=true`).

### 3. Start the consumer (Azure Functions)

In a new terminal:

```bash
cd demo/consumer
func start
```

You should see the four Azure Function triggers register for the `automation`, `monitoring`, `agent`, and `autoscaler` topics.

### 4. Send events (producer)

Open a PowerShell terminal and set the connection string:

```powershell
$env:SERVICEBUS_CONNECTION_STRING = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;UseDevelopmentEmulator=true;"
```

Run the full demo sequence (sends 7 events telling an end-to-end story):

```powershell
cd demo/producer
.\Send-Events.ps1 -Demo
```

Or send a specific event type:

```powershell
.\Send-Events.ps1 -EventType monitoring.alert.opened
.\Send-Events.ps1 -EventType automation.run.failed -Count 3
```

Switch back to the consumer terminal and watch the log output as each event flows through.

---

## Demo Sequence

When you run `.\Send-Events.ps1 -Demo`, the following events are sent in order to demonstrate how an end-to-end CI/CD scenario flows through the bus:

| Step | Event Type                    | Topic        | What It Represents                              |
|------|-------------------------------|--------------|-------------------------------------------------|
| 1    | `automation.run.started`      | `automation` | A GitHub workflow kicked off                    |
| 2    | `agent.instance.provisioned`  | `agent`      | Autoscaler provisioned a new runner             |
| 3    | `agent.instance.ready`        | `agent`      | Runner is online and accepting jobs             |
| 4    | `autoscaler.pool.scaled-out`  | `autoscaler` | Pool grew due to queue depth                    |
| 5    | `monitoring.alert.opened`     | `monitoring` | Alert fired: offline agent % exceeded threshold |
| 6    | `automation.run.failed`       | `automation` | The workflow failed (e.g., deploy error)        |
| 7    | `monitoring.alert.resolved`   | `monitoring` | Monitoring alert cleared after recovery         |

---

## Project Structure

```
demo/
├── README.md                          # This file
├── docker-compose.yml                 # Local infrastructure (emulator + Azurite)
├── infrastructure/
│   └── service-bus.config.json        # Emulator topic/subscription configuration
├── producer/
│   ├── Send-Events.ps1                # PowerShell entry point (producer interface)
│   ├── Producer.csproj                # C# console app — sends events via AMQP
│   └── Program.cs                     # Producer implementation
└── consumer/
    ├── consumer.csproj                # Azure Functions project (.NET 8, isolated)
    ├── Program.cs                     # Functions host entry point
    ├── host.json                      # Functions host configuration
    ├── local.settings.json.template   # Settings template (copy → local.settings.json)
    ├── Models/
    │   └── EventEnvelope.cs           # Canonical envelope model
    └── Functions/
        ├── AutomationEventConsumer.cs # Trigger on the automation topic
        ├── MonitoringEventConsumer.cs # Trigger on the monitoring topic
        ├── AgentEventConsumer.cs      # Trigger on the agent topic
        └── AutoscalerEventConsumer.cs # Trigger on the autoscaler topic
```

---

## How It Works

### Envelope

Every message on the bus is a JSON object that follows the canonical envelope schema:

```json
{
  "correlationId": "demo:automation.run.started:1",
  "messageId": "550e8400-e29b-41d4-a716-446655440000",
  "eventType": "automation.run.started",
  "source": "github.workflow",
  "specVersion": "1.0",
  "schemaVersion": "1.0",
  "importance": "normal",
  "timestamp": "2026-05-01T12:00:00Z",
  "payload": { ... }
}
```

The `eventType` determines which topic the message is routed to and which payload schema applies. See [`schema/`](../schema/) for schemas and [`schema/registry/`](../schema/registry/) for the event catalog.

### Topic Routing

The producer maps the first segment of `eventType` to a topic:

| `eventType` prefix | Service Bus topic |
|--------------------|-------------------|
| `automation`       | `automation`      |
| `monitoring`       | `monitoring`      |
| `agent`            | `agent`           |
| `autoscaler`       | `autoscaler`      |
| `membership`       | `membership`      |

### Generic Consumer Pattern

Each Azure Function subscribes to a single topic via the `generic-consumer` subscription. The function:

1. Receives any message on the topic (regardless of `eventType`)
2. Deserializes the canonical envelope
3. Routes processing using a `switch` on `envelope.EventType`
4. Logs the event details

This pattern allows a single consumer to handle all events on a topic without knowing the producer in advance. Domain-specific consumers can extend this by also inspecting `payload.details`.

---

## Extending the Demo

### Adding a new event type

1. Add the event to [`schema/registry/events.yaml`](../schema/registry/events.yaml)
2. Add a payload schema to `schema/payloads/<capability>/`
3. Add a case to the appropriate consumer function in `demo/consumer/Functions/`
4. Add a payload builder to `demo/producer/Program.cs`

### Adding a new topic (capability)

1. Add the topic to [`schema/registry/events.yaml`](../schema/registry/events.yaml)
2. Add the topic to `demo/infrastructure/service-bus.config.json`
3. Create a new Azure Function consumer in `demo/consumer/Functions/`
4. Map the new prefix in `demo/producer/Program.cs`
