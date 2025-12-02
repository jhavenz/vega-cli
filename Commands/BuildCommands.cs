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
        _buildManager = buildManager;
        _logger = logger;
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
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Running development cycle...", async ctx =>
                {
                    result = await _buildManager.BuildInstallLaunchAsync(buildType);
                });

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