using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Domain.Build;

namespace VegaDevCli.Commands;

[Description("Build and deploy the Vega application")]
public sealed class BuildCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega build --help' to see available subcommands");
        return 0;
    }
}

public sealed class BuildDebugSettings : CommandSettings
{
}

[Description("Build debug version")]
public sealed class BuildDebugCommand : AsyncCommand<BuildDebugSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<BuildDebugCommand> _logger;

    public BuildDebugCommand(IVegaBuildManager buildManager, ILogger<BuildDebugCommand> logger)
    {
        _buildManager = buildManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BuildDebugSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            BuildResult result = new();
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Building debug version...", async ctx =>
                {
                    result = await _buildManager.BuildAsync(BuildType.Debug);
                });

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]SUCCESS: Debug build completed successfully in {result.Duration}[/]");

                if (result.ArtifactPaths.Count > 0)
                {
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Green);
                    
                    table.AddColumn("[bold]Build Artifacts[/]");
                    
                    foreach (var artifact in result.ArtifactPaths)
                    {
                        table.AddRow(Path.GetFileName(artifact));
                    }
                    
                    AnsiConsole.Write(table);
                }
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]FAILED: Debug build failed: {result.ErrorMessage}[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}


public sealed class BuildCleanSettings : CommandSettings
{
}

[Description("Clean build artifacts")]
public sealed class BuildCleanCommand : AsyncCommand<BuildCleanSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<BuildCleanCommand> _logger;

    public BuildCleanCommand(IVegaBuildManager buildManager, ILogger<BuildCleanCommand> logger)
    {
        _buildManager = buildManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BuildCleanSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _buildManager.CleanAsync();

            if (success)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS: Build artifacts cleaned successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]WARNING: Clean completed with warnings[/]");
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clean failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}


public sealed class BuildReleaseSettings : CommandSettings
{
}

[Description("Build release version")]
public sealed class BuildReleaseCommand : AsyncCommand<BuildReleaseSettings>
{
    private readonly IVegaBuildManager _buildManager;
    private readonly ILogger<BuildReleaseCommand> _logger;

    public BuildReleaseCommand(IVegaBuildManager buildManager, ILogger<BuildReleaseCommand> logger)
    {
        _buildManager = buildManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BuildReleaseSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            BuildResult result = new();
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Building release version...", async ctx =>
                {
                    result = await _buildManager.BuildAsync(BuildType.Release);
                });

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]SUCCESS: Release build completed successfully in {result.Duration}[/]");

                if (result.ArtifactPaths.Count > 0)
                {
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Green);
                    
                    table.AddColumn("[bold]Build Artifacts[/]");
                    
                    foreach (var artifact in result.ArtifactPaths)
                    {
                        table.AddRow(Path.GetFileName(artifact));
                    }
                    
                    AnsiConsole.Write(table);
                }
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]FAILED: Release build failed: {result.ErrorMessage}[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed with exception");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}