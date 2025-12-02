namespace VegaDevCli.Domain.CpuBound;

public class MemoryInfo
{
    public long FreeMemoryMB { get; set; }
    public long ActiveMemoryMB { get; set; }
    public long InactiveMemoryMB { get; set; }
    public DateTime Timestamp { get; set; }

    public bool IsLowMemory => FreeMemoryMB < 500;
    public bool IsCriticalMemory => FreeMemoryMB < 200;
}