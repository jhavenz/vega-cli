namespace VegaDevCli.Domain.Project;

public class VegaProjectValidationResult
{
    public bool IsValid { get; init; }
    public string ProjectRoot { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public static VegaProjectValidationResult Valid(string projectRoot)
    {
        return new VegaProjectValidationResult
        {
            IsValid = true,
            ProjectRoot = projectRoot
        };
    }

    public static VegaProjectValidationResult Invalid(List<string> errors, List<string>? warnings = null)
    {
        return new VegaProjectValidationResult
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? new()
        };
    }

    public string GetFormattedErrorMessage()
    {
        if (IsValid) return "Project is valid";

        var message = "Current directory is not a valid Vega project.\n\n";
        
        if (Errors.Any())
        {
            message += "Issues found:\n";
            foreach (var error in Errors)
            {
                message += $"  ERROR: {error}\n";
            }
        }

        if (Warnings.Any())
        {
            message += "\nWarnings:\n";
            foreach (var warning in Warnings)
            {
                message += $"  WARNING: {warning}\n";
            }
        }

        message += "\nRun 'vega --help' for usage information.";
        return message;
    }
}