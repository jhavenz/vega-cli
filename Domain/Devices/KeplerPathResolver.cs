using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace VegaDevCli.Domain.Devices;

public class KeplerPathResolver : IKeplerPathResolver
{
    private readonly ILogger<KeplerPathResolver> _logger;
    private string? _cachedExecutablePath;
    private readonly object _lock = new();

    public KeplerPathResolver(ILogger<KeplerPathResolver> logger)
    {
        _logger = logger;
    }

    public string GetKeplerExecutablePath()
    {
        if (_cachedExecutablePath != null)
        {
            return _cachedExecutablePath;
        }

        lock (_lock)
        {
            if (_cachedExecutablePath != null)
            {
                return _cachedExecutablePath;
            }

            var executablePath = DiscoverKeplerPath();
            if (executablePath == null)
            {
                throw new InvalidOperationException(GetKeplerNotFoundErrorMessage());
            }

            _cachedExecutablePath = executablePath;
            _logger.LogDebug("Cached Kepler executable path: {Path}", executablePath);
            return executablePath;
        }
    }

    public bool IsKeplerAvailable()
    {
        try
        {
            var path = DiscoverKeplerPath();
            return path != null && File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetKeplerVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsKeplerAvailable())
            {
                return "Kepler SDK not available";
            }

            var executablePath = GetKeplerExecutablePath();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }

            return $"Unable to determine version (Exit: {process.ExitCode}, Error: {error.Trim()})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Kepler version");
            return $"Error getting version: {ex.Message}";
        }
    }

    public async Task<(bool Success, string Message)> ValidateKeplerInstallationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if Kepler can be discovered
            var discoveredPath = DiscoverKeplerPath();
            if (discoveredPath == null)
            {
                return (false, GetKeplerNotFoundErrorMessage());
            }

            // Check if executable file exists
            if (!File.Exists(discoveredPath))
            {
                return (false, $"Kepler executable not found at discovered path: {discoveredPath}");
            }

            // Check if executable is actually runnable
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = discoveredPath,
                        Arguments = "--help",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    return (false, $"Kepler executable is not functioning properly. Exit code: {process.ExitCode}. Error: {error.Trim()}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Unable to execute Kepler: {ex.Message}");
            }

            // Get version for validation success message
            var version = await GetKeplerVersionAsync(cancellationToken);

            return (true, $"Kepler SDK validation successful. Path: {discoveredPath}, Version: {version}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Kepler installation");
            return (false, $"Validation failed with exception: {ex.Message}");
        }
    }

    private string? DiscoverKeplerPath()
    {
        _logger.LogDebug("Starting Kepler path discovery");

        // Strategy 1: Check KEPLER_PATH environment variable
        var envPath = Environment.GetEnvironmentVariable("KEPLER_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var envExecutable = Path.Combine(envPath, "kepler");
            if (File.Exists(envExecutable))
            {
                _logger.LogDebug("Found Kepler via KEPLER_PATH: {Path}", envExecutable);
                return envExecutable;
            }
            _logger.LogWarning("KEPLER_PATH set to {Path} but kepler executable not found", envPath);
        }

        // Strategy 2: Search common SDK installation paths with multiple versions
        var keplerExecutable = SearchSdkInstallations();
        if (keplerExecutable != null)
        {
            _logger.LogDebug("Found Kepler via SDK search: {Path}", keplerExecutable);
            return keplerExecutable;
        }

        // Strategy 3: Check system PATH for kepler executable
        var pathExecutable = FindInSystemPath("kepler");
        if (pathExecutable != null)
        {
            _logger.LogDebug("Found Kepler via system PATH: {Path}", pathExecutable);
            return pathExecutable;
        }

        _logger.LogWarning("Kepler executable not found in any search location");
        return null;
    }

    private string? SearchSdkInstallations()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var searchPaths = new[]
        {
            Path.Combine(homeDirectory, "kepler", "sdk"),
            Path.Combine(homeDirectory, ".kepler", "sdk"),
            "/usr/local/kepler/sdk",
            "/opt/kepler/sdk"
        };

        foreach (var basePath in searchPaths)
        {
            _logger.LogDebug("Searching for Kepler SDK in: {Path}", basePath);

            if (!Directory.Exists(basePath))
            {
                continue;
            }

            // Find the latest version directory
            var latestVersion = FindLatestSdkVersion(basePath);
            if (latestVersion != null)
            {
                var executablePath = Path.Combine(latestVersion, "bin", "kepler");
                if (File.Exists(executablePath))
                {
                    _logger.LogDebug("Found Kepler SDK executable: {Path}", executablePath);
                    return executablePath;
                }
            }
        }

        return null;
    }

    private string? FindLatestSdkVersion(string sdkBasePath)
    {
        try
        {
            var versionDirectories = Directory.GetDirectories(sdkBasePath)
                .Where(dir => IsVersionDirectory(Path.GetFileName(dir)))
                .ToArray();

            if (versionDirectories.Length == 0)
            {
                return null;
            }

            // Sort by version number (semantic versioning aware)
            var sortedVersions = versionDirectories
                .Select(dir => new { Path = dir, Version = ParseVersion(Path.GetFileName(dir)) })
                .Where(v => v.Version != null)
                .OrderByDescending(v => v.Version)
                .ToArray();

            var latestVersionPath = sortedVersions.FirstOrDefault()?.Path;
            if (latestVersionPath != null)
            {
                _logger.LogDebug("Latest SDK version found: {Path}", latestVersionPath);
            }

            return latestVersionPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find latest SDK version in {Path}", sdkBasePath);
            return null;
        }
    }

    private static bool IsVersionDirectory(string dirName)
    {
        // Check for version patterns like "0.21.4839", "1.0.0", "2.1.0-beta", etc.
        return Regex.IsMatch(dirName, @"^\d+\.\d+(\.\d+)?(-.*)?$");
    }

    private static Version? ParseVersion(string versionString)
    {
        try
        {
            // Remove any pre-release suffixes for parsing
            var cleanVersion = versionString.Split('-')[0];
            
            // Ensure we have at least major.minor.patch
            var parts = cleanVersion.Split('.');
            while (parts.Length < 3)
            {
                cleanVersion += ".0";
                parts = cleanVersion.Split('.');
            }

            return Version.Parse(cleanVersion);
        }
        catch
        {
            return null;
        }
    }

    private string? FindInSystemPath(string executable)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        var pathSeparator = Path.PathSeparator;
        var paths = pathVariable.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), executable);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Skip invalid paths
                continue;
            }
        }

        return null;
    }

    private static string GetKeplerNotFoundErrorMessage()
    {
        return """
               Kepler SDK not found. Please ensure Kepler is properly installed.
               
               Troubleshooting steps:
               
               1. Set KEPLER_PATH environment variable:
                  export KEPLER_PATH="/path/to/kepler/sdk/x.x.x/bin"
               
               2. Install Kepler SDK to a standard location:
                  - ~/kepler/sdk/x.x.x/bin/kepler
                  - ~/.kepler/sdk/x.x.x/bin/kepler
                  - /usr/local/kepler/sdk/x.x.x/bin/kepler
               
               3. Ensure kepler is available in your system PATH
               
               4. Verify the kepler executable has proper permissions
               
               For more help, visit: https://developer.amazon.com/docs/fire-tv/vega-getting-started.html
               """;
    }
}