using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace VegaDevCli.Commands;

public sealed class HelpSettings : CommandSettings
{
    [CommandArgument(0, "[category]")]
    [Description("Category to show help for (" + Program.CategoryList + ")")]
    public string? Category { get; set; }
}

[Description("Show help for all commands or a specific category")]
public sealed class HelpCommand : Command<HelpSettings>
{
    public override int Execute(CommandContext context, HelpSettings settings, CancellationToken cancellationToken)
    {
        return RenderHelp(settings.Category);
    }

    public static int RenderHelp(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            ShowAllHelp();
            return 0;
        }

        var categoryLower = category.ToLower();

        if (!Program.AllCategories.Contains(categoryLower))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Unknown category '{category}'");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Valid categories:[/]");
            foreach (var cat in Program.AllCategories)
            {
                AnsiConsole.MarkupLine($"  [{Program.SecondaryColor}]{cat}[/]");
            }
            return 1;
        }

        ShowCategoryHelp(categoryLower);
        return 0;
    }

    private static void ShowAllHelp()
    {
        AnsiConsole.MarkupLine($"[bold {Program.PrimaryColor}]Vega Development CLI[/]");

        AnsiConsole.MarkupLine($"[{Program.SecondaryColor}]Command-line tools for Vega OS app development[/]");
        AnsiConsole.WriteLine();

        foreach (var cat in Program.AllCategories)
        {
            Program.ShowCategoryTable(cat, Program.PrimaryColor);
        }

        AnsiConsole.MarkupLine($"Use [{Program.PrimaryColor}]vega help <category>[/] to see commands for a specific category.");
        AnsiConsole.MarkupLine($"Use [{Program.PrimaryColor}]vega <command> --help[/] for detailed information on a command.");
        AnsiConsole.MarkupLine($"Use [{Program.PrimaryColor}]vega examples[/] for detailed usage examples.");
    }

    private static void ShowCategoryHelp(string category)
    {
        AnsiConsole.MarkupLine($"[bold]Vega CLI - {category} Commands[/]");
        AnsiConsole.WriteLine();

        Program.ShowCategoryTable(category, Program.PrimaryColor);

        AnsiConsole.MarkupLine($"Use [{Program.PrimaryColor}]vega <command> --help[/] for detailed information on a command.");
    }
}
