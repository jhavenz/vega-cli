using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Project;
using VegaDevCli.Domain.Proxy;

namespace VegaDevCli.Commands;

[Description("Network proxy management for debugging")]
public sealed class ProxyCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega proxy --help' to see available subcommands");
        return 0;
    }
}

public sealed class ProxyStartSettings : CommandSettings
{
    [CommandOption("--port")]
    [Description("Proxy port number")]
    [DefaultValue(8888)]
    public int Port { get; set; } = 8888;

    [CommandOption("--auto-config")]
    [Description("Automatically configure Charles settings")]
    [DefaultValue(true)]
    public bool AutoConfig { get; set; } = true;
}

[Description("Start Charles Proxy with Fire TV configuration")]
public sealed class ProxyStartCommand : AsyncCommand<ProxyStartSettings>
{
    private readonly IVegaProjectManager _projectManager;
    private readonly ILogger<ProxyStartCommand> _logger;

    public ProxyStartCommand(IVegaProjectManager projectManager, ILogger<ProxyStartCommand> logger)
    {
        _projectManager = projectManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ProxyStartSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Starting Charles Proxy...", async ctx =>
                {
                    var proxyManager = new CharlesProxyManager(_projectManager);
                    await proxyManager.StartAsync(settings.Port, settings.AutoConfig);
                });

            AnsiConsole.MarkupLine($"[green]SUCCESS:[/] Charles Proxy started on port {settings.Port}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Charles Proxy");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class ProxyStopSettings : CommandSettings
{
}

[Description("Stop Charles Proxy and clean up configuration")]
public sealed class ProxyStopCommand : AsyncCommand<ProxyStopSettings>
{
    private readonly IVegaProjectManager _projectManager;
    private readonly ILogger<ProxyStopCommand> _logger;

    public ProxyStopCommand(IVegaProjectManager projectManager, ILogger<ProxyStopCommand> logger)
    {
        _projectManager = projectManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ProxyStopSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync("Stopping Charles Proxy...", async ctx =>
                {
                    var proxyManager = new CharlesProxyManager(_projectManager);
                    await proxyManager.StopAsync();
                });

            AnsiConsole.MarkupLine("[green]SUCCESS:[/] Charles Proxy stopped and cleaned up");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Charles Proxy");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class ProxyStatusSettings : CommandSettings
{
}

[Description("Show Charles Proxy status and configuration")]
public sealed class ProxyStatusCommand : AsyncCommand<ProxyStatusSettings>
{
    private readonly IVegaProjectManager _projectManager;
    private readonly ILogger<ProxyStatusCommand> _logger;

    public ProxyStatusCommand(IVegaProjectManager projectManager, ILogger<ProxyStatusCommand> logger)
    {
        _projectManager = projectManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ProxyStatusSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var proxyManager = new CharlesProxyManager(_projectManager);
            await proxyManager.ShowStatusAsync();
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get proxy status");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class ProxyConfigureSettings : CommandSettings
{
    [CommandOption("--port")]
    [Description("Proxy port number")]
    [DefaultValue(8888)]
    public int Port { get; set; } = 8888;

    [CommandOption("--enable-ssl")]
    [Description("Enable SSL proxying for localhost:8092")]
    [DefaultValue(true)]
    public bool EnableSsl { get; set; } = true;

    [CommandOption("--transparent")]
    [Description("Enable transparent HTTP proxying")]
    [DefaultValue(true)]
    public bool Transparent { get; set; } = true;
}

[Description("Configure Charles Proxy settings for Fire TV development")]
public sealed class ProxyConfigureCommand : AsyncCommand<ProxyConfigureSettings>
{
    private readonly IVegaProjectManager _projectManager;
    private readonly ILogger<ProxyConfigureCommand> _logger;

    public ProxyConfigureCommand(IVegaProjectManager projectManager, ILogger<ProxyConfigureCommand> logger)
    {
        _projectManager = projectManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ProxyConfigureSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Configuring Charles Proxy...", async ctx =>
                {
                    var proxyManager = new CharlesProxyManager(_projectManager);
                    await proxyManager.ConfigureAsync(settings.Port, settings.EnableSsl, settings.Transparent);
                });

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Setting[/]");
            table.AddColumn("[bold]Value[/]");

            table.AddRow("Port", settings.Port.ToString());
            table.AddRow("SSL Proxying", settings.EnableSsl ? "[green]Enabled[/]" : "[red]Disabled[/]");
            table.AddRow("Transparent Proxy", settings.Transparent ? "[green]Enabled[/]" : "[red]Disabled[/]");
            table.AddRow("SSL Location", "localhost:8092");

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[green]SUCCESS:[/] Charles configuration complete");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Charles Proxy");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}