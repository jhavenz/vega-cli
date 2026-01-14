using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Build;

namespace VegaDevCli.Commands;

public sealed class InstallSettings : CommandSettings
{
    [CommandOption("--type")]
    [Description("Build type to install (debug or release)")]
    [DefaultValue("debug")]
    public string Type { get; set; } = "debug";
}

[Description("Install app on virtual device")]
public sealed class InstallCommand : AsyncCommand<InstallSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<InstallCommand> _logger;

    public InstallCommand(IVegaBuildManager buildManager, ILogger<InstallCommand> logger)
    {
        _logger = logger;
        _buildManager = buildManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, InstallSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var buildType = settings.Type.ToLowerInvariant() switch
            {
                "debug" => BuildType.Debug,
                "release" => BuildType.Release,
                _ => throw new ArgumentException($"Invalid build type: {settings.Type}")
            };

            var success = await _buildManager.InstallAppAsync(buildType);

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]SUCCESS: {buildType} app installed successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]FAILED: Failed to install {buildType} app[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}