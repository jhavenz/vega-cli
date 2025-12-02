namespace VegaDevCli.Domain.Build;

public enum BuildType
{
    Debug,
    Release
}

public class BuildResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> LogEntries { get; set; } = new();
    public string? BuildInfoPath { get; set; }
    public List<string> ArtifactPaths { get; set; } = new();
}

public interface IVegaBuildManager
{
    Task<BuildResult> BuildAsync(BuildType buildType, CancellationToken cancellationToken = default);
    Task<bool> InstallAppAsync(BuildType buildType, CancellationToken cancellationToken = default);
    Task<bool> LaunchAppAsync(CancellationToken cancellationToken = default);
    Task<BuildResult> BuildInstallLaunchAsync(BuildType buildType, CancellationToken cancellationToken = default);
    Task<bool> CleanAsync(CancellationToken cancellationToken = default);
}