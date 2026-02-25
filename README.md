# MQTTSimulator

A .NET 8 console application that simulates multiple MQTT devices sending configurable telemetry to various MQTT brokers. Supports Azure IoT Hub, Azure Event Grid (via mTLS), and standard MQTT brokers with optional TLS.

## Features

- **Multiple broker types**: IoT Hub (SAS/X.509), MQTT with mutual TLS, MQTT with username/password, MQTT over TLS
- **Pure MQTT**: Uses MQTTnet directly — no Azure Device SDK dependencies
- **Configurable telemetry**: Define reusable telemetry profiles with multiple field generator types
- **Field generators**: Increment (bouncing), Random Range, Sine Wave, Static, Enum Cycle, Timestamp
- **Connection stagger**: Configurable delay between device connections to avoid thundering-herd TLS handshakes
- **Graceful shutdown**: Ctrl+C cleanly disconnects all devices
- **Docker ready**: Multi-stage Dockerfile included

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An MQTT broker to connect to (IoT Hub, Event Grid, Mosquitto, etc.)

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
cp MQTTSimulator/appsettings.sample.json MQTTSimulator/appsettings.json
```

> **Note:** `appsettings.json` is excluded from source control via `.gitignore` because it contains sensitive credentials. Never commit it. Use `appsettings.sample.json` as a template.

Edit `MQTTSimulator/appsettings.json` to define your telemetry profiles and devices. The configuration has three main sections:

#### Connection Delay

```json
"ConnectionDelayMs": 100
```

Milliseconds to wait between starting each device connection. Set to `0` for immediate parallel startup. Useful when simulating many devices to avoid overwhelming the broker with simultaneous TLS handshakes.

#### Telemetry Profiles

Define reusable sets of telemetry fields. Multiple devices can reference the same profile — each gets its own independent generator state.

```json
"TelemetryProfiles": {
  "environmental": [
    {
      "Name": "temperature",
      "DataType": "double",
      "Generator": "Increment",
      "InitialValue": 20.0,
      "Step": 1.0,
      "Min": 15.0,
      "Max": 30.0
    },
    {
      "Name": "humidity",
      "DataType": "double",
      "Generator": "RandomRange",
      "Min": 40.0,
      "Max": 80.0
    }
  ]
}
```

#### Devices

Each device references a telemetry profile by name and specifies its broker connection details.

```json
"Devices": [
  {
    "DeviceId": "sensor-001",
    "Enabled": true,
    "SendIntervalMs": 5000,
    "TelemetryProfileName": "environmental",
    "Broker": {
      "Type": "IoTHub",
      "AuthMethod": "SAS",
      "ConnectionString": "HostName=myhub.azure-devices.net;DeviceId=sensor-001;SharedAccessKey=your-base64-key"
    }
  }
]
```

### 3. Run

```bash
dotnet run --project MQTTSimulator
```

Press **Ctrl+C** to gracefully stop all devices.

## Broker Types

### IoT Hub (`IoTHub`)

Connects to Azure IoT Hub using raw MQTT 3.1.1. Publishes to `devices/{deviceId}/messages/events/`.

| Property | Description |
|----------|-------------|
| `AuthMethod` | `SAS` or `X509` |
| `ConnectionString` | IoT Hub device connection string (SAS auth) — parsed at runtime to extract hostname, device ID, and key |
| `Hostname` | IoT Hub hostname (X.509 auth only) |
| `CertificatePath` | Path to client certificate PEM file (X.509 auth) |
| `KeyPath` | Path to private key PEM file (X.509 auth) |

**SAS example:**
```json
{
  "Type": "IoTHub",
  "AuthMethod": "SAS",
  "ConnectionString": "HostName=myhub.azure-devices.net;DeviceId=sensor-001;SharedAccessKey=your-base64-key"
}
```

The connection string is available in the Azure Portal under **IoT Hub > Devices > your device > Primary connection string**.

**X.509 example:**
```json
{
  "Type": "IoTHub",
  "Hostname": "myhub.azure-devices.net",
  "AuthMethod": "X509",
  "CertificatePath": "certs/device.pem",
  "KeyPath": "certs/device-key.pem"
}
```

#### IoT Hub Fleet Provisioning

Instead of configuring each device individually, you can use the `IoTHubFleets` section to auto-provision a batch of devices using an IoT Hub owner connection string. The simulator will create the devices in IoT Hub if they don't already exist, retrieve their connection strings, and run them all with the same telemetry profile.

| Property | Description |
|----------|-------------|
| `Enabled` | Whether this fleet is active (default: `true`) |
| `ConnectionString` | IoT Hub owner/service connection string (e.g., `iothubowner` policy) |
| `DevicePrefix` | Prefix for device IDs (devices are named `{prefix}-001`, `{prefix}-002`, etc.) |
| `DeviceCount` | Number of devices to create and simulate |
| `SendIntervalMs` | Telemetry send interval in milliseconds |
| `TelemetryProfileName` | Name of the telemetry profile to use for all fleet devices |

```json
"IoTHubFleets": [
  {
    "Enabled": true,
    "ConnectionString": "HostName=myhub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=your-owner-key",
    "DevicePrefix": "sim",
    "DeviceCount": 10,
    "SendIntervalMs": 5000,
    "TelemetryProfileName": "environmental"
  }
]
```

The owner connection string is available in the Azure Portal under **IoT Hub > Shared access policies > iothubowner**. Fleet devices are combined with any individually configured devices and respect the `ConnectionDelayMs` stagger.

### MQTT with Mutual TLS (`MqttMtls`)

Standard MQTT over mutual TLS. Works with Azure Event Grid MQTT broker and any mTLS-enabled broker.

| Property | Description |
|----------|-------------|
| `Hostname` | Broker hostname |
| `Port` | Broker port (default: `8883`) |
| `Topic` | MQTT topic to publish to |
| `CertificatePath` | Path to client certificate PEM file |
| `KeyPath` | Path to private key PEM file |
| `CaCertificatePath` | Path to server/CA certificate PEM file |

```json
{
  "Type": "MqttMtls",
  "Hostname": "myns.westus2-1.ts.eventgrid.azure.net",
  "Port": 8883,
  "Topic": "devices/gateway-001/telemetry",
  "CertificatePath": "certs/client.pem",
  "KeyPath": "certs/client-key.pem",
  "CaCertificatePath": "certs/ca.pem"
}
```

### MQTT with Username/Password (`Mqtt` / `MqttTls`)

Standard MQTT with username/password authentication. Use `Mqtt` for plain connections or `MqttTls` for TLS-encrypted connections.

| Property | Description |
|----------|-------------|
| `Hostname` | Broker hostname |
| `Port` | Broker port (default: `1883` for Mqtt, `8883` for MqttTls) |
| `Topic` | MQTT topic to publish to |
| `Username` | MQTT username |
| `Password` | MQTT password |
| `CaCertificatePath` | Path to CA certificate PEM file (MqttTls only, optional) |

```json
{
  "Type": "MqttTls",
  "Hostname": "broker.example.com",
  "Port": 8883,
  "Topic": "devices/device1/telemetry",
  "Username": "device1",
  "Password": "secret",
  "CaCertificatePath": "certs/ca.pem"
}
```

## Field Generators

| Generator | Description | Config Properties |
|-----------|-------------|-------------------|
| `Increment` | Bounces between min and max by step | `InitialValue`, `Step`, `Min`, `Max` |
| `RandomRange` | Random value between min and max | `Min`, `Max` |
| `SineWave` | Sinusoidal oscillation | `Amplitude`, `Offset`, `PeriodSeconds` |
| `Static` | Constant value | `Value` (string) or `InitialValue` (numeric) |
| `EnumCycle` | Cycles through a list of strings | `Values` (string array) |
| `Timestamp` | Current UTC time in ISO 8601 | *(none)* |

## Docker

### Build

```bash
docker build -t mqtt-simulator .
```

### Run

```bash
docker run mqtt-simulator
```

Override configuration via environment variables using the .NET configuration naming convention:

```bash
docker run \
  -e Simulator__Devices__0__Broker__ConnectionString="HostName=myhub.azure-devices.net;DeviceId=my-device;SharedAccessKey=your-key" \
  mqtt-simulator
```

Mount certificate files if using X.509 or mTLS:

```bash
docker run \
  -v /path/to/certs:/app/certs \
  mqtt-simulator
```

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