using System.Text.Json.Serialization;

namespace VegaDevCli.Domain.Devices;

public class VirtualDeviceStatus
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("process_id")]
    public ProcessInfo? ProcessId { get; set; }
}

public class ProcessInfo
{
    [JsonPropertyName("qemu")]
    public int Qemu { get; set; }
}