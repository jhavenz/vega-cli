using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.CpuBound;
using VegaDevCli.Domain.Devices;

namespace VegaDevCli.Commands;

[Description("Development session management")]
public sealed class SessionCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega session --help' to see available subcommands");
        return 0;
    }
}

public sealed class SessionStartSettings : CommandSettings
{
}

[Description("Start complete development session")]
public sealed class SessionStartCommand : AsyncCommand<SessionStartSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<SessionStartCommand> _logger;

    public SessionStartCommand(IVegaResourcesMonitor systemMonitor, IVegaDeviceManager deviceManager, ILogger<SessionStartCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SessionStartSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold green]Starting Development Session[/]")
                .RuleStyle("green");
            AnsiConsole.Write(rule);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Starting development session...", async ctx =>
                {
                    ctx.Status = "Checking system status...";
                    var memory = await _systemMonitor.GetMemoryInfoAsync();
                    var crashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();
                    
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Grey);

                    table.AddColumn("[bold]Component[/]");
                    table.AddColumn("[bold]Status[/]");
                    table.AddColumn("[bold]Details[/]");

                    table.AddRow("Memory", 
                        memory.IsCriticalMemory ? "[red]Critical[/]" : memory.IsLowMemory ? "[yellow]Low[/]" : "[green]OK[/]", 
                        $"{memory.FreeMemoryMB}MB free");
                    table.AddRow("Processes", 
                        crashpadCount > 0 ? "[yellow]Cleanup needed[/]" : "[green]Clean[/]", 
                        $"{crashpadCount} crashpad processes");

                    AnsiConsole.Write(table);

                    if (memory.IsCriticalMemory)
                    {
                        AnsiConsole.MarkupLine("[yellow]WARNING:[/] Low memory detected");
                    }

                    if (crashpadCount > 0)
                    {
                        ctx.Status = "Cleaning up orphaned processes...";
                        await _systemMonitor.CleanupCrashpadProcessesAsync(false);
                        AnsiConsole.MarkupLine("[green]Process cleanup completed[/]");
                    }

                    ctx.Status = "Starting virtual device...";
                    var deviceStarted = await _deviceManager.StartAsync();
                    
                    if (!deviceStarted)
                    {
                        throw new Exception("Failed to start virtual device");
                    }

                    ctx.Status = "Verifying device status...";
                    var status = await _deviceManager.GetStatusAsync();
                    
                    if (!status.Running)
                    {
                        throw new Exception("Virtual device failed to start properly");
                    }

                    AnsiConsole.MarkupLine($"[green]Virtual device running (PID: {status.ProcessId?.Qemu})[/]");
                });

            var panel = new Panel(
                new Markup("[green]Development environment ready![/]\n\n" +
                          "[bold]Next steps:[/]\n" +
                          "  [cyan]vega dev[/]         Complete build + install + launch\n" +
                          "  [cyan]vega metro-start[/] Start hot reload development\n" +
                          "  [cyan]vega quick[/]       Quick iteration cycle\n" +
                          "  [cyan]vega session-stop[/] End development session"))
                .Header("[bold green]Session Ready[/]")
                .BorderColor(Color.Green);

            AnsiConsole.Write(panel);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start development session");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Session start failed: {ex.Message}");
            return 1;
        }
    }
}

public sealed class SessionStopSettings : CommandSettings
{
}

[Description("Stop development session and cleanup")]
public sealed class SessionStopCommand : AsyncCommand<SessionStopSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<SessionStopCommand> _logger;

    public SessionStopCommand(IVegaResourcesMonitor systemMonitor, IVegaDeviceManager deviceManager, ILogger<SessionStopCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SessionStopSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold yellow]Ending Development Session[/]")
                .RuleStyle("yellow");
            AnsiConsole.Write(rule);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync("Stopping development session...", async ctx =>
                {
                    ctx.Status = "Stopping virtual device...";
                    var deviceStopped = await _deviceManager.StopAsync();
                    AnsiConsole.MarkupLine(deviceStopped ? 
                        "[green]Virtual device stopped successfully[/]" : 
                        "[yellow]Virtual device stop completed with warnings[/]");

                    ctx.Status = "Cleaning up all processes...";
                    await _systemMonitor.CleanupCrashpadProcessesAsync(true);
                    AnsiConsole.MarkupLine("[green]Process cleanup completed[/]");

                    ctx.Status = "Checking Watchman status...";
                    if (await _systemMonitor.IsWatchmanRunningAsync())
                    {
                        await _systemMonitor.KillWatchmanAsync();
                        AnsiConsole.MarkupLine("[green]Watchman stopped[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[green]Watchman not running[/]");
                    }
                });

            var finalCrashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();
            var memory = await _systemMonitor.GetMemoryInfoAsync();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Final Status[/]");
            table.AddColumn("[bold]Value[/]");

            table.AddRow("Remaining processes", finalCrashpadCount.ToString());
            table.AddRow("Available memory", $"{memory.FreeMemoryMB}MB");

            AnsiConsole.Write(table);

            if (finalCrashpadCount == 0)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS:[/] Development session ended cleanly!");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]WARNING:[/] Development session ended with {finalCrashpadCount} remaining processes");
                AnsiConsole.MarkupLine("These may require manual cleanup if they persist");
            }

            return finalCrashpadCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop development session");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Session stop failed: {ex.Message}");
            return 1;
        }
    }
}

public sealed class SessionStatusSettings : CommandSettings
{
}

[Description("Show current development session status")]
public sealed class SessionStatusCommand : AsyncCommand<SessionStatusSettings>
{
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<SessionStatusCommand> _logger;

    public SessionStatusCommand(IVegaResourcesMonitor systemMonitor, IVegaDeviceManager deviceManager, ILogger<SessionStatusCommand> logger)
    {
        _systemMonitor = systemMonitor;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SessionStatusSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceStatus = await _deviceManager.GetStatusAsync();
            var memory = await _systemMonitor.GetMemoryInfoAsync();
            var crashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();
            var watchmanRunning = await _systemMonitor.IsWatchmanRunningAsync();

            var rule = new Rule("[bold blue]Development Session Status[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            var deviceTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Title("[bold]Device Status[/]");

            deviceTable.AddColumn("[bold]Component[/]");
            deviceTable.AddColumn("[bold]Status[/]");
            deviceTable.AddColumn("[bold]Details[/]");

            deviceTable.AddRow("Virtual Device", 
                deviceStatus.Running ? "[green]Running[/]" : "[red]Stopped[/]",
                deviceStatus.Running && deviceStatus.ProcessId?.Qemu > 0 ? 
                    $"PID: {deviceStatus.ProcessId.Qemu}" : "N/A");

            AnsiConsole.Write(deviceTable);

            var systemTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Title("[bold]System Health[/]");

            systemTable.AddColumn("[bold]Component[/]");
            systemTable.AddColumn("[bold]Status[/]");
            systemTable.AddColumn("[bold]Details[/]");

            systemTable.AddRow("Memory", 
                memory.IsCriticalMemory ? "[red]Critical[/]" : memory.IsLowMemory ? "[yellow]Low[/]" : "[green]OK[/]", 
                $"{memory.FreeMemoryMB}MB free");
            systemTable.AddRow("Crashpad processes", 
                crashpadCount > 0 ? "[yellow]Present[/]" : "[green]Clean[/]", 
                crashpadCount.ToString());
            systemTable.AddRow("Watchman", 
                watchmanRunning ? "[green]Running[/]" : "[grey]Stopped[/]", 
                "File watcher");

            AnsiConsole.Write(systemTable);

            var buildTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Title("[bold]Build Status[/]");

            buildTable.AddColumn("[bold]Component[/]");
            buildTable.AddColumn("[bold]Status[/]");

            buildTable.AddRow("Build artifacts", 
                File.Exists("buildinfo.json") ? "[green]Present[/]" : "[red]Missing[/]");

            AnsiConsole.Write(buildTable);

            var isReady = deviceStatus.Running && !memory.IsCriticalMemory && crashpadCount < 3;

            var statusPanel = new Panel(
                new Markup($"[bold]{(isReady ? "[green]Ready for development[/]" : "[yellow]Needs attention[/]")}[/]"))
                .Header($"[bold]{(isReady ? "green" : "yellow")}Session Status[/]")
                .BorderColor(isReady ? Color.Green : Color.Yellow);

            AnsiConsole.Write(statusPanel);

            if (!isReady)
            {
                var recommendations = new List<string>();
                if (!deviceStatus.Running)
                    recommendations.Add("  Start virtual device: [cyan]vega device-start[/]");
                if (memory.IsCriticalMemory)
                    recommendations.Add("  Free up system memory");
                if (crashpadCount >= 3)
                    recommendations.Add("  Clean up processes: [cyan]vega system-cleanup[/]");

                if (recommendations.Any())
                {
                    var recPanel = new Panel(string.Join("\n", recommendations))
                        .Header("[bold yellow]Recommendations[/]")
                        .BorderColor(Color.Yellow);
                    AnsiConsole.Write(recPanel);
                }
            }

            return isReady ? 0 : 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session status");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}