using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MQTTSimulator.Simulation;

public class ConsoleDisplay
{
    private readonly ConcurrentDictionary<string, DeviceState> _deviceStates = new();
    private LiveDisplayContext? _context;

    public void RegisterDevice(string deviceId, string brokerType)
    {
        _deviceStates[deviceId] = new DeviceState
        {
            DeviceId = deviceId,
            BrokerType = brokerType,
            Status = "Starting"
        };
    }

    public void UpdateStatus(string deviceId, string status)
    {
        if (_deviceStates.TryGetValue(deviceId, out var state))
        {
            state.Status = status;
            Refresh();
        }
    }

    public void RecordTelemetry(string deviceId, long messageId, string payload)
    {
        if (_deviceStates.TryGetValue(deviceId, out var state))
        {
            state.MessagesSent = messageId;
            state.LastSendTime = DateTime.Now.ToString("HH:mm:ss");
            state.LastPayload = payload;
        }

        Refresh();
    }

    public void RecordError(string deviceId, string error)
    {
        if (_deviceStates.TryGetValue(deviceId, out var state))
        {
            state.Status = "Error";
            state.LastPayload = error;
        }

        Refresh();
    }

    private void Refresh()
    {
        _context?.Refresh();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var live = AnsiConsole.Live(new RenderableAdapter(this));
        await live.StartAsync(async ctx =>
        {
            _context = ctx;
            ctx.Refresh();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            finally
            {
                _context = null;
            }
        });
    }

    private sealed class RenderableAdapter : IRenderable
    {
        private readonly ConsoleDisplay _display;

        public RenderableAdapter(ConsoleDisplay display) => _display = display;

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            return Build().Measure(options, maxWidth);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            return Build().Render(options, maxWidth);
        }

        private IRenderable Build()
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold yellow]MQTT Simulator[/]")
                .Expand()
                .AddColumn("Device")
                .AddColumn("Broker")
                .AddColumn("Status")
                .AddColumn(new TableColumn("Msgs").RightAligned())
                .AddColumn("Last Send")
                .AddColumn(new TableColumn("Last Telemetry"));

            foreach (var state in _display._deviceStates.Values.OrderBy(s => s.DeviceId))
            {
                var statusMarkup = state.Status switch
                {
                    "Connected" => "[green]Connected[/]",
                    "Error" => "[red]Error[/]",
                    "Stopped" => "[grey]Stopped[/]",
                    _ => $"[yellow]{Markup.Escape(state.Status)}[/]"
                };

                var telemetry = string.IsNullOrEmpty(state.LastPayload)
                    ? new Markup("[grey]-[/]")
                    : new Markup($"[dim]{Markup.Escape(state.LastPayload)}[/]");

                table.AddRow(
                    new Markup(Markup.Escape(state.DeviceId)),
                    new Markup(Markup.Escape(state.BrokerType)),
                    new Markup(statusMarkup),
                    new Markup(state.MessagesSent.ToString()),
                    new Markup(state.LastSendTime),
                    telemetry
                );
            }

            return table;
        }
    }
}
