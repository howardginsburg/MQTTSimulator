using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MQTTSimulator.Configuration;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MQTTSimulator.Simulation;

public class ConsoleDisplay
{
    private readonly ConcurrentDictionary<string, DeviceState> _deviceStates = new();
    private LiveDisplayContext? _context;
    private int _currentPage = 0;
    private int _pageSize;
    private readonly int _configuredPageSize;

    private readonly bool _isInteractive;

    public ConsoleDisplay(IOptions<SimulatorConfig> config)
    {
        _isInteractive = !Console.IsInputRedirected;
        _configuredPageSize = config.Value.PageSize;
        _pageSize = _configuredPageSize > 0 ? _configuredPageSize : ComputeAutoPageSize();
    }

    private static int ComputeAutoPageSize()
    {
        // Reserve rows for: table border (2), title (1), header (1), footer (1), pager line (1), margin (2)
        var available = Console.WindowHeight - 8;
        return Math.Max(5, available);
    }

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
        // Recompute auto page size now that all devices are registered
        if (_configuredPageSize == 0)
            _pageSize = _isInteractive ? ComputeAutoPageSize() : _deviceStates.Count;

        var live = AnsiConsole.Live(new RenderableAdapter(this));
        await live.StartAsync(async ctx =>
        {
            _context = ctx;
            ctx.Refresh();

            // Background task: listen for ← → arrow keys to change page (interactive only)
            Task? inputTask = null;
            if (_isInteractive)
            {
                inputTask = Task.Run(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (!Console.KeyAvailable)
                        {
                            Thread.Sleep(50);
                            continue;
                        }
                        var key = Console.ReadKey(intercept: true);
                        var totalPages = TotalPages();
                        if (key.Key == ConsoleKey.RightArrow || key.Key == ConsoleKey.DownArrow)
                        {
                            if (_currentPage < totalPages - 1)
                            {
                                _currentPage++;
                                Refresh();
                            }
                        }
                        else if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.UpArrow)
                        {
                            if (_currentPage > 0)
                            {
                                _currentPage--;
                                Refresh();
                            }
                        }
                    }
                }, cancellationToken);
            }

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
                if (inputTask != null)
                    await inputTask.ConfigureAwait(false);
            }
        });
    }

    private int TotalPages()
    {
        var total = _deviceStates.Count;
        return total == 0 ? 1 : (int)Math.Ceiling(total / (double)_pageSize);
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
            var allDevices = _display._deviceStates.Values.OrderBy(s => s.DeviceId).ToList();
            var pageSize = _display._pageSize;
            var totalPages = _display.TotalPages();
            // Clamp current page in case device count changed
            var page = Math.Min(_display._currentPage, totalPages - 1);
            _display._currentPage = page;

            var pageDevices = allDevices.Skip(page * pageSize).Take(pageSize).ToList();

            var pagerText = totalPages > 1
                ? $"Page {page + 1} of {totalPages}  [dim]\u2190 \u2192 to navigate[/]"
                : string.Empty;

            var title = totalPages > 1
                ? $"[bold yellow]MQTT Simulator[/]  {pagerText}"
                : "[bold yellow]MQTT Simulator[/]";

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title(title)
                .Expand()
                .AddColumn("Device")
                .AddColumn("Broker")
                .AddColumn("Status")
                .AddColumn(new TableColumn("Msgs").RightAligned())
                .AddColumn("Last Send")
                .AddColumn(new TableColumn("Last Telemetry"));

            foreach (var state in pageDevices)
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
