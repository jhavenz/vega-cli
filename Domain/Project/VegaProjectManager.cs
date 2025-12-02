using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VegaDevCli.Domain.Project;

public class VegaProjectManager : IVegaProjectManager
{
    private readonly ILogger<VegaProjectManager> _logger;
    private string? _cachedProjectRoot;
    
    public VegaProjectManager(ILogger<VegaProjectManager> logger)
    {
        _logger = logger;
    }

    public string ProjectRoot
    {
        get
        {
            if (_cachedProjectRoot != null)
                return _cachedProjectRoot;

            var validation = ValidateCurrentDirectory();
            if (!validation.IsValid)
                throw new VegaProjectNotFoundException(validation);

            _cachedProjectRoot = validation.ProjectRoot;
            return _cachedProjectRoot;
        }
    }

    public VegaProjectValidationResult ValidateCurrentDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        // Check if we're in VegaDevCli directory - for backward compatibility
        if (IsVegaDevCliDirectory(currentDir))
        {
            var parentDir = Path.GetDirectoryName(currentDir);
            if (parentDir != null && IsValidVegaProject(parentDir))
            {
                _logger.LogDebug("Running from VegaDevCli directory, using parent as project root: {ProjectRoot}", parentDir);
                return VegaProjectValidationResult.Valid(parentDir);
            }
        }

        // Check current directory
        if (IsValidVegaProject(currentDir))
        {
            return VegaProjectValidationResult.Valid(currentDir);
        }

        // Generate detailed error information
        return GenerateValidationErrors(currentDir);
    }

    public bool IsInValidProject()
    {
        try
        {
            var validation = ValidateCurrentDirectory();
            return validation.IsValid;
        }
        catch
        {
            return false;
        }
    }

    public string GetProjectPath(string relativePath)
    {
        return Path.Combine(ProjectRoot, relativePath);
    }

    public void EnsureValidProject()
    {
        var validation = ValidateCurrentDirectory();
        if (!validation.IsValid)
            throw new VegaProjectNotFoundException(validation);
    }

    private bool IsVegaDevCliDirectory(string directory)
    {
        // Check if current directory is the VegaDevCli directory
        var directoryName = Path.GetFileName(directory);
        return directoryName.Equals("VegaDevCli", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(Path.Combine(directory, "VegaDevCli.csproj"));
    }

    private bool IsValidVegaProject(string directory)
    {
        try
        {
            // Must have package.json
            var packageJsonPath = Path.Combine(directory, "package.json");
            if (!File.Exists(packageJsonPath))
                return false;

            // Must have manifest.toml
            var manifestPath = Path.Combine(directory, "manifest.toml");
            if (!File.Exists(manifestPath))
                return false;

            // Validate package.json content
            if (!IsValidPackageJson(packageJsonPath))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error validating directory {Directory}", directory);
            return false;
        }
    }

    private bool IsValidPackageJson(string packageJsonPath)
    {
        try
        {
            var packageJsonContent = File.ReadAllText(packageJsonPath);
            var packageJson = JsonDocument.Parse(packageJsonContent);
            var root = packageJson.RootElement;

            // Check for React Native dependencies
            if (root.TryGetProperty("dependencies", out var dependencies))
            {
                var hasReactNative = dependencies.TryGetProperty("react-native", out _);
                if (!hasReactNative)
                    return false;
            }
            else
            {
                return false;
            }

            // Check for required build scripts
            if (root.TryGetProperty("scripts", out var scripts))
            {
                var hasBuildDebug = scripts.TryGetProperty("build:debug", out _);
                var hasBuildRelease = scripts.TryGetProperty("build:release", out _);
                
                if (!hasBuildDebug || !hasBuildRelease)
                    return false;
            }
            else
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing package.json at {Path}", packageJsonPath);
            return false;
        }
    }

    private VegaProjectValidationResult GenerateValidationErrors(string directory)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for package.json
        var packageJsonPath = Path.Combine(directory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            errors.Add("package.json not found");
        }
        else
        {
            // Detailed package.json validation
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                var packageJson = JsonDocument.Parse(content);
                var root = packageJson.RootElement;

                if (!root.TryGetProperty("dependencies", out var deps) || 
                    !deps.TryGetProperty("react-native", out _))
                {
                    errors.Add("package.json missing react-native dependency");
                }

                if (!root.TryGetProperty("scripts", out var scripts))
                {
                    errors.Add("package.json missing scripts section");
                }
                else
                {
                    if (!scripts.TryGetProperty("build:debug", out _))
                        errors.Add("package.json missing 'build:debug' script");
                    
                    if (!scripts.TryGetProperty("build:release", out _))
                        errors.Add("package.json missing 'build:release' script");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"package.json is malformed: {ex.Message}");
            }
        }

        // Check for manifest.toml
        var manifestPath = Path.Combine(directory, "manifest.toml");
        if (!File.Exists(manifestPath))
        {
            errors.Add("manifest.toml not found (required for Vega platform)");
        }

        // Check for other typical Vega project files
        var srcPath = Path.Combine(directory, "src");
        if (!Directory.Exists(srcPath))
        {
            warnings.Add("src/ directory not found (typical for React Native projects)");
        }

        if (errors.Count == 0)
        {
            errors.Add("Directory does not appear to be a valid Vega project");
        }

        return VegaProjectValidationResult.Invalid(errors, warnings);
    }
}