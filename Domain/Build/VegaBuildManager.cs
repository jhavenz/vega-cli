using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VegaDevCli.Domain.CpuBound;
using VegaDevCli.Domain.Devices;
using VegaDevCli.Domain.Project;

namespace VegaDevCli.Domain.Build;

public class VegaBuildManager : IVegaBuildManager
{
    private readonly string _keplerPath;
    private readonly IVegaProjectManager _projectManager;

    private readonly ILogger<VegaBuildManager> _logger;
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IVegaDeviceManager _deviceManager;

    public VegaBuildManager(
        ILogger<VegaBuildManager> logger,
        IVegaResourcesMonitor systemMonitor,
        IVegaDeviceManager deviceManager,
        IVegaProjectManager projectManager)
    {
        _logger = logger;
        _systemMonitor = systemMonitor;
        _deviceManager = deviceManager;
        _projectManager = projectManager;
        _keplerPath = Environment.GetEnvironmentVariable("KEPLER_PATH")
                     ?? "/Users/jhavens/kepler/sdk/0.21.4839/bin";
    }

    public async Task<BuildResult> BuildAsync(BuildType buildType, CancellationToken cancellationToken = default)
    {
        var buildResult = new BuildResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting {BuildType} build...", buildType);

            await PreBuildSetupAsync(cancellationToken);

            using var memoryMonitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var memoryMonitorTask = MonitorBuildMemory(buildType, memoryMonitorCts.Token);

            try
            {
                var npmScript = buildType == BuildType.Debug ? "build:debug" : "build:release";
                var result = await RunNpmCommandAsync(npmScript, cancellationToken);

                buildResult.Success = result.Success;
                buildResult.ErrorMessage = result.Error;
                buildResult.LogEntries.AddRange(result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries));

                if (buildResult.Success)
                {
                    _logger.LogInformation("Build completed successfully in {Duration}", stopwatch.Elapsed);

                    await CollectBuildArtifacts(buildResult, buildType);
                }
                else
                {
                    _logger.LogError("Build failed: {Error}", result.Error);
                }
            }
            finally
            {
                memoryMonitorCts.Cancel();
                try { await memoryMonitorTask; } catch (OperationCanceledException) { /* Expected */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build process failed with exception");
            buildResult.Success = false;
            buildResult.ErrorMessage = ex.Message;
        }
        finally
        {
            buildResult.Duration = stopwatch.Elapsed;
        }

        return buildResult;
    }

    public async Task<bool> InstallAppAsync(BuildType buildType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing {BuildType} build to virtual device...", buildType);

        if (!await _deviceManager.EnsureRunningAsync(cancellationToken))
        {
            _logger.LogError("Virtual device is not running and could not be started");
            return false;
        }

        if (!File.Exists(_projectManager.GetProjectPath("buildinfo.json")))
        {
            _logger.LogError("buildinfo.json not found. Please run build first.");
            return false;
        }

        var buildFlag = buildType == BuildType.Debug ? "Debug" : "Release";
        var result = await RunKeplerCommandAsync($"device install-app -b {buildFlag} --dir \"{_projectManager.ProjectRoot}\"", cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("App installed successfully");
            return true;
        }
        else
        {
            _logger.LogError("App installation failed: {Error}", result.Error);
            return false;
        }
    }

    public async Task<bool> LaunchAppAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Launching app on virtual device...");

        if (!await _deviceManager.EnsureRunningAsync(cancellationToken))
        {
            _logger.LogError("Virtual device is not running and could not be started");
            return false;
        }

        var result = await RunKeplerCommandAsync("device launch-app", cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("App launched successfully");
            return true;
        }
        else
        {
            _logger.LogError("App launch failed: {Error}", result.Error);
            return false;
        }
    }

    public async Task<BuildResult> BuildInstallLaunchAsync(BuildType buildType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting complete build-install-launch cycle for {BuildType}...", buildType);

        var buildResult = await BuildAsync(buildType, cancellationToken);

        if (!buildResult.Success)
        {
            return buildResult;
        }

        var installSuccess = await InstallAppAsync(buildType, cancellationToken);
        if (!installSuccess)
        {
            buildResult.Success = false;
            buildResult.ErrorMessage = "Installation failed after successful build";
            return buildResult;
        }

        var launchSuccess = await LaunchAppAsync(cancellationToken);
        if (!launchSuccess)
        {
            buildResult.Success = false;
            buildResult.ErrorMessage = "Launch failed after successful build and install";
            return buildResult;
        }

        _logger.LogInformation("Complete development cycle completed successfully!");
        return buildResult;
    }

    public async Task<bool> CleanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning build artifacts...");

        try
        {
            var dirsToClean = new[] { "build", "kepler-build", "bundle", "generated", "coverage" };
            var filesToClean = new[] { "buildinfo.json", "memory-monitor.log" };

            foreach (var dir in dirsToClean)
            {
                var fullPath = _projectManager.GetProjectPath(dir);
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    _logger.LogDebug("Deleted directory: {Directory}", fullPath);
                }
            }

            foreach (var file in filesToClean)
            {
                var fullPath = _projectManager.GetProjectPath(file);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogDebug("Deleted file: {File}", fullPath);
                }
            }

            var debugLogs = Directory.GetFiles(_projectManager.ProjectRoot, "kepler-debug-*.log");
            foreach (var log in debugLogs)
            {
                File.Delete(log);
                _logger.LogDebug("Deleted log: {Log}", log);
            }

            _logger.LogInformation("Clean completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean build artifacts");
            return false;
        }
    }

    private async Task PreBuildSetupAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing pre-build setup...");

        if (await _systemMonitor.IsWatchmanRunningAsync(cancellationToken))
        {
            _logger.LogInformation("Killing Watchman to prevent file watcher conflicts");
            await _systemMonitor.KillWatchmanAsync(cancellationToken);
        }

        var watchmanConfig = _projectManager.GetProjectPath(".watchmanconfig");
        if (!File.Exists(watchmanConfig))
        {
            await File.WriteAllTextAsync(watchmanConfig, "{}", cancellationToken);
            _logger.LogDebug("Created empty .watchmanconfig");
        }

        var crashpadCount = await _systemMonitor.GetCrashpadProcessCountAsync(cancellationToken);
        if (crashpadCount > 0)
        {
            _logger.LogInformation("Cleaning up {Count} orphaned crashpad processes", crashpadCount);
            await _systemMonitor.CleanupCrashpadProcessesAsync(false, false, cancellationToken);
        }
    }

    private async Task MonitorBuildMemory(BuildType buildType, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting memory monitoring for {BuildType} build", buildType);

        try
        {
            await foreach (var memoryInfo in _systemMonitor.MonitorMemoryContinuously(TimeSpan.FromSeconds(3), cancellationToken))
            {
                if (memoryInfo.IsCriticalMemory)
                {
                    _logger.LogError("CRITICAL: Very low memory during build: {AvailableMB}MB available (Free: {FreeMB}MB, Inactive: {InactiveMB}MB)",
                        memoryInfo.AvailableMemoryMB, memoryInfo.FreeMemoryMB, memoryInfo.InactiveMemoryMB);
                }
                else if (memoryInfo.IsLowMemory)
                {
                    _logger.LogWarning("Low memory during build: {AvailableMB}MB available (Free: {FreeMB}MB, Inactive: {InactiveMB}MB)",
                        memoryInfo.AvailableMemoryMB, memoryInfo.FreeMemoryMB, memoryInfo.InactiveMemoryMB);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Memory monitoring cancelled for build");
        }
    }

    private async Task CollectBuildArtifacts(BuildResult buildResult, BuildType buildType)
    {
        try
        {
            var buildInfoPath = _projectManager.GetProjectPath("buildinfo.json");
            if (File.Exists(buildInfoPath))
            {
                buildResult.BuildInfoPath = buildInfoPath;
            }

            var buildDir = _projectManager.GetProjectPath("build");
            if (Directory.Exists(buildDir))
            {
                var vpkgFiles = Directory.GetFiles(buildDir, "*.vpkg", SearchOption.AllDirectories);
                buildResult.ArtifactPaths.AddRange(vpkgFiles);
            }

            _logger.LogInformation("Found {Count} build artifacts", buildResult.ArtifactPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect build artifact information");
        }
    }

    private async Task<(bool Success, string Output, string Error)> RunNpmCommandAsync(
        string script,
        CancellationToken cancellationToken = default)
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
                    WorkingDirectory = _projectManager.ProjectRoot,
                    Environment =
                    {
                        ["WATCHMAN_DISABLE"] = "1", // Disable Watchman globally
                        ["PATH"] = $"{_keplerPath}:{Environment.GetEnvironmentVariable("PATH")}"
                    }
                }
            };

            _logger.LogDebug("Executing: npm run {Script}", script);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute npm command: {Script}", script);
            return (false, "", ex.Message);
        }
    }

    private async Task<(bool Success, string Output, string Error)> RunKeplerCommandAsync(
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var keplerExecutable = Path.Combine(_keplerPath, "kepler");

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = keplerExecutable,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _projectManager.ProjectRoot,
                    Environment =
                    {
                        ["PATH"] = $"{_keplerPath}:{Environment.GetEnvironmentVariable("PATH")}"
                    }
                }
            };

            _logger.LogDebug("Executing: {Command} {Arguments}", keplerExecutable, arguments);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Kepler command: {Arguments}", arguments);
            return (false, "", ex.Message);
        }
    }
}