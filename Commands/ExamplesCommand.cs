using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace VegaDevCli.Commands;

public sealed class ExamplesSettings : CommandSettings
{
}

[Description("Show usage examples")]
public sealed class ExamplesCommand : Command<ExamplesSettings>
{
    public override int Execute(CommandContext context, ExamplesSettings settings, CancellationToken cancellationToken = default)
    {
        var rule = new Rule("[bold]Vega Development CLI - Usage Examples[/]")
            .RuleStyle("grey");
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        ShowSessionManagement();
        AnsiConsole.WriteLine();

        ShowDevelopmentWorkflow();
        AnsiConsole.WriteLine();

        ShowBuildOperations();
        AnsiConsole.WriteLine();

        ShowTestingAndQuality();
        AnsiConsole.WriteLine();

        ShowSystemManagement();
        AnsiConsole.WriteLine();

        ShowDeviceManagement();
        AnsiConsole.WriteLine();

        ShowCompleteWorkflows();
        AnsiConsole.WriteLine();

        ShowEnvironmentVariables();

        return 0;
    }

    private static void ShowSessionManagement()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Title("Session Management");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega session-start[/]", "Complete development environment setup");
        table.AddRow("[cyan]vega session-stop[/]", "Clean shutdown and cleanup");
        table.AddRow("[cyan]vega session-status[/]", "Check current session health");

        AnsiConsole.Write(table);
    }

    private static void ShowDevelopmentWorkflow()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title("Development Workflow");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega dev[/]", "Complete build + install + launch cycle");
        table.AddRow("[cyan]vega dev --type release[/]", "Development cycle with release build");
        table.AddRow("[cyan]vega launch[/]", "Launch app on device");

        AnsiConsole.Write(table);
    }

    private static void ShowBuildOperations()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Title("Build Operations");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega build-debug[/]", "Build debug version with monitoring");
        table.AddRow("[cyan]vega build-release[/]", "Build release version");
        table.AddRow("[cyan]vega build-clean[/]", "Clean all build artifacts");
        table.AddRow("[cyan]vega install --type debug[/]", "Install debug build");
        table.AddRow("[cyan]vega install --type release[/]", "Install release build");
        table.AddRow("[cyan]vega launch[/]", "Launch app on device");

        AnsiConsole.Write(table);
    }

    private static void ShowTestingAndQuality()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Magenta)
            .Title("Testing & Quality");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega test-run[/]", "Run all tests");
        table.AddRow("[cyan]vega test-run --watch[/]", "Run tests in watch mode");
        table.AddRow("[cyan]vega typecheck[/]", "TypeScript type checking");
        table.AddRow("[cyan]vega lint[/]", "Lint code");
        table.AddRow("[cyan]vega lint --fix[/]", "Lint and fix issues");
        table.AddRow("[cyan]vega format[/]", "Format code with Prettier");
        table.AddRow("[cyan]vega quality-check[/]", "Run all quality checks");

        AnsiConsole.Write(table);
    }

    private static void ShowSystemManagement()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Red)
            .Title("System Management");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega system-status[/]", "Comprehensive system health check");
        table.AddRow("[cyan]vega system-cleanup[/]", "Smart cleanup of orphaned processes");
        table.AddRow("[cyan]vega system-cleanup --force[/]", "Force cleanup all processes");
        table.AddRow("[cyan]vega system-monitor[/]", "Real-time memory monitoring");
        table.AddRow("[cyan]vega system-monitor --interval 10[/]", "Monitor with custom interval");

        AnsiConsole.Write(table);
    }

    private static void ShowDeviceManagement()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan)
            .Title("Device Management");

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]vega device-start[/]", "Start virtual device with monitoring");
        table.AddRow("[cyan]vega device-stop[/]", "Stop virtual device cleanly");
        table.AddRow("[cyan]vega device-restart[/]", "Restart virtual device");
        table.AddRow("[cyan]vega device-restart --cleanup-proxy false[/]", "Restart without proxy cleanup");
        table.AddRow("[cyan]vega device-status[/]", "Show device and system status");

        AnsiConsole.Write(table);
    }

    private static void ShowCompleteWorkflows()
    {
        var panel = new Panel("""
        [bold]Full development session[/]
        vega session-start             # Setup everything
        vega dev                       # Build and run your app
        
        [bold]Quality assurance workflow[/]
        vega quality-check             # All quality checks
        vega build-release             # Production build
        
        [bold]Quick debugging workflow[/]
        vega system-status             # Check system health
        vega device-restart            # Fresh device start
        vega dev --type debug          # Build and deploy
        """)
        {
            Header = new PanelHeader("Complete Workflows"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };

        AnsiConsole.Write(panel);
    }

    private static void ShowEnvironmentVariables()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("Environment Variables");

        table.AddColumn("[bold]Variable[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("KEPLER_PATH", "Override Kepler SDK path");
        table.AddRow("VEGA_LOG_LEVEL", "Set logging level (debug/info/warn/error)");

        AnsiConsole.Write(table);
    }
}