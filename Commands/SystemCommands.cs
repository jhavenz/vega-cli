using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.CpuBound;
using VegaDevCli.Domain.Devices;

namespace VegaDevCli.Commands;

[Description("System monitoring and cleanup")]
public sealed class SystemCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega system --help' to see available subcommands");
        return 0;
    }
}

public sealed class SystemStatusSettings : CommandSettings
{
}

[Description("Show comprehensive system status")]
public sealed class SystemStatusCommand : AsyncCommand<SystemStatusSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly ILogger<SystemStatusCommand> _logger;

    public SystemStatusCommand(IVegaResourcesMonitor systemMonitor, ILogger<SystemStatusCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SystemStatusSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var memory = await _systemMonitor.GetMemoryInfoAsync();
            var crashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();
            var watchmanRunning = await _systemMonitor.IsWatchmanRunningAsync();

            var rule = new Rule("[bold]Vega Development Environment Status[/]")
                .RuleStyle("grey");
            AnsiConsole.Write(rule);

            var memoryTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Blue)
                .Title("Memory Status");

            memoryTable.AddColumn("[bold]Metric[/]");
            memoryTable.AddColumn("[bold]Value[/]");

            memoryTable.AddRow("Available", $"{memory.AvailableMemoryMB}MB");
            memoryTable.AddRow("Free", $"{memory.FreeMemoryMB}MB");
            memoryTable.AddRow("Inactive", $"{memory.InactiveMemoryMB}MB");
            memoryTable.AddRow("Active", $"{memory.ActiveMemoryMB}MB");

            var memoryStatus = memory.IsCriticalMemory ? "[red]CRITICAL: Very low memory![/]" :
                             memory.IsLowMemory ? "[yellow]WARNING: Low memory detected[/]" :
                             "[green]Healthy/Green[/]";
            memoryTable.AddRow("Health", memoryStatus);

            AnsiConsole.Write(memoryTable);
            AnsiConsole.WriteLine();

            var processTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
                .Title("Process Status");

            processTable.AddColumn("[bold]Process Type[/]");
            processTable.AddColumn("[bold]Status[/]");

            processTable.AddRow("Crashpad Processes", crashpadCount.ToString());
            processTable.AddRow("Watchman Running", watchmanRunning ? "[green]Yes[/]" : "[red]No[/]");

            if (crashpadCount > 0)
            {
                processTable.AddRow("Process Health", "[yellow]WARNING: Orphaned processes detected[/]");
                
                var processes = await _systemMonitor.GetCrashpadProcessDetailsAsync();
                var processPanel = new Panel(string.Join("\n", processes.Take(3).Select(p =>
                {
                    var parts = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length > 1 ? $"PID {parts[1]}: {parts[^1]}" : p;
                }).Concat(processes.Count > 3 ? new[] { $"... and {processes.Count - 3} more" } : Array.Empty<string>())))
                {
                    Header = new PanelHeader("Orphaned Processes"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow)
                };
                
                AnsiConsole.Write(processTable);
                AnsiConsole.WriteLine();
                AnsiConsole.Write(processPanel);
            }
            else
            {
                processTable.AddRow("Process Health", "[green]No orphaned processes[/]");
                AnsiConsole.Write(processTable);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system status");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public sealed class SystemCleanupSettings : CommandSettings
{
    [CommandOption("--force")]
    [Description("Force cleanup of all processes")]
    [DefaultValue(false)]
    public bool Force { get; set; } = false;
}

[Description("Clean up orphaned processes and resources")]
public sealed class SystemCleanupCommand : AsyncCommand<SystemCleanupSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<SystemCleanupCommand> _logger;

    public SystemCleanupCommand(IVegaResourcesMonitor systemMonitor, IVegaDeviceManager deviceManager, ILogger<SystemCleanupCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SystemCleanupSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanupType = settings.Force ? "Force" : "Smart";
            AnsiConsole.MarkupLine($"[cyan]{cleanupType} cleanup starting...[/]");

            // Check device status first
            VirtualDeviceStatus? deviceStatus = null;
            bool deviceIsRunning = false;
            
            try
            {
                deviceStatus = await _deviceManager.GetStatusAsync(cancellationToken);
                deviceIsRunning = deviceStatus.Running;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine device status, proceeding with caution");
            }

            var initialCrashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();
            var watchmanRunning = await _systemMonitor.IsWatchmanRunningAsync();
            var deviceProcessCount = await GetDeviceProcessCountAsync(cancellationToken);
            
            var totalProcessesToClean = initialCrashpadCount + (watchmanRunning ? 1 : 0);
            
            AnsiConsole.MarkupLine($"Process Analysis:");
            AnsiConsole.MarkupLine($"  Device Status: {(deviceIsRunning ? "[green]Running[/]" : "[red]Not Running[/]")}");
            AnsiConsole.MarkupLine($"  Crashpad processes: [yellow]{initialCrashpadCount}[/]");
            AnsiConsole.MarkupLine($"  Watchman running: {(watchmanRunning ? "[yellow]Yes[/]" : "[green]No[/]")}");
            AnsiConsole.MarkupLine($"  Device processes: {deviceProcessCount}");
            AnsiConsole.WriteLine();

            // If device is running and not force, warn user and skip device cleanup
            if (deviceIsRunning && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]WARNING: Device is running. Skipping device process cleanup to avoid device shutdown.[/]");
                AnsiConsole.MarkupLine($"[grey]Use --force flag if you want to clean up device processes anyway.[/]");
            }

            // If only minimal processes and not force, ask user for confirmation
            if (!settings.Force && totalProcessesToClean <= 2 && totalProcessesToClean > 0)
            {
                var processTypes = new List<string>();
                if (initialCrashpadCount > 0) processTypes.Add($"{initialCrashpadCount} crashpad");
                if (watchmanRunning) processTypes.Add("watchman");
                
                var processDescription = string.Join(" and ", processTypes);
                
                var shouldContinue = AnsiConsole.Confirm(
                    $"Only {processDescription} process(es) found. Continue with cleanup anyway?");
                    
                if (!shouldContinue)
                {
                    AnsiConsole.MarkupLine("[grey]Cleanup cancelled by user[/]");
                    return 0;
                }
            }

            // Perform cleanup based on device status
            if (initialCrashpadCount > 0)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("green"))
                    .StartAsync("Cleaning up processes...", async ctx =>
                    {
                        await _systemMonitor.CleanupCrashpadProcessesAsync(settings.Force, deviceIsRunning, cancellationToken);
                        await Task.Delay(2000);
                    });

                var finalCount = await _systemMonitor.GetCrashpadProcessCountAsync();
                
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(finalCount == 0 ? Color.Green : Color.Yellow);

                table.AddColumn("[bold]Before[/]");
                table.AddColumn("[bold]After[/]");
                table.AddColumn("[bold]Result[/]");

                var result = finalCount == 0 ? "[green]Success[/]" : 
                           finalCount < initialCrashpadCount ? "[yellow]Partial[/]" : "[red]Failed[/]";

                table.AddRow(initialCrashpadCount.ToString(), finalCount.ToString(), result);
                AnsiConsole.Write(table);

                if (finalCount == 0)
                {
                    AnsiConsole.MarkupLine("[green]All processes cleaned up successfully[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]WARNING: {finalCount} processes remain (may require manual intervention)[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]No crashpad processes found to clean up[/]");
            }

            if (watchmanRunning)
            {
                AnsiConsole.MarkupLine("Cleaning up Watchman processes...");
                await _systemMonitor.KillWatchmanAsync();
                AnsiConsole.MarkupLine("[green]Watchman processes terminated[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task<int> GetDeviceProcessCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var processes = await _systemMonitor.GetVirtualDeviceProcessDetailsAsync(cancellationToken);
            return processes.Count;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}

public sealed class SystemMonitorSettings : CommandSettings
{
    [CommandOption("--interval")]
    [Description("Check interval in seconds")]
    [DefaultValue(5)]
    public int Interval { get; set; } = 5;
}

[Description("Monitor memory usage continuously")]
public sealed class SystemMonitorCommand : AsyncCommand<SystemMonitorSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly ILogger<SystemMonitorCommand> _logger;

    public SystemMonitorCommand(IVegaResourcesMonitor systemMonitor, ILogger<SystemMonitorCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SystemMonitorSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule($"[bold]Memory Monitor (interval: {settings.Interval}s)[/]")
                .RuleStyle("grey");
            AnsiConsole.Write(rule);
            
            AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]");
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Time[/]");
            table.AddColumn("[bold]Available MB[/]");
            table.AddColumn("[bold]Free MB[/]");
            table.AddColumn("[bold]Status[/]");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await foreach (var memory in _systemMonitor.MonitorMemoryContinuously(
                TimeSpan.FromSeconds(settings.Interval), cts.Token))
            {
                var status = memory.IsCriticalMemory ? "[red]CRITICAL[/]" :
                           memory.IsLowMemory ? "[yellow]LOW[/]" : "[green]OK[/]";

                var time = DateTime.Now.ToString("HH:mm:ss");
                
                table.AddRow(time, memory.AvailableMemoryMB.ToString(), memory.FreeMemoryMB.ToString(), status);
                
                AnsiConsole.Clear();
                AnsiConsole.Write(rule);
                AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.Write(table);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Monitor stopped.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitor failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}