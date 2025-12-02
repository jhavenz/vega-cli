namespace VegaDevCli.Domain.CpuBound;

public interface IVegaResourcesMonitor
{
    Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<MemoryInfo> MonitorMemoryContinuously(TimeSpan interval, CancellationToken cancellationToken = default);
    
    Task<int> GetCrashpadProcessCountAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetCrashpadProcessDetailsAsync(CancellationToken cancellationToken = default);
    Task CleanupCrashpadProcessesAsync(bool forceAll = false, bool skipDeviceProcesses = false, CancellationToken cancellationToken = default);
    Task<bool> IsProcessOldAsync(string pid, CancellationToken cancellationToken = default);
    
    Task CleanupVirtualDeviceProcessesAsync(bool forceAll = false, CancellationToken cancellationToken = default);
    Task<List<string>> GetVirtualDeviceProcessDetailsAsync(CancellationToken cancellationToken = default);
    Task CleanupRemainingCrashpadProcessesAsync(bool forceAll = false, CancellationToken cancellationToken = default);
    
    Task CleanupVdaProcessesAsync(CancellationToken cancellationToken = default);
    Task CleanupTelemetryProcessesAsync(CancellationToken cancellationToken = default);
    Task CleanupOldSdkProcessesAsync(CancellationToken cancellationToken = default);
    Task CleanupTemporaryFilesAsync(CancellationToken cancellationToken = default);
    
    Task<bool> IsWatchmanRunningAsync(CancellationToken cancellationToken = default);
    Task KillWatchmanAsync(CancellationToken cancellationToken = default);
}