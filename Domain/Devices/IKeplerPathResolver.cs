namespace VegaDevCli.Domain.Devices;

public interface IKeplerPathResolver
{
    /// <summary>
    /// Gets the full path to the Kepler executable
    /// </summary>
    /// <returns>Full path to the kepler executable</returns>
    string GetKeplerExecutablePath();
    
    /// <summary>
    /// Checks if Kepler SDK is properly installed and accessible
    /// </summary>
    /// <returns>True if Kepler is available, false otherwise</returns>
    bool IsKeplerAvailable();
    
    /// <summary>
    /// Gets the version of the installed Kepler SDK for diagnostics
    /// </summary>
    /// <returns>Version string or error message if unavailable</returns>
    Task<string> GetKeplerVersionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs comprehensive validation of Kepler installation
    /// </summary>
    /// <returns>Validation result with success status and detailed message</returns>
    Task<(bool Success, string Message)> ValidateKeplerInstallationAsync(CancellationToken cancellationToken = default);
}