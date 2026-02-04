namespace DRaaS.CoreLib.Models;

public record DrasiInstanceStateTransition
{
    public required DomainStatus Status { get; init; }
    public required DateTime TimeStamp { get; init; }
    public required Dictionary<string, string> StateMetadata { get; init; } = [];
}

public enum DomainStatus
{
    Registered = 0,
    Configured,
    ConfigurationError,
    Deregistered,
}

public static class DomainStatusExtensions
{
    public static bool IsActive(this DomainStatus status)
        => status != DomainStatus.Deregistered;

    public static bool HasValidConfiguration(this DomainStatus status)
        => status == DomainStatus.Configured;

    public static bool CanDeploy(this DomainStatus status)
        => status == DomainStatus.Configured;

    public static bool CanModifyConfiguration(this DomainStatus status)
        => status != DomainStatus.Deregistered;
}
