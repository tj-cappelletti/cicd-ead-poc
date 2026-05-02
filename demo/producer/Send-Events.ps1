<#
.SYNOPSIS
    Simulates a CI/CD event producer by sending sample events to Azure Service Bus.

.DESCRIPTION
    This script builds the EventProducer C# project and sends one or more sample
    event envelopes to the appropriate Azure Service Bus topic.

    It supports both the local Azure Service Bus Emulator (via docker-compose) and
    a real Azure Service Bus namespace.

.PARAMETER ConnectionString
    The Azure Service Bus connection string.
    For the local emulator, use the format:
        Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;UseDevelopmentEmulator=true;
    For a real Azure Service Bus namespace:
        Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;

    If not provided, the script looks for the SERVICEBUS_CONNECTION_STRING environment variable.

.PARAMETER EventType
    The event type to send. Defaults to a full demo sequence if not specified.
    Supported values:
        automation.run.started
        automation.run.completed
        automation.run.failed
        monitoring.alert.opened
        monitoring.alert.resolved
        monitoring.queue.depth-changed
        agent.instance.provisioned
        agent.instance.ready
        agent.instance.failed
        autoscaler.pool.scaled-out
        autoscaler.pool.scaled-in

.PARAMETER Count
    Number of events to send for the selected event type. Defaults to 1.

.PARAMETER Demo
    When specified, sends a pre-defined sequence of events that tells a story:
        1. automation.run.started
        2. agent.instance.provisioned
        3. agent.instance.ready
        4. autoscaler.pool.scaled-out
        5. monitoring.alert.opened
        6. automation.run.failed
        7. monitoring.alert.resolved

.EXAMPLE
    # Send a single automation.run.started event using the local emulator
    .\Send-Events.ps1 -EventType automation.run.started

.EXAMPLE
    # Run the full demo sequence with a real Azure Service Bus namespace
    .\Send-Events.ps1 -ConnectionString "Endpoint=sb://myns.servicebus.windows.net/;..." -Demo

.EXAMPLE
    # Send 5 monitoring.alert.opened events
    .\Send-Events.ps1 -EventType monitoring.alert.opened -Count 5
#>

[CmdletBinding()]
param (
    [Parameter()]
    [string]$ConnectionString = $env:SERVICEBUS_CONNECTION_STRING,

    [Parameter()]
    [ValidateSet(
        "automation.run.started",
        "automation.run.completed",
        "automation.run.failed",
        "monitoring.alert.opened",
        "monitoring.alert.resolved",
        "monitoring.queue.depth-changed",
        "agent.instance.provisioned",
        "agent.instance.ready",
        "agent.instance.failed",
        "autoscaler.pool.scaled-out",
        "autoscaler.pool.scaled-in"
    )]
    [string]$EventType,

    [Parameter()]
    [ValidateRange(1, 100)]
    [int]$Count = 1,

    [Parameter()]
    [switch]$Demo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot

function Write-Banner {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  CI/CD EAD Demo - Event Producer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Assert-DotNetSdk {
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        Write-Error "The .NET SDK is required but was not found. Install it from https://dot.net/download"
        exit 1
    }
    $version = dotnet --version
    Write-Host "  Using .NET SDK $version" -ForegroundColor Gray
}

function Build-Producer {
    Write-Host "Building EventProducer..." -ForegroundColor Yellow
    $result = dotnet build "$ScriptDir/Producer.csproj" --configuration Release --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:`n$result"
        exit 1
    }
    Write-Host "  Build succeeded." -ForegroundColor Green
}

function Send-Event {
    param (
        [string]$ConnStr,
        [string]$Type,
        [int]$Num = 1
    )

    Write-Host ""
    Write-Host "  -> Sending: $Type (x$Num)" -ForegroundColor White
    $result = dotnet run --project "$ScriptDir/Producer.csproj" --configuration Release --no-build -- "$ConnStr" "$Type" "$Num" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to send event '$Type':`n$result"
        exit 1
    }
    Write-Host $result -ForegroundColor DarkGray
}

# ── Entry point ─────────────────────────────────────────────────────────────────

Write-Banner
Assert-DotNetSdk

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Host "No connection string provided." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Options:" -ForegroundColor White
    Write-Host "  1. Set the SERVICEBUS_CONNECTION_STRING environment variable." -ForegroundColor Gray
    Write-Host "  2. Pass -ConnectionString <value> to this script." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Local emulator connection string format:" -ForegroundColor White
    Write-Host "  Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;UseDevelopmentEmulator=true;" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Azure Service Bus connection string format:" -ForegroundColor White
    Write-Host "  Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>;" -ForegroundColor Gray
    exit 1
}

Build-Producer

if ($Demo) {
    Write-Host ""
    Write-Host "Running full demo sequence..." -ForegroundColor Cyan

    $sequence = @(
        "automation.run.started",
        "agent.instance.provisioned",
        "agent.instance.ready",
        "autoscaler.pool.scaled-out",
        "monitoring.alert.opened",
        "automation.run.failed",
        "monitoring.alert.resolved"
    )

    foreach ($type in $sequence) {
        Send-Event -ConnStr $ConnectionString -Type $type
        Start-Sleep -Milliseconds 500
    }

    Write-Host ""
    Write-Host "Demo sequence complete! Check the consumer logs to see all events processed." -ForegroundColor Green
}
elseif (-not [string]::IsNullOrWhiteSpace($EventType)) {
    Send-Event -ConnStr $ConnectionString -Type $EventType -Num $Count
    Write-Host ""
    Write-Host "Done!" -ForegroundColor Green
}
else {
    Write-Host "No event type specified. Use -EventType <type> to send a specific event, or -Demo for the full demo sequence." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Supported event types:" -ForegroundColor White
    Write-Host "  automation.run.started" -ForegroundColor Gray
    Write-Host "  automation.run.completed" -ForegroundColor Gray
    Write-Host "  automation.run.failed" -ForegroundColor Gray
    Write-Host "  monitoring.alert.opened" -ForegroundColor Gray
    Write-Host "  monitoring.alert.resolved" -ForegroundColor Gray
    Write-Host "  monitoring.queue.depth-changed" -ForegroundColor Gray
    Write-Host "  agent.instance.provisioned" -ForegroundColor Gray
    Write-Host "  agent.instance.ready" -ForegroundColor Gray
    Write-Host "  agent.instance.failed" -ForegroundColor Gray
    Write-Host "  autoscaler.pool.scaled-out" -ForegroundColor Gray
    Write-Host "  autoscaler.pool.scaled-in" -ForegroundColor Gray
    exit 1
}
