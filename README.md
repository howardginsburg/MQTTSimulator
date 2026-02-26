# MQTTSimulator

A .NET 8 console application that simulates multiple IoT devices sending configurable telemetry to various brokers. Supports Azure IoT Hub, Azure Event Grid (via mTLS), Azure Event Hubs, and standard MQTT brokers with optional TLS.

## Features

- **Multiple broker types**: IoT Hub (SAS / X.509), MQTT with mutual TLS, MQTT with username/password, MQTT over TLS, Azure Event Hubs
- **Pure MQTT**: Uses MQTTnet directly for MQTT brokers — no Azure Device SDK dependencies
- **Fleet provisioning**: Auto-provision batches of IoT Hub or Event Hub devices from a single connection string
- **Named brokers**: Define shared broker connection details once and reference by name — devices override only what differs
- **Dynamic topic templates**: Use `{deviceId}` in MQTT topic strings — replaced with each device's ID at runtime
- **YAML configuration**: Clean, compact YAML config with smart defaults — auth, ports, and intervals are inferred automatically
- **Configurable telemetry**: Define reusable telemetry profiles with 7 field generator types
- **Startup validation**: Configuration is validated at startup with clear error messages before any connections are made
- **Connection stagger**: Configurable delay between device connections to avoid thundering-herd TLS handshakes
- **Live dashboard**: Real-time Spectre.Console UI showing device status and message counts
- **Structured logging**: Serilog file logging with per-device context
- **Graceful shutdown**: Ctrl+C cleanly disconnects all devices

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A broker to connect to (IoT Hub, Event Grid, Event Hubs, Mosquitto, etc.)

## Getting Started

### 1. Clone and build

```bash
git clone https://github.com/howardginsburg/MQTTSimulator.git
cd MQTTSimulator
dotnet build MQTTSimulator
```

### 2. Configure devices

Copy the sample configuration file and fill in your credentials:

```bash
cp MQTTSimulator/devices.sample.yaml MQTTSimulator/devices.yaml
```

> **Note:** `devices.yaml` is excluded from source control via `.gitignore` because it contains sensitive credentials. Never commit it. Use `devices.sample.yaml` as a template.

### 3. Run

```bash
dotnet run --project MQTTSimulator
```

Press **Ctrl+C** to gracefully stop all devices.

## Configuration

All configuration lives in `devices.yaml`. The file has five sections under `simulator:`.

### Global Settings

```yaml
simulator:
  connectionDelayMs: 100    # Stagger (ms) between device connections
  defaultInterval: 5s       # Default send interval for all devices/fleets
```

Interval format: `"5s"` (seconds), `"500ms"` (milliseconds), `"1m"` (minutes).

### Named Brokers

Define shared broker connection details once and reference them by name in devices or fleets. This avoids repeating connection strings, hostnames, and certificate paths across many devices.

```yaml
  brokers:
    my-iothub:
      type: IoTHub
      host: myhub.azure-devices.net      # device id inferred from device.id

    my-event-grid:
      type: MqttMtls
      host: myns.westus2-1.ts.eventgrid.azure.net
      topic: devices/{deviceId}/telemetry  # {deviceId} resolved at runtime
      ca: certs/eventgrid-ca.pem
```

In a device, reference with `name:` and provide only the fields that differ per device:

```yaml
  devices:
    - id: sensor-001
      broker:
        name: my-iothub
        key: your-device-shared-access-key   # only the device-specific key

    - id: Device1
      broker:
        name: my-event-grid
        cert: certs/Device1PublicKey.pem     # device-specific cert/key pair
        key: certs/Device1PrivateKey.pem
```

In a fleet, reference with `brokerRef:`:

```yaml
  fleets:
    - brokerRef: my-event-grid
      prefix: sensor
      count: 20
      profile: environmental
```

Any field set alongside `name:` or `brokerRef:` takes precedence over the named broker's value. Named brokers are purely optional — inline broker blocks work exactly as before.

### Topic Templates

All MQTT broker types (`Mqtt`, `MqttTls`, `MqttMtls`) support `{deviceId}` as a placeholder in the `topic` field. It is replaced with the actual device ID at runtime:

```yaml
brokers:
  my-grid:
    type: MqttMtls
    topic: devices/{deviceId}/telemetry   # Device1 → devices/Device1/telemetry
```

The token is case-insensitive (`{deviceId}`, `{DeviceId}`, `{DEVICEID}` all work). A device can still override the topic entirely by specifying its own `topic:` field.

### Telemetry Profiles

Reusable payload schemas. Multiple devices can share a profile — each gets its own independent generator state.

```yaml
  profiles:
    environmental:
      temperature: { gen: Increment, init: 20, step: 1, min: 15, max: 30 }
      humidity:    { gen: Random, min: 40, max: 80 }
      pressure:    { gen: Sine, amplitude: 10, offset: 1013.25, period: 60 }
      status:      { gen: Cycle, values: [ON, STANDBY, OFF] }
      timestamp:   { gen: Timestamp }
      location:    { gen: HashSelect, values: [Building-A, Building-B, Building-C] }
```

### Devices

Each device references a profile and specifies its broker connection. Auth and port are inferred automatically — only set `interval` if overriding the `defaultInterval`.

```yaml
  devices:
    - id: sensor-001
      profile: environmental
      broker:
        type: IoTHub
        connection: "HostName=myhub.azure-devices.net;DeviceId=sensor-001;SharedAccessKey=your-key"
```

### Fleets

Spawn multiple simulated devices from a single connection string. Devices are named `{prefix}-001`, `{prefix}-002`, etc.

```yaml
  fleets:
    - type: IoTHub
      connection: "HostName=myhub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=your-key"
      prefix: sim
      count: 5
      profile: environmental
```

## Broker Types

### IoT Hub (`IoTHub`)

Connects via raw MQTT 3.1.1. Publishes to `devices/{deviceId}/messages/events/`. Auth is auto-detected from configuration — no `auth` field needed.

**SAS (full device connection string):**
```yaml
broker:
  type: IoTHub
  connection: "HostName=myhub.azure-devices.net;DeviceId=sensor-001;SharedAccessKey=your-key"
```
The connection string is available in the Azure Portal under **IoT Hub → Devices → your device → Primary connection string**.

**SAS via named broker (device ID inferred, only key differs per device):**
```yaml
brokers:
  my-iothub:
    type: IoTHub
    host: myhub.azure-devices.net

devices:
  - id: sensor-001
    broker:
      name: my-iothub
      key: your-device-primary-key   # Primary Key from Azure Portal → IoT Hub → Devices
```
The device's `id` is used as the IoT Hub Device ID. The `key` is the device's Primary or Secondary Key (not the full connection string).

**X.509 (certificate):**
```yaml
broker:
  type: IoTHub
  host: myhub.azure-devices.net
  cert: certs/device.pem
  key: certs/device-key.pem
```

| Property | Auth | Description |
|----------|------|-------------|
| `connection` | SAS (full) | Full device connection string |
| `host` | SAS (named) / X.509 | IoT Hub hostname |
| `key` | SAS (named) | Device Primary/Secondary Key (from Azure Portal) |
| `cert` | X.509 | Path to client certificate PEM file |
| `key` | X.509 | Path to private key PEM file |

### Event Hubs (`EventHub`)

Sends telemetry to Azure Event Hubs using the Event Hubs REST API with SAS token authentication. Each device's ID is used as the partition key. No SDK dependency required.

```yaml
broker:
  type: EventHub
  connection: "Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=your-key"
  hub: my-event-hub
```

| Property | Required | Description |
|----------|----------|-------------|
| `connection` | Yes | Event Hub connection string |
| `hub` | No | Event Hub entity name (omit if `EntityPath` is in the connection string) |

### MQTT with Mutual TLS (`MqttMtls`)

Standard MQTT over mutual TLS. Works with Azure Event Grid MQTT broker and any mTLS-enabled broker.

```yaml
broker:
  type: MqttMtls
  host: myns.westus2-1.ts.eventgrid.azure.net
  topic: devices/{deviceId}/telemetry   # {deviceId} resolved at runtime
  cert: certs/client.pem
  key: certs/client-key.pem
  ca: certs/ca.pem
```

| Property | Required | Description |
|----------|----------|-------------|
| `host` | Yes | Broker hostname |
| `topic` | Yes | MQTT topic to publish to. Supports `{deviceId}` placeholder. |
| `cert` | Yes | Path to client certificate PEM file |
| `key` | Yes | Path to private key PEM file |
| `ca` | Yes | Path to CA certificate PEM file |

### MQTT with Username/Password (`Mqtt` / `MqttTls`)

Standard MQTT with optional TLS. Use `Mqtt` for plain connections or `MqttTls` for TLS-encrypted.

```yaml
broker:
  type: Mqtt          # or MqttTls
  host: localhost
  topic: devices/{deviceId}/telemetry   # {deviceId} resolved at runtime
  user: device1
  pass: secret
```

| Property | Required | Description |
|----------|----------|-------------|
| `host` | Yes | Broker hostname |
| `topic` | Yes | MQTT topic to publish to. Supports `{deviceId}` placeholder. |
| `port` | No | Override default port (Mqtt: 1883, MqttTls: 8883) |
| `user` | No | MQTT username |
| `pass` | No | MQTT password |
| `ca` | No | Path to CA certificate PEM file (MqttTls only) |

## Smart Defaults

To keep configuration minimal, several fields are inferred automatically:

| Feature | Rule |
|---------|------|
| **Auth** | `connection` present → SAS (full); `cert` present → X.509; `host` + `key` (no cert) → SAS via named broker |
| **Port** | IoTHub/MqttTls/MqttMtls → 8883; Mqtt → 1883 (EventHub uses HTTPS :443) |
| **IoTHub Device ID** | When using named broker pattern, the device's `id` is used as the IoT Hub Device ID |
| **Topic resolution** | `{deviceId}` in any MQTT `topic` string is replaced with the device's `id` at runtime |
| **Interval** | Inherits `defaultInterval` unless device/fleet overrides |
| **Enabled** | Defaults to `true` |

## Field Generators

| Generator | Description | Parameters |
|-----------|-------------|------------|
| `Increment` | Bounces between min and max by step | `init`, `step`, `min`, `max` |
| `Random` | Uniform random in range | `min`, `max` |
| `Sine` | Sinusoidal oscillation over time | `amplitude`, `offset`, `period` (seconds) |
| `Static` | Constant value (auto-detects type) | `value` (string) or `init` (numeric) |
| `Cycle` | Rotates through a list of values | `values` (list) |
| `Timestamp` | Current UTC time in ISO 8601 | *(none)* |
| `HashSelect` | Deterministic pick based on device ID hash | `values` (list) |

## Fleet Provisioning

Fleets let you spawn many simulated devices from a single connection string. The `type` field is required.

### IoT Hub Fleets

Devices are auto-created in IoT Hub via the REST API using an owner-level connection string. Each device gets its own SAS key.

```yaml
fleets:
  - type: IoTHub
    connection: "HostName=myhub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=your-key"
    prefix: sim
    count: 10
    profile: environmental
```

The owner connection string is available in the Azure Portal under **IoT Hub → Shared access policies → iothubowner**.

### Event Hub Fleets

All devices share one Event Hub connection string. Each device's ID is used as the partition key — no server-side provisioning needed.

```yaml
fleets:
  - type: EventHub
    connection: "Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=your-key"
    hub: my-event-hub
    prefix: eh
    count: 10
    profile: environmental
```

| Property | Required | Description |
|----------|----------|-------------|
| `type` | Yes (or `brokerRef`) | `IoTHub` or `EventHub` |
| `connection` | Yes (or `brokerRef`) | Connection string (owner-level for IoT Hub, SAS for Event Hub) |
| `brokerRef` | Alt. to type+connection | Name of a broker defined in `simulator.brokers` |
| `hub` | EventHub | Event Hub entity name |
| `prefix` | Yes | Device ID prefix (devices named `{prefix}-001`, etc.) |
| `count` | Yes | Number of devices to simulate |
| `profile` | Yes | Telemetry profile name |
| `interval` | No | Override send interval (inherits `defaultInterval`) |
| `enabled` | No | `true` / `false` (default: `true`) |

## Configuration Validation

The simulator validates all configuration at startup before attempting any connections. Errors are logged with clear messages and the app will not start until issues are fixed.

**Validated rules include:**
- Profiles have at least one field with valid generator parameters
- Device IDs are present and unique
- Profiles referenced by devices/fleets exist
- Broker-specific required fields are present after named broker resolution (e.g., `connection` or `host`+`key` for IoTHub SAS, `host` + `cert` + `key` + `ca` + `topic` for MqttMtls)
- Named broker references (`name:` / `brokerRef:`) resolve to a defined broker
- Fleet `type` or `brokerRef` is set, `connection` / `prefix` / `profile` are present, `count > 0`
- Certificate/key file paths that refer to PEM files are checked (warnings if missing); IoTHub `key` values (SharedAccessKey strings) are not treated as file paths

## Payload Format

Each message is a JSON object with the device ID and all configured telemetry fields:

```json
{
  "deviceId": "sensor-001",
  "temperature": 22.0,
  "humidity": 65.43,
  "pressure": 1018.72,
  "status": "ON",
  "timestamp": "2026-02-25T15:30:00.0000000Z",
  "location": "Building-A"
}
```

## Docker

### Build

```bash
docker build -t mqtt-simulator .
```

### Run

Mount your `devices.yaml` and certificate files:

```bash
docker run \
  -v /path/to/devices.yaml:/app/devices.yaml \
  -v /path/to/certs:/app/certs \
  mqtt-simulator
```