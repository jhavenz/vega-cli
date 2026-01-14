using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace VegaDevCli.Domain.CpuBound;

public class VegaResourceMonitor : IVegaResourcesMonitor
{
    private readonly ILogger<VegaResourceMonitor> _logger;
    private const int LOW_MEMORY_THRESHOLD_MB = 500;
    private const int CRITICAL_MEMORY_THRESHOLD_MB = 200;

    public VegaResourceMonitor(ILogger<VegaResourceMonitor> logger)
    {
        _logger = logger;
    }

    private long? _cachedPageSize;

    public async Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("Memory monitoring is only supported on macOS");
        }

        if (!_cachedPageSize.HasValue)
        {
            var pageSizeResult = await RunCommandAsync("sysctl", "-n hw.pagesize", cancellationToken);
            if (pageSizeResult.Success && long.TryParse(pageSizeResult.Output.Trim(), out var pageSize))
            {
                _cachedPageSize = pageSize;
            }
            else
            {
                _logger.LogWarning("Failed to get page size via sysctl, defaulting to 16KB");
                _cachedPageSize = 16384;
            }
        }

        var result = await RunCommandAsync("vm_stat", "", cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to get memory info: {result.Error}");
        }

        return ParseMemoryInfo(result.Output);
    }

    public async Task<int> GetCrashpadProcessCountAsync(CancellationToken cancellationToken = default)
    {
        var processes = await GetCrashpadProcessDetailsAsync(cancellationToken);
        return processes.Count;
    }

    public async Task<List<string>> GetCrashpadProcessDetailsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("ps", "aux", cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to get process list: {Error}", result.Error);
            return new List<string>();
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("crashpad_handler") && line.Contains("kepler"))
            .ToList();
    }

    public async Task CleanupCrashpadProcessesAsync(bool forceAll = false, bool skipDeviceProcesses = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive process cleanup (forceAll: {ForceAll}, skipDeviceProcesses: {SkipDeviceProcesses})", forceAll, skipDeviceProcesses);

        // Step 1: Clean up supporting processes first
        await CleanupVdaProcessesAsync(cancellationToken);
        await CleanupTelemetryProcessesAsync(cancellationToken);
        await CleanupOldSdkProcessesAsync(cancellationToken);

        // Step 2: Get initial crashpad count
        var initialProcesses = await GetCrashpadProcessDetailsAsync(cancellationToken);

        if (initialProcesses.Count == 0)
        {
            _logger.LogInformation("No crashpad processes found to cleanup");
        }
        else
        {
            _logger.LogInformation("Found {Count} crashpad processes. Starting robust cleanup...", initialProcesses.Count);

            // Step 3: Try to shut down parent virtual device processes first (only if not skipping)
            if (!skipDeviceProcesses)
            {
                await CleanupVirtualDeviceProcessesAsync(forceAll, cancellationToken);
                
                // Wait for graceful shutdown
                await Task.Delay(3000, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Skipping virtual device process cleanup to preserve running device");
            }

            // Step 4: Clean up any remaining crashpad processes
            await CleanupRemainingCrashpadProcessesAsync(forceAll, cancellationToken);
        }

        await CleanupTemporaryFilesAsync(cancellationToken);

        _logger.LogInformation("Comprehensive cleanup completed");
    }

    public async Task CleanupVirtualDeviceProcessesAsync(bool forceAll = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up virtual device parent processes");

        // Find kepler-virtual-device processes
        var result = await RunCommandAsync("ps", "aux", cancellationToken);
        if (!result.Success) return;

        var virtualDeviceProcesses = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("kepler-virtual-device") && line.Contains("kepler"))
            .ToList();

        if (virtualDeviceProcesses.Any())
        {
            _logger.LogInformation("Found {Count} virtual device processes to terminate", virtualDeviceProcesses.Count);

            var pidsToKill = new List<string>();
            foreach (var processLine in virtualDeviceProcesses)
            {
                var parts = processLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var pid = parts[1];
                    if (forceAll || await IsProcessOldAsync(pid, cancellationToken))
                    {
                        pidsToKill.Add(pid);
                    }
                }
            }

            if (pidsToKill.Any())
            {
                var pidArgs = string.Join(" ", pidsToKill);

                // Be aggressive from the start if forceAll is true, or if we're dealing with stubborn processes
                if (forceAll)
                {
                    _logger.LogInformation("Force killing virtual device processes immediately");
                    await RunCommandAsync("kill", $"-9 {pidArgs}", cancellationToken);
                    
                    // Wait and verify they're gone
                    await Task.Delay(3000, cancellationToken);
                }
                else
                {
                    // Try graceful termination first
                    _logger.LogInformation("Attempting graceful termination of virtual device processes");
                    await RunCommandAsync("kill", $"-TERM {pidArgs}", cancellationToken);
                    await Task.Delay(3000, cancellationToken);

                    // Check if they're still running and force kill if necessary
                    var stillRunning = await GetVirtualDeviceProcessDetailsAsync(cancellationToken);
                    if (stillRunning.Any())
                    {
                        _logger.LogWarning("Virtual device processes still running, force killing");
                        await RunCommandAsync("kill", $"-9 {pidArgs}", cancellationToken);
                        await Task.Delay(3000, cancellationToken);
                    }
                }

                // Final verification and additional cleanup if needed
                var finalCheck = await GetVirtualDeviceProcessDetailsAsync(cancellationToken);
                if (finalCheck.Any())
                {
                    _logger.LogWarning("Virtual device processes are still running after kill attempts");
                    
                    // Try more aggressive approaches
                    await RunCommandAsync("pkill", "-9 -f kepler-virtual-device", cancellationToken);
                    await Task.Delay(2000, cancellationToken);
                }
            }
        }
    }

    public async Task<List<string>> GetVirtualDeviceProcessDetailsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("ps", "aux", cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to get process list: {Error}", result.Error);
            return new List<string>();
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("kepler-virtual-device") && line.Contains("kepler"))
            .ToList();
    }

    public async Task CleanupRemainingCrashpadProcessesAsync(bool forceAll = false, CancellationToken cancellationToken = default)
    {
        // Multiple passes to handle respawning processes
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var processes = await GetCrashpadProcessDetailsAsync(cancellationToken);
            
            if (processes.Count == 0)
            {
                _logger.LogInformation("No crashpad processes remaining after attempt {Attempt}", attempt + 1);
                break;
            }

            _logger.LogInformation("Attempt {Attempt}: Found {Count} remaining crashpad processes", attempt + 1, processes.Count);

            var pidsToKill = new List<string>();

            foreach (var processLine in processes)
            {
                var parts = processLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var pid = parts[1];

                    if (forceAll)
                    {
                        pidsToKill.Add(pid);
                    }
                    else
                    {
                        if (await IsProcessOldAsync(pid, cancellationToken))
                        {
                            pidsToKill.Add(pid);
                        }
                    }
                }
            }

            if (pidsToKill.Count > 0)
            {
                var pidArgs = string.Join(" ", pidsToKill);

                // Be more aggressive - start with SIGKILL immediately on later attempts
                var signal = attempt == 0 ? "" : "-9 ";
                _logger.LogInformation("Killing {Count} crashpad processes with signal {Signal}", pidsToKill.Count, signal.Trim() == "-9" ? "SIGKILL" : "SIGTERM");
                await RunCommandAsync("kill", $"{signal}{pidArgs}", cancellationToken);

                // Wait less time on later attempts
                var delayMs = Math.Max(1000, 3000 - (attempt * 1000));
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    public async Task<bool> IsWatchmanRunningAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("pgrep", "watchman", cancellationToken);
        return result.Success && !string.IsNullOrWhiteSpace(result.Output);
    }

    public async Task KillWatchmanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Killing Watchman processes");
        await RunCommandAsync("killall", "watchman", cancellationToken);
    }

    public async Task CleanupVdaProcessesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning up VDA track-devices processes...");

        var result = await RunCommandAsync("pkill", "-f \"vda track-devices\"", cancellationToken);
        if (result.Success)
        {
            _logger.LogInformation("VDA processes terminated");
        }
        else
        {
            _logger.LogDebug("No VDA processes found or already terminated");
        }
    }

    public async Task CleanupTelemetryProcessesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning up telemetry service processes...");

        var result = await RunCommandAsync("pkill", "-f \"telemetry -baseDir\"", cancellationToken);
        if (result.Success)
        {
            _logger.LogInformation("Telemetry service terminated");
        }
        else
        {
            _logger.LogDebug("No telemetry service found or already terminated");
        }
    }

    public async Task CleanupOldSdkProcessesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning up old SDK version processes...");

        var result = await RunCommandAsync("ps", "aux", cancellationToken);
        if (result.Success)
        {
            var oldSdkProcesses = result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("0.21.4726"))
                .ToList();

            if (oldSdkProcesses.Any())
            {
                _logger.LogWarning("Found {Count} old SDK version processes", oldSdkProcesses.Count);

                var oldPids = new List<string>();
                foreach (var processLine in oldSdkProcesses)
                {
                    var parts = processLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        oldPids.Add(parts[1]);
                    }
                }

                if (oldPids.Any())
                {
                    var pidArgs = string.Join(" ", oldPids);
                    await RunCommandAsync("kill", $"-9 {pidArgs}", cancellationToken);
                    _logger.LogInformation("Terminated {Count} old SDK processes", oldPids.Count);
                }
            }
        }
    }

    public async Task<bool> IsProcessOldAsync(string pid, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunCommandAsync("ps", $"-o etime= -p {pid}", cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return false; // Process not found, consider it not old
            }

            var etime = result.Output.Trim();

            if (etime.Contains('-'))
            {
                return true;
            }
            else if (etime.Count(c => c == ':') == 2)
            {
                var parts = etime.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[0], out var hours))
                {
                    return hours >= 1;
                }
            }
            else if (etime.Count(c => c == ':') == 1)
            {
                var parts = etime.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out var minutes))
                {
                    return minutes > 60;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check process age for PID {Pid}", pid);
            return false;
        }
    }

    public async Task CleanupTemporaryFilesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning up temporary files...");

        var cleanupTasks = new[]
        {
            RunCommandAsync("rm", "-rf /tmp/android-*/emu-crash-*.db", cancellationToken),
            RunCommandAsync("find", "/var/folders -name '*kepler*' -type d -exec rm -rf {} + 2>/dev/null || true", cancellationToken),
            RunCommandAsync("find", "/var/folders -name '*vega*' -type d -exec rm -rf {} + 2>/dev/null || true", cancellationToken),
            RunCommandAsync("rm", "-f memory-monitor.log", cancellationToken),
            RunCommandAsync("rm", "-f kepler-debug-*.log", cancellationToken),
            RunCommandAsync("rm", "-f crashpad-monitor.log", cancellationToken)
        };

        await Task.WhenAll(cleanupTasks);
        _logger.LogDebug("Temporary file cleanup completed");
    }

    public async IAsyncEnumerable<MemoryInfo> MonitorMemoryContinuously(TimeSpan interval, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            MemoryInfo? memoryInfo = null;

            try
            {
                memoryInfo = await GetMemoryInfoAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory info during monitoring");
                continue;
            }

            if (memoryInfo != null)
            {
                yield return memoryInfo;

                if (memoryInfo.IsLowMemory)
                {
                    _logger.LogWarning("Low memory detected: {FreeMB}MB free", memoryInfo.FreeMemoryMB);
                }

                if (memoryInfo.IsCriticalMemory)
                {
                    _logger.LogError("Critical memory situation: {FreeMB}MB free", memoryInfo.FreeMemoryMB);
                }
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private MemoryInfo ParseMemoryInfo(string vmStatOutput)
    {
        var memoryInfo = new MemoryInfo { Timestamp = DateTime.UtcNow };
        var pageSize = _cachedPageSize ?? 16384; // Fallback to 16KB if not set

        var lines = vmStatOutput.Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("Pages free:"))
            {
                var freePages = ExtractNumber(line);
                memoryInfo.FreeMemoryMB = (freePages * pageSize) / (1024 * 1024);
            }
            else if (line.StartsWith("Pages active:"))
            {
                var activePages = ExtractNumber(line);
                memoryInfo.ActiveMemoryMB = (activePages * pageSize) / (1024 * 1024);
            }
            else if (line.StartsWith("Pages inactive:"))
            {
                var inactivePages = ExtractNumber(line);
                memoryInfo.InactiveMemoryMB = (inactivePages * pageSize) / (1024 * 1024);
            }
        }

        return memoryInfo;
    }

    private long ExtractNumber(string line)
    {
        var match = Regex.Match(line, @"(\d+)");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    private async Task<(bool Success, string Output, string Error)> RunCommandAsync(
        string command,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

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
            _logger.LogError(ex, "Failed to execute command: {Command} {Arguments}", command, arguments);
            return (false, "", ex.Message);
        }
    }
}