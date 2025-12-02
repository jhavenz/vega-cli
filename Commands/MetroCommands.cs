using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Project;
using VegaDevCli.Domain.Devices;

namespace VegaDevCli.Commands;

[Description("Metro bundler for hot reload development")]
public sealed class MetroStartCommand : AsyncCommand<MetroSettings>
{
    private readonly ILogger<MetroStartCommand> _logger;
    private readonly IVegaProjectManager _projectManager;

    public MetroStartCommand(ILogger<MetroStartCommand> logger, IVegaProjectManager projectManager)
    {
        _logger = logger;
        _projectManager = projectManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, MetroSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            _projectManager.EnsureValidProject();
            var rule = new Rule("[bold blue]Starting Metro Bundler[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            if (settings.ResetCache)
            {
                AnsiConsole.MarkupLine("[yellow]Resetting Metro cache...[/]");
            }

            var arguments = settings.ResetCache ? "start -- --reset-cache" : "start";
            
            if (settings.Port != 8081)
            {
                arguments += $" --port {settings.Port}";
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Setting[/]");
            table.AddColumn("[bold]Value[/]");

            table.AddRow("Port", settings.Port.ToString());
            table.AddRow("Reset Cache", settings.ResetCache ? "[yellow]Yes[/]" : "[green]No[/]");
            table.AddRow("Command", $"npm {arguments}");

            AnsiConsole.Write(table);

            var success = await RunNpmCommandAsync(arguments, _logger, _projectManager, interactive: true);
            
            if (!success)
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Metro bundler failed to start");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metro command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string arguments, ILogger logger, IVegaProjectManager projectManager, bool interactive = false)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = arguments,
                    RedirectStandardOutput = !interactive,
                    RedirectStandardError = !interactive,
                    RedirectStandardInput = !interactive,
                    UseShellExecute = interactive,
                    CreateNoWindow = !interactive,
                    WorkingDirectory = projectManager.ProjectRoot,
                    Environment =
                    {
                        ["WATCHMAN_DISABLE"] = "1"
                    }
                }
            };

            logger.LogDebug("Executing: npm {Arguments}", arguments);

            process.Start();

            if (!interactive)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    logger.LogError("npm command failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        AnsiConsole.WriteLine(error);
                    }
                }
                else if (!string.IsNullOrEmpty(output))
                {
                    AnsiConsole.WriteLine(output);
                }

                return process.ExitCode == 0;
            }
            else
            {
                await process.WaitForExitAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Arguments}", arguments);
            AnsiConsole.MarkupLine($"[red]Error running npm {arguments}:[/] {ex.Message}");
            return false;
        }
    }
}

public sealed class MetroSettings : CommandSettings
{
    [CommandOption("--reset-cache")]
    [Description("Reset Metro cache before starting")]
    [DefaultValue(false)]
    public bool ResetCache { get; set; } = false;

    [CommandOption("--port")]
    [Description("Metro bundler port")]
    [DefaultValue(8081)]
    public int Port { get; set; } = 8081;
}

[Description("Quick development iteration (fast build + install + launch)")]
public sealed class QuickCommand : AsyncCommand<QuickSettings>
{
    private readonly ILogger<QuickCommand> _logger;
    private readonly IVegaProjectManager _projectManager;
    private readonly IKeplerPathResolver _keplerPathResolver;
    private readonly IVegaDeviceManager _deviceManager;

    public QuickCommand(ILogger<QuickCommand> logger, IVegaProjectManager projectManager, IKeplerPathResolver keplerPathResolver, IVegaDeviceManager deviceManager)
    {
        _logger = logger;
        _projectManager = projectManager;
        _keplerPathResolver = keplerPathResolver;
        _deviceManager = deviceManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, QuickSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            _projectManager.EnsureValidProject();
            var rule = new Rule("[bold green]Quick Development Iteration[/]")
                .RuleStyle("green");
            AnsiConsole.Write(rule);

            var buildScript = settings.Type.ToLowerInvariant() == "release" ? "build:release" : "build:debug";
            var buildFlag = settings.Type.ToLowerInvariant() == "release" ? "Release" : "Debug";

            var buildStatus = "[yellow]Pending[/]";
            var deviceStatus = "[grey]Waiting[/]";
            var installStatus = "[grey]Waiting[/]";
            var launchStatus = "[grey]Waiting[/]";

            await AnsiConsole.Live(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus))
                .StartAsync(async ctx =>
                {
                    // Step 1: Build
                    buildStatus = "[yellow]Running...[/]";
                    ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));

                    var buildSuccess = await RunNpmCommandAsync($"run {buildScript}", _logger, _projectManager);
                    
                    if (!buildSuccess)
                    {
                        buildStatus = "[red]Failed[/]";
                        ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                        throw new Exception("Quick build failed");
                    }

                    buildStatus = "[green]Complete[/]";
                    deviceStatus = "[yellow]Running...[/]";
                    ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));

                    // Step 2: Device Check
                    try
                    {
                        var deviceRunning = await _deviceManager.EnsureRunningAsync();
                        if (!deviceRunning)
                        {
                            deviceStatus = "[red]Failed[/]";
                            ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                            throw new Exception("Virtual device failed to start");
                        }
                        
                        deviceStatus = "[green]Complete[/]";
                        installStatus = "[yellow]Running...[/]";
                        ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Device startup failed");
                        deviceStatus = "[red]Failed[/]";
                        ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                        throw new Exception($"Virtual device startup failed: {ex.Message}");
                    }

                    // Step 3: Install
                    var installSuccess = await RunKeplerCommandAsync($"device install-app -b {buildFlag} --dir \"{_projectManager.ProjectRoot}\"", _logger, _projectManager);
                    
                    if (!installSuccess)
                    {
                        installStatus = "[red]Failed[/]";
                        ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                        throw new Exception("Install failed");
                    }

                    installStatus = "[green]Complete[/]";
                    launchStatus = "[yellow]Running...[/]";
                    ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));

                    // Step 4: Launch
                    var launchSuccess = await RunKeplerCommandAsync("device launch-app", _logger, _projectManager);
                    
                    if (!launchSuccess)
                    {
                        launchStatus = "[red]Failed[/]";
                        ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
                        throw new Exception("Launch failed");
                    }

                    launchStatus = "[green]Complete[/]";
                    ctx.UpdateTarget(CreateQuickTable(buildScript, buildFlag, buildStatus, deviceStatus, installStatus, launchStatus));
            });

            AnsiConsole.MarkupLine("[green]SUCCESS:[/] Quick iteration completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private Table CreateQuickTable(string buildScript, string buildFlag, string buildStatus, string deviceStatus, string installStatus, string launchStatus)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn("[bold]Step[/]");
        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Status[/]");

        table.AddRow("Build", $"npm run {buildScript}", buildStatus);
        table.AddRow("Device", "Ensure virtual device running", deviceStatus);
        table.AddRow("Install", $"kepler device install-app -b {buildFlag}", installStatus);
        table.AddRow("Launch", "kepler device launch-app", launchStatus);

        return table;
    }

    private async Task<bool> RunNpmCommandAsync(string arguments, ILogger logger, IVegaProjectManager projectManager, bool interactive = false)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = arguments,
                    RedirectStandardOutput = !interactive,
                    RedirectStandardError = !interactive,
                    RedirectStandardInput = !interactive,
                    UseShellExecute = interactive,
                    CreateNoWindow = !interactive,
                    WorkingDirectory = projectManager.ProjectRoot,
                    Environment =
                    {
                        ["WATCHMAN_DISABLE"] = "1"
                    }
                }
            };

            logger.LogDebug("Executing: npm {Arguments}", arguments);

            process.Start();

            if (!interactive)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    logger.LogError("npm command failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                    return false;
                }

                return process.ExitCode == 0;
            }
            else
            {
                await process.WaitForExitAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Arguments}", arguments);
            return false;
        }
    }

    private async Task<bool> RunKeplerCommandAsync(string arguments, ILogger logger, IVegaProjectManager projectManager)
    {
        string keplerExecutable;
        try
        {
            keplerExecutable = _keplerPathResolver.GetKeplerExecutablePath();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Kepler SDK not found: {Error}", ex.Message);
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return false;
        }

        var keplerDirectory = Path.GetDirectoryName(keplerExecutable) ?? "";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = keplerExecutable,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectManager.ProjectRoot,
                    Environment =
                    {
                        ["PATH"] = $"{keplerDirectory}:{Environment.GetEnvironmentVariable("PATH")}"
                    }
                }
            };

            logger.LogDebug("Executing: {Command} {Arguments}", keplerExecutable, arguments);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                logger.LogError("Kepler command failed (exit {ExitCode}): {Error}", process.ExitCode, error);
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute Kepler command: {Arguments}", arguments);
            return false;
        }
    }
}

public sealed class QuickSettings : CommandSettings
{
    [CommandOption("--type")]
    [Description("Build type (debug or release)")]
    [DefaultValue("debug")]
    public string Type { get; set; } = "debug";
}