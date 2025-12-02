using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace VegaDevCli.Commands;

public sealed class VersionSettings : CommandSettings
{
}

[Description("Show version information")]
public sealed class VersionCommand : Command<VersionSettings>
{
    public override int Execute(CommandContext context, VersionSettings settings, CancellationToken cancellationToken = default)
    {
        var version = typeof(VersionCommand).Assembly.GetName().Version?.ToString() ?? "Unknown";
        
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders();
            
        table.AddColumn("");
        table.AddColumn("");
        
        table.AddRow("[bold]Vega Development CLI[/]", $"v{version}");
        table.AddRow("[grey]Built for[/]", "Amazon Fire TV / Vega OS development");
        
        AnsiConsole.Write(table);
        
        return 0;
    }
}