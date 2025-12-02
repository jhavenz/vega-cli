using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VegaDevCli.Domain.CpuBound;

namespace VegaDevCli.Domain.Devices;

public class VegaDeviceManager : IVegaDeviceManager
{
    private readonly ILogger<VegaDeviceManager> _logger;
    private readonly IVegaResourcesMonitor _systemMonitor;
    private readonly IKeplerPathResolver _keplerPathResolver;

    public VegaDeviceManager(ILogger<VegaDeviceManager> logger, IVegaResourcesMonitor systemMonitor, IKeplerPathResolver keplerPathResolver)
    {
        _logger = logger;
        _systemMonitor = systemMonitor;
        _keplerPathResolver = keplerPathResolver;
    }

    public async Task<VirtualDeviceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunKeplerCommandAsync("virtual-device status", cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to get device status: {result.Error}");
        }

        try
        {
            return JsonSerializer.Deserialize<VirtualDeviceStatus>(result.Output)
                   ?? throw new InvalidOperationException("Invalid status response");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse device status: {ex.Message}");
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Vega Virtual Device...");

        var status = await GetStatusAsync(cancellationToken);
        if (status.Running)
        {
            _logger.LogInformation("Virtual device is already running (PID: {Pid})", status.ProcessId?.Qemu);
            return true;
        }

        await _systemMonitor.CleanupCrashpadProcessesAsync(false, false, cancellationToken);

        if (await _systemMonitor.IsWatchmanRunningAsync(cancellationToken))
        {
            await _systemMonitor.KillWatchmanAsync(cancellationToken);
        }
        using var memoryMonitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var memoryMonitorTask = MonitorMemoryDuringOperation("Virtual Device Startup", memoryMonitorCts.Token);

        try
        {
            var result = await RunKeplerCommandAsync("virtual-device start", cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Virtual device started successfully");

                await Task.Delay(2000, cancellationToken);
                status = await GetStatusAsync(cancellationToken);
                return status.Running;
            }
            else
            {
                _logger.LogError("Failed to start virtual device: {Error}", result.Error);
                return false;
            }
        }
        finally
        {
            memoryMonitorCts.Cancel();
            try { await memoryMonitorTask; } catch (OperationCanceledException) { }
        }
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Vega Virtual Device...");

        var result = await RunKeplerCommandAsync("virtual-device stop", cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Virtual device stopped successfully");

            await Task.Delay(2000, cancellationToken);
            await _systemMonitor.CleanupCrashpadProcessesAsync(false, false, cancellationToken);

            return true;
        }
        else
        {
            _logger.LogWarning("Failed to stop virtual device gracefully: {Error}", result.Error);

            await _systemMonitor.CleanupCrashpadProcessesAsync(true, false, cancellationToken);
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restarting Vega Virtual Device...");

        await StopAsync(cancellationToken);
        await Task.Delay(3000, cancellationToken);

        return await StartAsync(cancellationToken);
    }

    public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (status.Running)
        {
            _logger.LogDebug("Virtual device is already running");
            return true;
        }

        _logger.LogInformation("Virtual device not running. Starting...");
        return await StartAsync(cancellationToken);
    }

    private async Task MonitorMemoryDuringOperation(string operationName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting memory monitoring for {Operation}", operationName);

        try
        {
            await foreach (var memoryInfo in _systemMonitor.MonitorMemoryContinuously(TimeSpan.FromSeconds(2), cancellationToken))
            {
                if (memoryInfo.IsCriticalMemory)
                {
                    _logger.LogError("CRITICAL: Very low memory during {Operation}: {FreeMB}MB free",
                        operationName, memoryInfo.FreeMemoryMB);
                }
                else if (memoryInfo.IsLowMemory)
                {
                    _logger.LogWarning("Low memory during {Operation}: {FreeMB}MB free",
                        operationName, memoryInfo.FreeMemoryMB);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Memory monitoring cancelled for {Operation}", operationName);
        }
    }

    private async Task<(bool Success, string Output, string Error)> RunKeplerCommandAsync(
        string arguments,
        CancellationToken cancellationToken = default)
    {
        string keplerExecutable;
        try
        {
            keplerExecutable = _keplerPathResolver.GetKeplerExecutablePath();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Kepler SDK not found: {Error}", ex.Message);
            return (false, "", ex.Message);
        }

        var keplerDirectory = Path.GetDirectoryName(keplerExecutable) ?? "";

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
                    Environment =
                    {
                        ["PATH"] = $"{keplerDirectory}:{Environment.GetEnvironmentVariable("PATH")}"
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

            if (process.ExitCode != 0)
            {
                _logger.LogError("Kepler command failed (exit {ExitCode}): {Error}", process.ExitCode, error);
            }
            else
            {
                _logger.LogDebug("Kepler command succeeded");
            }

            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Kepler command: {Arguments}", arguments);
            return (false, "", ex.Message);
        }
    }
}