using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Build;

namespace VegaDevCli.Commands;

public sealed class DevSettings : CommandSettings
{
    [CommandOption("--type")]
    [Description("Build type (debug or release)")]
    [DefaultValue("debug")]
    public string Type { get; set; } = "debug";
}

[Description("Complete development cycle: build -> install -> launch")]
public sealed class DevCommand : AsyncCommand<DevSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<DevCommand> _logger;

    public DevCommand(IVegaBuildManager buildManager, ILogger<DevCommand> logger)
    {
        _buildManager = buildManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DevSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var buildType = settings.Type.ToLowerInvariant() switch
            {
                "debug" => BuildType.Debug,
                "release" => BuildType.Release,
                _ => throw new ArgumentException($"Invalid build type: {settings.Type}")
            };

            AnsiConsole.MarkupLine($"Starting complete development cycle for [cyan]{buildType}[/]...");

            BuildResult result = new();
            
            result = await _buildManager.BuildInstallLaunchAsync(buildType, cancellationToken);

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]SUCCESS: Complete development cycle completed successfully in {result.Duration}[/]");
                AnsiConsole.MarkupLine("App is now running on the virtual device");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]FAILED: Development cycle failed: {result.ErrorMessage}[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Development cycle failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}