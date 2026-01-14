using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace VegaDevCli.Commands;

[Description("Debugging and analysis tools")]
public sealed class DebugCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega debug --help' to see available subcommands");
        return 0;
    }
}

public sealed class DebugBuildInfoSettings : CommandSettings
{
}

[Description("Show detailed build information")]
public sealed class DebugBuildInfoCommand : AsyncCommand<DebugBuildInfoSettings>
{
    private readonly ILogger<DebugBuildInfoCommand> _logger;

    public DebugBuildInfoCommand(ILogger<DebugBuildInfoCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DebugBuildInfoSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold blue]Build Information[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            if (File.Exists("buildinfo.json"))
            {
                var buildInfoContent = await File.ReadAllTextAsync("buildinfo.json", cancellationToken);
                
                try
                {
                    var buildInfo = JsonSerializer.Deserialize<JsonElement>(buildInfoContent);
                    var formattedJson = JsonSerializer.Serialize(buildInfo, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });

                    var panel = new Panel(formattedJson)
                        .Header("[bold green]buildinfo.json[/]")
                        .BorderColor(Color.Green)
                        .Padding(1, 0);

                    AnsiConsole.Write(panel);
                }
                catch (JsonException)
                {
                    var panel = new Panel(buildInfoContent)
                        .Header("[bold yellow]buildinfo.json (raw)[/]")
                        .BorderColor(Color.Yellow)
                        .Padding(1, 0);

                    AnsiConsole.Write(panel);
                }

                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] No buildinfo.json found");
                AnsiConsole.MarkupLine("Run a build first: [cyan]vega build debug[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show build info");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class DebugAppInfoSettings : CommandSettings
{
}

public sealed class DebugLogInfoSettings : CommandSettings
{
}

[Description("Show application package information")]
public sealed class DebugAppInfoCommand : AsyncCommand<DebugAppInfoSettings>
{
    private readonly ILogger<DebugAppInfoCommand> _logger;

    public DebugAppInfoCommand(ILogger<DebugAppInfoCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DebugAppInfoSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold blue]Application Information[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            var buildDir = "build";
            if (Directory.Exists(buildDir))
            {
                var vpkgFiles = Directory.GetFiles(buildDir, "*.vpkg", SearchOption.AllDirectories);
                
                if (vpkgFiles.Length > 0)
                {
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Green);

                    table.AddColumn("[bold]Package File[/]");
                    table.AddColumn("[bold]Size (KB)[/]");
                    table.AddColumn("[bold]Location[/]");

                    foreach (var vpkgFile in vpkgFiles.Take(10))
                    {
                        var fileInfo = new FileInfo(vpkgFile);
                        var sizeKb = fileInfo.Length / 1024;
                        var relativePath = Path.GetRelativePath(".", Path.GetDirectoryName(vpkgFile)!);

                        table.AddRow(
                            Path.GetFileName(vpkgFile),
                            $"{sizeKb:N0}",
                            relativePath
                        );
                    }

                    AnsiConsole.Write(table);

                    if (vpkgFiles.Length > 10)
                    {
                        AnsiConsole.MarkupLine($"[grey]... and {vpkgFiles.Length - 10} more package files[/]");
                    }

                    AnsiConsole.MarkupLine($"[green]Found {vpkgFiles.Length} package file(s)[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]ERROR:[/] No .vpkg files found");
                    AnsiConsole.MarkupLine("Run a build first: [cyan]vega build debug[/]");
                    return 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Build directory not found");
                AnsiConsole.MarkupLine("Run a build first: [cyan]vega build debug[/]");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show app info");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class DebugBundleSizeSettings : CommandSettings
{
}

[Description("Analyze bundle sizes")]
public sealed class DebugBundleSizeCommand : AsyncCommand<DebugBundleSizeSettings>
{
    private readonly ILogger<DebugBundleSizeCommand> _logger;

    public DebugBundleSizeCommand(ILogger<DebugBundleSizeCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DebugBundleSizeSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold blue]Bundle Size Analysis[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            var buildDir = "build";
            if (Directory.Exists(buildDir))
            {
                var bundleFiles = Directory.GetFiles(buildDir, "*.bundle", SearchOption.AllDirectories);
                
                if (bundleFiles.Length > 0)
                {
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Blue);

                    table.AddColumn("[bold]Bundle File[/]");
                    table.AddColumn("[bold]Size (KB)[/]");
                    table.AddColumn("[bold]Size (MB)[/]");
                    table.AddColumn("[bold]Status[/]");

                    var totalSize = 0L;
                    
                    foreach (var bundleFile in bundleFiles)
                    {
                        var fileInfo = new FileInfo(bundleFile);
                        var sizeKb = fileInfo.Length / 1024.0;
                        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
                        totalSize += fileInfo.Length;
                        
                        var status = sizeKb > 1024 ? "[yellow]Large[/]" : 
                                   sizeKb > 2048 ? "[red]Very Large[/]" : "[green]OK[/]";

                        table.AddRow(
                            Path.GetFileName(bundleFile),
                            $"{sizeKb:F1}",
                            $"{sizeMb:F2}",
                            status
                        );
                    }

                    AnsiConsole.Write(table);

                    var totalMb = totalSize / (1024.0 * 1024.0);
                    var summaryPanel = new Panel(
                        $"[bold]Total bundle size:[/] {totalMb:F2} MB\n" +
                        $"[bold]Number of bundles:[/] {bundleFiles.Length}")
                        .Header("[bold green]Summary[/]")
                        .BorderColor(totalMb > 10 ? Color.Yellow : Color.Green);

                    AnsiConsole.Write(summaryPanel);

                    if (totalSize > 5 * 1024 * 1024) 
                    {
                        AnsiConsole.MarkupLine("[yellow]WARNING:[/] Large bundle size detected - consider code splitting");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]ERROR:[/] No .bundle files found");
                    AnsiConsole.MarkupLine("Run a build first: [cyan]vega build debug[/]");
                    return 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Build directory not found");
                AnsiConsole.MarkupLine("Run a build first: [cyan]vega build debug[/]");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze bundle sizes");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class DebugNativeLogsSettings : CommandSettings
{
    [CommandOption("--tail")]
    [Description("Number of lines to show from each log file")]
    [DefaultValue(20)]
    public int TailLines { get; set; } = 20;
}

[Description("Check native build logs")]
public sealed class DebugNativeLogsCommand : AsyncCommand<DebugNativeLogsSettings>
{
    private readonly ILogger<DebugNativeLogsCommand> _logger;

    public DebugNativeLogsCommand(ILogger<DebugNativeLogsCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DebugNativeLogsSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold blue]Native Build Logs[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            var foundAnyLogs = false;
            var buildDir = "build";

            if (Directory.Exists(buildDir))
            {
                var logFiles = Directory.GetFiles(buildDir, "*.log", SearchOption.AllDirectories);
                
                if (logFiles.Length > 0)
                {
                    foundAnyLogs = true;
                    
                    foreach (var logFile in logFiles)
                    {
                        try
                        {
                            var relativePath = Path.GetRelativePath(".", logFile);
                            var lines = await File.ReadAllLinesAsync(logFile, cancellationToken);
                            var lastLines = lines.TakeLast(settings.TailLines);
                            
                            var logContent = string.Join("\n", lastLines);
                            var panel = new Panel(logContent)
                                .Header($"[bold yellow]{relativePath}[/]")
                                .BorderColor(Color.Yellow);

                            AnsiConsole.Write(panel);

                            if (lines.Length > settings.TailLines)
                            {
                                AnsiConsole.MarkupLine($"[grey]... (showing last {settings.TailLines} of {lines.Length} lines)[/]");
                            }

                            AnsiConsole.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Error reading {logFile}:[/] {ex.Message}");
                        }
                    }
                }
            }

            // Check for Kepler debug logs
            var debugLogs = Directory.GetFiles(".", "kepler-debug-*.log");
            if (debugLogs.Length > 0)
            {
                foundAnyLogs = true;
                
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Blue)
                    .Title("[bold]Kepler Debug Logs[/]");

                table.AddColumn("[bold]Log File[/]");
                table.AddColumn("[bold]Size[/]");
                table.AddColumn("[bold]Modified[/]");

                foreach (var debugLog in debugLogs.Take(5))
                {
                    var fileInfo = new FileInfo(debugLog);
                    table.AddRow(
                        Path.GetFileName(debugLog),
                        $"{fileInfo.Length / 1024} KB",
                        fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                    );
                }

                AnsiConsole.Write(table);

                if (debugLogs.Length > 5)
                {
                    AnsiConsole.MarkupLine($"[grey]... and {debugLogs.Length - 5} more debug logs[/]");
                }
            }

            if (!foundAnyLogs)
            {
                AnsiConsole.MarkupLine("[yellow]WARNING:[/] No build log files found");
                AnsiConsole.MarkupLine("This is normal if no builds have been run yet.");
                AnsiConsole.MarkupLine("Run: [cyan]vega build debug[/] to generate logs");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show native logs");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }
}