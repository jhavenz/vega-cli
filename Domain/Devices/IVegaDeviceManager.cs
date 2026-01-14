
namespace VegaDevCli.Domain.Devices;

public interface IVegaDeviceManager
{
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
    Task<bool> StopAsync(CancellationToken cancellationToken = default);
    Task<bool> RestartAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default);
    Task<VirtualDeviceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Output, string Error)> GetLogInfoAsync(CancellationToken cancellationToken = default);
}