using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;
using MQTTSimulator.Simulation;
using Serilog;

var logFileName = $"logs/simulator-{DateTime.Now:yyyyMMdd-HHmmss}.log";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(logFileName, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddYamlFile("devices.yaml", optional: false, reloadOnChange: true);

// Remove default console logging — Spectre.Console owns the terminal
builder.Logging.ClearProviders();
builder.Services.AddLogging(lb => lb.AddSerilog(dispose: true));

builder.Services.Configure<SimulatorConfig>(builder.Configuration.GetSection("Simulator"));
builder.Services.AddSingleton<ConsoleDisplay>();
builder.Services.AddHostedService<SimulationHostedService>();

var host = builder.Build();
await host.RunAsync();
