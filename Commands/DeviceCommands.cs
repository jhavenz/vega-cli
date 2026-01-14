using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.CpuBound;
using VegaDevCli.Domain.Devices;
using VegaDevCli.Domain.Project;
using VegaDevCli.Domain.Proxy;

namespace VegaDevCli.Commands;

[Description("Manage Vega virtual device")]
public sealed class DeviceCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega device --help' to see available subcommands");
        return 0;
    }
}

public sealed class DeviceStatusSettings : CommandSettings
{
}

[Description("Show virtual device status")]
public sealed class DeviceStatusCommand : AsyncCommand<DeviceStatusSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly ILogger<DeviceStatusCommand> _logger;

    public DeviceStatusCommand(IVegaDeviceManager deviceManager, IVegaResourcesMonitor systemMonitor, ILogger<DeviceStatusCommand> logger)
    {
        _deviceManager = deviceManager;
        _systemMonitor = systemMonitor;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DeviceStatusSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _deviceManager.GetStatusAsync();
            var memory = await _systemMonitor.GetMemoryInfoAsync();
            var crashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Property[/]");
            table.AddColumn("[bold]Value[/]");

            table.AddRow("Virtual Device Running", status.Running ? "[green]Yes[/]" : "[red]No[/]");
            
            if (status.ProcessId?.Qemu > 0)
            {
                table.AddRow("Process ID", status.ProcessId.Qemu.ToString());
            }

            table.AddRow("Available Memory", $"{memory.AvailableMemoryMB}MB");
            table.AddRow("Free Memory", $"{memory.FreeMemoryMB}MB");
            table.AddRow("Inactive Memory", $"{memory.InactiveMemoryMB}MB");
            table.AddRow("Active Memory", $"{memory.ActiveMemoryMB}MB");
            table.AddRow("Crashpad Processes", crashpadCount.ToString());

            AnsiConsole.Write(table);

            if (memory.IsLowMemory)
            {
                AnsiConsole.MarkupLine("[yellow]WARNING: Low memory detected![/]");
            }

            if (crashpadCount > 0)
            {
                AnsiConsole.MarkupLine("[yellow]WARNING: Orphaned crashpad processes detected![/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device status");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public sealed class DeviceLogInfoSettings : CommandSettings
{
}

[Description("Get detailed log information from the device")]
public sealed class DeviceLogInfoCommand : AsyncCommand<DeviceLogInfoSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<DeviceLogInfoCommand> _logger;

    public DeviceLogInfoCommand(IVegaDeviceManager deviceManager, ILogger<DeviceLogInfoCommand> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DeviceLogInfoSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _deviceManager.GetLogInfoAsync(cancellationToken);

            if (result.Success)
            {
                var rule = new Rule("[bold blue]Kepler Device Log Information[/]")
                    .RuleStyle("blue");
                AnsiConsole.Write(rule);

                Console.WriteLine(result.Output);
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/]");
                Console.WriteLine(result.Error);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get log info");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public sealed class DeviceStartSettings : CommandSettings
{
}

[Description("Start the virtual device")]
public sealed class DeviceStartCommand : AsyncCommand<DeviceStartSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<DeviceStartCommand> _logger;

    public DeviceStartCommand(IVegaDeviceManager deviceManager, ILogger<DeviceStartCommand> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DeviceStartSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Starting virtual device...", async ctx =>
                {
                    var success = await _deviceManager.StartAsync();
                    if (success)
                    {
                        ctx.Status("Virtual device started successfully");
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("green"));
                        await Task.Delay(1000);
                    }
                });

            var success = await _deviceManager.StartAsync();
            if (success)
            {
                AnsiConsole.MarkupLine("[green]Virtual device started successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to start virtual device[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start device");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public sealed class DeviceStopSettings : CommandSettings
{
}

[Description("Stop the virtual device")]
public sealed class DeviceStopCommand : AsyncCommand<DeviceStopSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<DeviceStopCommand> _logger;

    public DeviceStopCommand(IVegaDeviceManager deviceManager, ILogger<DeviceStopCommand> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DeviceStopSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _deviceManager.StopAsync();
            if (success)
            {
                AnsiConsole.MarkupLine("[green]Virtual device stopped successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Virtual device stop completed with warnings[/]");
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop device");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public sealed class DeviceRestartSettings : CommandSettings
{
    [CommandOption("--cleanup-proxy")]
    [Description("Also cleanup proxy configuration during restart")]
    [DefaultValue(true)]
    public bool CleanupProxy { get; set; } = true;
}

[Description("Restart the virtual device")]
public sealed class DeviceRestartCommand : AsyncCommand<DeviceRestartSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly IVegaProjectManager _projectManager;
    private readonly ILogger<DeviceRestartCommand> _logger;

    public DeviceRestartCommand(IVegaDeviceManager deviceManager, IVegaProjectManager projectManager, ILogger<DeviceRestartCommand> logger)
    {
        _deviceManager = deviceManager;
        _projectManager = projectManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DeviceRestartSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            if (settings.CleanupProxy)
            {
                AnsiConsole.MarkupLine("Cleaning up proxy configuration...");
                var proxyManager = new CharlesProxyManager(_projectManager);
                await proxyManager.StopAsync();
            }

            var success = await _deviceManager.RestartAsync();
            if (success)
            {
                AnsiConsole.MarkupLine("[green]Virtual device restarted successfully[/]");
                
                if (settings.CleanupProxy)
                {
                    AnsiConsole.MarkupLine("Proxy cleanup completed. Use '[cyan]./vega proxy start[/]' to re-enable debugging");
                }
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to restart virtual device[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart device");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

// Port Forwarding Commands

public sealed class DevicePortForwardSettings : CommandSettings
{
    [CommandArgument(0, "<local_port>")]
    [Description("Local port number")]
    public int LocalPort { get; set; }

    [CommandArgument(1, "<remote_port>")]
    [Description("Remote port number")]
    public int RemotePort { get; set; }
}

[Description("Set up port forwarding to device")]
public sealed class DevicePortForwardCommand : AsyncCommand<DevicePortForwardSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<DevicePortForwardCommand> _logger;

    public DevicePortForwardCommand(IVegaDeviceManager deviceManager, ILogger<DevicePortForwardCommand> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DevicePortForwardSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if device is running
            var deviceStatus = await _deviceManager.GetStatusAsync();
            if (!deviceStatus.Running)
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Virtual device is not running. Start it first with '[cyan]vega device start[/]'");
                return 1;
            }

            AnsiConsole.MarkupLine($"Setting up port forwarding: [cyan]{settings.LocalPort}[/] -> [cyan]{settings.RemotePort}[/]");

            var success = await SetupPortForwardingAsync(settings.LocalPort, settings.RemotePort, cancellationToken);

            if (success)
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Green);

                table.AddColumn("[bold]Setting[/]");
                table.AddColumn("[bold]Value[/]");

                table.AddRow("Local Port", settings.LocalPort.ToString());
                table.AddRow("Remote Port", settings.RemotePort.ToString());
                table.AddRow("Status", "[green]Active[/]");
                table.AddRow("Command Used", $"adb reverse tcp:{settings.LocalPort} tcp:{settings.RemotePort}");

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine("[green]SUCCESS:[/] Port forwarding enabled");
                AnsiConsole.MarkupLine("Use '[cyan]vega device port-forward-stop[/]' to disable port forwarding");

                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Failed to set up port forwarding");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up port forwarding");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> SetupPortForwardingAsync(int localPort, int remotePort, CancellationToken cancellationToken = default)
    {
        try
        {
            var adbPath = FindAdbPath();
            if (string.IsNullOrEmpty(adbPath))
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] ADB not found. Ensure Android SDK is installed and ADB is in PATH");
                return false;
            }

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"reverse tcp:{localPort} tcp:{remotePort}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Executing: {Command} {Arguments}", adbPath, process.StartInfo.Arguments);

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("ADB reverse failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                AnsiConsole.MarkupLine($"[red]ADB Error:[/] {error}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute ADB reverse command");
            return false;
        }
    }

    private string FindAdbPath()
    {
        // Check common locations for ADB
        var paths = new[]
        {
            "adb", // In PATH
            "/usr/local/bin/adb",
            "/opt/homebrew/bin/adb",
            Environment.ExpandEnvironmentVariables("~/Library/Android/sdk/platform-tools/adb"),
            Environment.ExpandEnvironmentVariables("~/Android/Sdk/platform-tools/adb")
        };

        foreach (var path in paths)
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(1000); // 1 second timeout

                if (process.ExitCode == 0)
                {
                    return path;
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return string.Empty;
    }
}

public sealed class DevicePortForwardStopSettings : CommandSettings
{
    [CommandOption("--all")]
    [Description("Remove all port forwarding rules")]
    [DefaultValue(false)]
    public bool All { get; set; } = false;

    [CommandOption("--port")]
    [Description("Specific port to stop forwarding (if not --all)")]
    public int? Port { get; set; }
}

[Description("Stop port forwarding to device")]
public sealed class DevicePortForwardStopCommand : AsyncCommand<DevicePortForwardStopSettings>
{
    private readonly IVegaDeviceManager _deviceManager;
    private readonly ILogger<DevicePortForwardStopCommand> _logger;

    public DevicePortForwardStopCommand(IVegaDeviceManager deviceManager, ILogger<DevicePortForwardStopCommand> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DevicePortForwardStopSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var adbPath = FindAdbPath();
            if (string.IsNullOrEmpty(adbPath))
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] ADB not found. Ensure Android SDK is installed and ADB is in PATH");
                return 1;
            }

            if (settings.All)
            {
                AnsiConsole.MarkupLine("Removing all port forwarding rules...");
                var success = await StopAllPortForwardingAsync(adbPath, cancellationToken);
                
                if (success)
                {
                    AnsiConsole.MarkupLine("[green]SUCCESS:[/] All port forwarding rules removed");
                    return 0;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]ERROR:[/] Failed to remove port forwarding rules");
                    return 1;
                }
            }
            else if (settings.Port.HasValue)
            {
                AnsiConsole.MarkupLine($"Removing port forwarding for port [cyan]{settings.Port.Value}[/]...");
                var success = await StopPortForwardingAsync(adbPath, settings.Port.Value, cancellationToken);
                
                if (success)
                {
                    AnsiConsole.MarkupLine($"[green]SUCCESS:[/] Port forwarding removed for port {settings.Port.Value}");
                    return 0;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]ERROR:[/] Failed to remove port forwarding for port {settings.Port.Value}");
                    return 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]WARNING:[/] Please specify either --all or --port <port_number>");
                AnsiConsole.MarkupLine("Examples:");
                AnsiConsole.MarkupLine("  [cyan]vega device port-forward-stop --all[/]");
                AnsiConsole.MarkupLine("  [cyan]vega device port-forward-stop --port 8092[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop port forwarding");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> StopAllPortForwardingAsync(string adbPath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "reverse --remove-all",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Executing: {Command} {Arguments}", adbPath, process.StartInfo.Arguments);

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("ADB reverse --remove-all failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute ADB reverse --remove-all command");
            return false;
        }
    }

    private async Task<bool> StopPortForwardingAsync(string adbPath, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"reverse --remove tcp:{port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Executing: {Command} {Arguments}", adbPath, process.StartInfo.Arguments);

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("ADB reverse --remove failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute ADB reverse --remove command");
            return false;
        }
    }

    private string FindAdbPath()
    {
        // Check common locations for ADB
        var paths = new[]
        {
            "adb", // In PATH
            "/usr/local/bin/adb",
            "/opt/homebrew/bin/adb",
            Environment.ExpandEnvironmentVariables("~/Library/Android/sdk/platform-tools/adb"),
            Environment.ExpandEnvironmentVariables("~/Android/Sdk/platform-tools/adb")
        };

        foreach (var path in paths)
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(1000); // 1 second timeout

                if (process.ExitCode == 0)
                {
                    return path;
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return string.Empty;
    }
}