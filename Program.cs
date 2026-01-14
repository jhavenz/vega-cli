namespace VegaDevCli;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Cli;
using VegaDevCli.Commands;
using VegaDevCli.DI;
using VegaDevCli.Domain.Build;
using VegaDevCli.Domain.CpuBound;
using VegaDevCli.Domain.Devices;
using VegaDevCli.Domain.Project;

class Program
{
    public const string CategoryList = "Session, Development, Build, Device, System, Metro, Proxy, Debug, Test";

    public static readonly Color PrimaryColor = Color.Teal;

    public static readonly Color SecondaryColor = Color.White;

    public static readonly string[] AllCategories = CategoryList.Split(", ");

    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        ConfigureLogging(services);
        ConfigureServices(services);

        var registrar = new TypeRegistar(services);
        var app = new CommandApp(registrar);

        app.Configure(ConfigureApplication);

        if (args.Length == 0)
        {
            HelpCommand.RenderHelp(null);
            return 0;
        }

        return app.Run(args);
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        var logLevel = Environment.GetEnvironmentVariable("VEGA_LOG_LEVEL") switch
        {
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => LogEventLevel.Warning
        };

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console()
            .WriteTo.File("vegacli.log")
            .CreateLogger();

        services.AddLogging(c => c.AddSerilog(logger));
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IVegaProjectManager, VegaProjectManager>();
        services.AddSingleton<IVegaResourcesMonitor, VegaResourceMonitor>();
        services.AddSingleton<IKeplerPathResolver, KeplerPathResolver>();
        services.AddSingleton<IVegaDeviceManager, VegaDeviceManager>();
        services.AddSingleton<IVegaBuildManager, VegaBuildManager>();
    }

    private static void ConfigureApplication(IConfigurator config)
    {
        config.SetApplicationName("Vega");
        config.SetApplicationVersion("0.1.0");

        ConfigureCommands(config);
    }

    private static void ConfigureCommands(IConfigurator config)
    {
        config.AddCommand<SessionStartCommand>("session-start")
            .WithDescription("Complete development environment setup");
        config.AddCommand<SessionStopCommand>("session-stop")
            .WithDescription("Clean shutdown and cleanup");
        config.AddCommand<SessionStatusCommand>("session-status")
            .WithDescription("Check current session health");

        config.AddCommand<DevCommand>("dev")
            .WithDescription("Complete build + install + launch cycle");
        config.AddCommand<QuickCommand>("quick")
            .WithDescription("Fast iteration (skip unnecessary steps)");

        config.AddCommand<BuildDebugCommand>("build-debug")
            .WithDescription("Build debug version with monitoring");
        config.AddCommand<BuildReleaseCommand>("build-release")
            .WithDescription("Build release version");
        config.AddCommand<BuildCleanCommand>("build-clean")
            .WithDescription("Clean all build artifacts");
        config.AddCommand<InstallCommand>("install")
            .WithDescription("Install app to device");
        config.AddCommand<LaunchCommand>("launch")
            .WithDescription("Launch app on device");

        config.AddCommand<DeviceStartCommand>("device-start")
            .WithDescription("Start virtual device with monitoring");
        config.AddCommand<DeviceStopCommand>("device-stop")
            .WithDescription("Stop virtual device cleanly");
        config.AddCommand<DeviceRestartCommand>("device-restart")
            .WithDescription("Restart virtual device");
        config.AddCommand<DeviceStatusCommand>("device-status")
            .WithDescription("Show device and system status");
        config.AddCommand<DeviceLogInfoCommand>("device-get-log-info")
            .WithDescription("Get detailed log information from the device (calls kepler device get-log-info)");
        config.AddCommand<DevicePortForwardCommand>("port-forward")
            .WithDescription("Set up port forwarding to device");
        config.AddCommand<DevicePortForwardStopCommand>("port-stop")
            .WithDescription("Stop port forwarding");

        config.AddCommand<SystemStatusCommand>("system-status")
            .WithDescription("Comprehensive system health check");
        config.AddCommand<SystemCleanupCommand>("system-cleanup")
            .WithDescription("Smart cleanup of orphaned processes");
        config.AddCommand<SystemMonitorCommand>("system-monitor")
            .WithDescription("Real-time memory monitoring");

        config.AddCommand<MetroStartCommand>("metro-start")
            .WithDescription("Start Metro bundler");

        config.AddCommand<ProxyStartCommand>("proxy-start")
            .WithDescription("Start network proxy for debugging");
        config.AddCommand<ProxyStopCommand>("proxy-stop")
            .WithDescription("Stop network proxy");
        config.AddCommand<ProxyStatusCommand>("proxy-status")
            .WithDescription("Check proxy status");
        config.AddCommand<ProxyConfigureCommand>("proxy-configure")
            .WithDescription("Configure proxy settings");

        config.AddCommand<DebugBuildInfoCommand>("debug-build-info")
            .WithDescription("Show build information");
        config.AddCommand<DebugAppInfoCommand>("debug-app-info")
            .WithDescription("Show app information");
        config.AddCommand<DebugBundleSizeCommand>("debug-bundle-size")
            .WithDescription("Analyze bundle size");
        config.AddCommand<DebugNativeLogsCommand>("debug-native-logs")
            .WithDescription("View native logs");

        config.AddCommand<TestRunCommand>("test-run")
            .WithDescription("Run tests");
        config.AddCommand<TestTypecheckCommand>("typecheck")
            .WithDescription("TypeScript type checking");
        config.AddCommand<TestLintCommand>("lint")
            .WithDescription("Lint code");
        config.AddCommand<TestFormatCommand>("format")
            .WithDescription("Format code with Prettier");
        config.AddCommand<TestCheckCommand>("quality-check")
            .WithDescription("Run all quality checks");

        config.AddCommand<HelpCommand>("help")
            .WithDescription("Show help for all commands or a specific category");
        config.AddCommand<ExamplesCommand>("examples")
            .WithDescription("Show detailed usage examples");
        config.AddCommand<VersionCommand>("version")
            .WithDescription("Show version information");
    }

    internal static void ShowCategoryTable(string category, Color color)
    {
        var commands = GetCommandsForCategory(category);
        if (!commands.Any()) return;

        var title = category.ToUpper();
        AnsiConsole.MarkupLineInterpolated($"[bold {color}] {title}[/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(color);

        table.AddColumn(new TableColumn($"[bold {SecondaryColor}]Command[/]").Width(25));
        table.AddColumn(new TableColumn($"[bold {SecondaryColor}]Description[/]").Width(40));

        foreach (var (cmd, desc) in commands)
        {
            table.AddRow($"[bold {PrimaryColor}]{cmd}[/]", desc);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    internal static List<(string Command, string Description)> GetCommandsForCategory(string category)
    {
        return category.ToLower() switch
        {
            "session" => new()
            {
                ("session-start", "Complete development environment setup"),
                ("session-stop", "Clean shutdown and cleanup"),
                ("session-status", "Check current session health")
            },
            "development" => new()
            {
                ("dev", "Complete build + install + launch cycle"),
                ("quick", "Fast iteration (skip unnecessary steps)")
            },
            "build" => new()
            {
                ("build-debug", "Build debug version with monitoring"),
                ("build-release", "Build release version"),
                ("build-clean", "Clean all build artifacts"),
                ("install", "Install app to device"),
                ("launch", "Launch app on device")
            },
            "device" => new()
            {
                ("device-start", "Start virtual device with monitoring"),
                ("device-stop", "Stop virtual device cleanly"),
                ("device-restart", "Restart virtual device"),
                ("device-status", "Show device and system status"),
                ("device-get-log-info", "Get detailed log information from the device"),
                ("port-forward", "Set up port forwarding to device"),
                ("port-stop", "Stop port forwarding")
            },
            "system" => new()
            {
                ("system-status", "Comprehensive system health check"),
                ("system-cleanup", "Smart cleanup of orphaned processes"),
                ("system-monitor", "Real-time memory monitoring")
            },
            "metro" => new()
            {
                ("metro-start", "Start Metro bundler")
            },
            "proxy" => new()
            {
                ("proxy-start", "Start network proxy for debugging"),
                ("proxy-stop", "Stop network proxy"),
                ("proxy-status", "Check proxy status"),
                ("proxy-configure", "Configure proxy settings")
            },
            "debug" => new()
            {
                ("debug-build-info", "Show build information"),
                ("debug-app-info", "Show app information"),
                ("debug-bundle-size", "Analyze bundle size"),
                ("debug-native-logs", "View native logs")
            },
            "test" => new()
            {
                ("test-run", "Run tests"),
                ("typecheck", "TypeScript type checking"),
                ("lint", "Lint code"),
                ("format", "Format code with Prettier"),
                ("quality-check", "Run all quality checks")
            },
            _ => new()
        };
    }
}