namespace VegaDevCli.Domain.CpuBound;

public class MemoryInfo
{
    public long FreeMemoryMB { get; set; }
    public long ActiveMemoryMB { get; set; }
    public long InactiveMemoryMB { get; set; }
    public long AvailableMemoryMB => FreeMemoryMB + InactiveMemoryMB;
    public DateTime Timestamp { get; set; }

    public bool IsLowMemory => AvailableMemoryMB < 500;
    public bool IsCriticalMemory => AvailableMemoryMB < 200;
}