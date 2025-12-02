using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace VegaDevCli.Commands;

[Description("Run tests and quality checks")]
public sealed class TestCommand : Command<CommandSettings>
{
    public override int Execute(CommandContext context, CommandSettings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine("Use 'vega test --help' to see available subcommands");
        return 0;
    }
}

public sealed class TestRunSettings : CommandSettings
{
    [CommandOption("--watch")]
    [Description("Run tests in watch mode")]
    [DefaultValue(false)]
    public bool Watch { get; set; } = false;

    [CommandOption("--update-snapshots")]
    [Description("Update test snapshots")]
    [DefaultValue(false)]
    public bool UpdateSnapshots { get; set; } = false;
}

[Description("Run all tests")]
public sealed class TestRunCommand : AsyncCommand<TestRunSettings>
{
    private readonly ILogger<TestRunCommand> _logger;

    public TestRunCommand(ILogger<TestRunCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestRunSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var npmScript = settings.UpdateSnapshots ? "test:snapshot" : 
                           settings.Watch ? "test:watch" : "test";

            var rule = new Rule($"[bold green]🧪 Running Tests ({npmScript})[/]")
                .RuleStyle("green");
            AnsiConsole.Write(rule);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Setting[/]");
            table.AddColumn("[bold]Value[/]");

            table.AddRow("Mode", settings.Watch ? "[blue]Watch[/]" : settings.UpdateSnapshots ? "[yellow]Update Snapshots[/]" : "[green]Single Run[/]");
            table.AddRow("Script", npmScript);

            AnsiConsole.Write(table);

            var success = await RunNpmCommandAsync(npmScript, _logger, cancellationToken);
            
            if (success)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS:[/] Tests completed successfully");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]FAILED:[/] Tests failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string script, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"run {script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            AnsiConsole.MarkupLine($"[red]Error running npm {script}:[/] {ex.Message}");
            return false;
        }
    }
}

public sealed class TestTypecheckSettings : CommandSettings
{
}

[Description("Run TypeScript type checking")]
public sealed class TestTypecheckCommand : AsyncCommand<TestTypecheckSettings>
{
    private readonly ILogger<TestTypecheckCommand> _logger;

    public TestTypecheckCommand(ILogger<TestTypecheckCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestTypecheckSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]🔍 Type checking...[/]");
            
            var success = await RunNpmCommandAsync("typecheck", _logger, cancellationToken);
            
            if (success)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS:[/] Type checking passed");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]FAILED:[/] Type checking failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Typecheck command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string script, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"run {script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            return false;
        }
    }
}

public sealed class TestLintSettings : CommandSettings
{
    [CommandOption("--fix")]
    [Description("Fix linting issues automatically")]
    [DefaultValue(false)]
    public bool Fix { get; set; } = false;
}

[Description("Lint code")]
public sealed class TestLintCommand : AsyncCommand<TestLintSettings>
{
    private readonly ILogger<TestLintCommand> _logger;

    public TestLintCommand(ILogger<TestLintCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestLintSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var script = settings.Fix ? "lint:fix" : "lint";
            
            AnsiConsole.MarkupLine($"[blue]🔍 Linting code ({script})...[/]");
            
            var success = await RunNpmCommandAsync(script, _logger, cancellationToken);
            
            if (success)
            {
                var message = settings.Fix ? "Linting and fixes completed" : "Linting completed";
                AnsiConsole.MarkupLine($"[green]SUCCESS:[/] {message}");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]FAILED:[/] Linting failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lint command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string script, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"run {script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            return false;
        }
    }
}

public sealed class TestFormatSettings : CommandSettings
{
}

[Description("Format code with Prettier")]
public sealed class TestFormatCommand : AsyncCommand<TestFormatSettings>
{
    private readonly ILogger<TestFormatCommand> _logger;

    public TestFormatCommand(ILogger<TestFormatCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestFormatSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]✨ Formatting code...[/]");
            
            var success = await RunNpmCommandAsync("prettier", _logger, cancellationToken);
            
            if (success)
            {
                AnsiConsole.MarkupLine("[green]SUCCESS:[/] Code formatting completed");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]FAILED:[/] Code formatting failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Format command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string script, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"run {script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            return false;
        }
    }
}

public sealed class TestCheckSettings : CommandSettings
{
}

[Description("Run all quality checks (typecheck + lint + test)")]
public sealed class TestCheckCommand : AsyncCommand<TestCheckSettings>
{
    private readonly ILogger<TestCheckCommand> _logger;

    public TestCheckCommand(ILogger<TestCheckCommand> logger)
    {
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TestCheckSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new Rule("[bold blue]🔍 Running All Quality Checks[/]")
                .RuleStyle("blue");
            AnsiConsole.Write(rule);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            table.AddColumn("[bold]Check[/]");
            table.AddColumn("[bold]Status[/]");
            table.AddColumn("[bold]Result[/]");

            table.AddRow("TypeScript", "[yellow]Running...[/]", "");
            table.AddRow("Linting", "[grey]Waiting[/]", "");
            table.AddRow("Tests", "[grey]Waiting[/]", "");

            var liveTable = AnsiConsole.Live(table);

            var allPassed = true;

            await liveTable.StartAsync(async ctx =>
            {
                var checks = new[]
                {
                    ("TypeScript", "typecheck"),
                    ("Linting", "lint"),
                    ("Tests", "test")
                };

                for (int i = 0; i < checks.Length; i++)
                {
                    var (name, script) = checks[i];
                    
                    table.Rows.Update(i, 1, new Markup("[yellow]Running...[/]"));
                    ctx.Refresh();

                    var success = await RunNpmCommandAsync(script, _logger, cancellationToken);
                    
                    if (success)
                    {
                        table.Rows.Update(i, 1, new Markup("[green]Complete[/]"));
                        table.Rows.Update(i, 2, new Markup("[green]Passed[/]"));
                    }
                    else
                    {
                        table.Rows.Update(i, 1, new Markup("[red]Complete[/]"));
                        table.Rows.Update(i, 2, new Markup("[red]Failed[/]"));
                        allPassed = false;
                    }

                    // Update next row to "Running" if there is one
                    if (i + 1 < checks.Length)
                    {
                        table.Rows.Update(i + 1, 1, new Markup("[yellow]Running...[/]"));
                    }

                    ctx.Refresh();
                }
            });

            if (allPassed)
            {
                AnsiConsole.MarkupLine("[green]🎉 All quality checks passed![/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]❌ Some quality checks failed[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check command failed");
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunNpmCommandAsync(string script, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"run {script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                logger.LogError("npm command failed (exit {ExitCode}): {Error}", process.ExitCode, error);
                // Don't output errors for check command to keep table clean
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            return false;
        }
    }
}