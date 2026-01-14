using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Build;

namespace VegaDevCli.Commands;

public sealed class LaunchSettings : CommandSettings
{
}

[Description("Launch app on virtual device")]
public sealed class LaunchCommand : AsyncCommand<LaunchSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<LaunchCommand> _logger;

    public LaunchCommand(IVegaBuildManager buildManager, ILogger<LaunchCommand> logger)
    {
        _buildManager = buildManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, LaunchSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _buildManager.LaunchAppAsync();

            if (success)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS: App launched successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]FAILED: Failed to launch app[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}