namespace VegaDevCli.Domain.Project;

public interface IVegaProjectManager
{
    /// <summary>
    /// Gets the root path of the current Vega project.
    /// Throws VegaProjectNotFoundException if not in a valid project.
    /// </summary>
    string ProjectRoot { get; }

    /// <summary>
    /// Validates that the current directory contains a valid Vega project.
    /// </summary>
    /// <returns>Validation result with details about any issues found</returns>
    VegaProjectValidationResult ValidateCurrentDirectory();

    /// <summary>
    /// Checks if the current directory is part of a valid Vega project.
    /// </summary>
    /// <returns>True if in a valid Vega project directory</returns>
    bool IsInValidProject();

    /// <summary>
    /// Gets the absolute path for a file relative to the project root.
    /// </summary>
    /// <param name="relativePath">Path relative to project root</param>
    /// <returns>Full absolute path</returns>
    string GetProjectPath(string relativePath);

    /// <summary>
    /// Ensures the current directory is a valid Vega project.
    /// Throws VegaProjectNotFoundException with helpful message if not.
    /// </summary>
    void EnsureValidProject();
}