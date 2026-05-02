# CI/CD Events

**This repository defines the canonical envelope + event contracts; domains own the `details`.**

This repository is the source of truth for our event bus message contracts.
It exists to make event publishing and consumption consistent across CI/CD tooling, while still allowing domain teams to evolve source-specific details without forcing every consumer to understand the producer.

To understand why we are focusing on an event-driven architecture (EDA), checkout [CI/CD - Why Event-Driven Architecture (EAD)](./docs/WHY_EVENT_DRIVEN.md) in the `docs` folder.

---

## What This Repository Is Reponsible For

### The Event Catalog

This repository defines **which event types exist** and the minimum metadata that must be present for each message to be meaningful and routable.

At a high-level, the event catalog standardizes:

- `eventType` naming and semantics (e.g., `monitoring.alert.opened` or `automation.run.failed`)
- Required envelope fields and how they are used across the ecosystem
- Canonical payload expectation per `eventType`

### Canonical Payload Schema Per Event Type

For each `eventType`, this repository defines a **canonical payload schema** that captures the common fields consumers can rely on.

Canonical payload schemas are designed to enable generic consumers such as:

- Notification routers (e.g. Teams or Email)
- Dashboard/metrics pipelines
- Auditing and reporting
- Incident automation

---

## What Domain/Source Owners Are Responsible For

The `details` objects is the primary extension mechanism

- The **domain/source team owns** the schema and semantics for the `details` object
- Generic consumers should not be required to understand `details`
- Domain-specific consumers may validate and process `details` using domain-owned schemas/utilization

This keeps the bus interoperable while allowing each domain to iterate quickly on source-specific data.

---

## Core Concepts

### `eventType`

The `eventType` answers: **What happened?**

We use a consistent pattern:

```text
<capability>.<subject>.<verb>
```
**Examples:**

- `agent.instance.failed`
- `automation.run.started`
- `autoscaler.pool.scaled-in`
- `monitoring.alert.resolved`

### `source`

The `source` answers: **Where did this event originate from?**

We use a simple pattern to allow for maximum flexibility:

```text
<domain>.<resource>
```

**Examples:**

- `azuredevops.agentpool`
- `azuredevops.membership`
- `blackduck.availability`
- `checkmarx.availability`
- `github.automation`
- `github.workflow`

### `correlationId` vs `messageId`

- `correlationId`: Groups **related events** in a flow or incident (useful for joining and traceability)
- `messageId`: Identifies a **single logical event** (useful for dedup/idempotency)

See: [`docs/MESSAGE_IDENTITY.md`](./docs/MESSAGE_IDENTITY.md)

---

## Repository Structure

- `demo/` (a local demo of how events flow)
- `docs/` (design notes and standards)
- `examples/` (example messages that must validate in CI)
- `schema/`
  - `envelope.schema.json` (canonical envelope)
  - `payloads/` (canonical schemas per event type)
  - `registry/` (event catalog metadata)

---

## Validation and CI (high-level)

This repository uses CI to ensure contract consistency. Typical validation may consist of:

- Example message validate against the envelop schema
- Example payloads validate their canonical payload schemas
- Event naming convention remain consistent

Domain/source repositories are encouraged to:

- Publish and version their `details` schemas
- Validate their own example messages (including `details`) in their own CI

---

## Design Tools

- **Interoperablity first:** Consumers can reliably process canonical events without knowing every source
- **Extensibility:** Domain teams can add source-specific details without breaking generic consumers
- **Clarity:** The meaning of `eventType`, `source`, and identity fields is consistent across the ecosystem
- **Pragmatism:** We socialize standards early and add enforcement where it provides high ROI

---

## Contributing (high-level)

**TO DO: Need to provide guidance and details once initially released.**
