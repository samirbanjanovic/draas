namespace DRaaS.Core.Messaging;

/// <summary>
/// Channel names for message bus routing.
/// Platform-specific channels allow independent scaling of platform workers.
/// </summary>
public static class Channels
{
    // Instance command channels (platform-specific)
    public const string InstanceCommandsProcess = "instance.commands.process";
    public const string InstanceCommandsDocker = "instance.commands.docker";
    public const string InstanceCommandsAKS = "instance.commands.aks";

    // Configuration command channels (platform-specific)
    public const string ConfigCommandsProcess = "config.commands.process";
    public const string ConfigCommandsDocker = "config.commands.docker";
    public const string ConfigCommandsAKS = "config.commands.aks";

    // Event channels (broadcast)
    public const string InstanceEvents = "instance.events";
    public const string ConfigurationEvents = "configuration.events";
    public const string StatusEvents = "status.events";

    // Monitor channels (platform-specific)
    public const string MonitorProcess = "monitor.process";
    public const string MonitorDocker = "monitor.docker";
    public const string MonitorAKS = "monitor.aks";

    /// <summary>
    /// Get the instance command channel for a platform type.
    /// </summary>
    public static string GetInstanceCommandChannel(Models.PlatformType platformType)
    {
        return platformType switch
        {
            Models.PlatformType.Process => InstanceCommandsProcess,
            Models.PlatformType.Docker => InstanceCommandsDocker,
            Models.PlatformType.AKS => InstanceCommandsAKS,
            _ => throw new ArgumentException($"Unknown platform type: {platformType}", nameof(platformType))
        };
    }

    /// <summary>
    /// Get the config command channel for a platform type.
    /// </summary>
    public static string GetConfigCommandChannel(Models.PlatformType platformType)
    {
        return platformType switch
        {
            Models.PlatformType.Process => ConfigCommandsProcess,
            Models.PlatformType.Docker => ConfigCommandsDocker,
            Models.PlatformType.AKS => ConfigCommandsAKS,
            _ => throw new ArgumentException($"Unknown platform type: {platformType}", nameof(platformType))
        };
    }

    /// <summary>
    /// Get the monitor channel for a platform type.
    /// </summary>
    public static string GetMonitorChannel(Models.PlatformType platformType)
    {
        return platformType switch
        {
            Models.PlatformType.Process => MonitorProcess,
            Models.PlatformType.Docker => MonitorDocker,
            Models.PlatformType.AKS => MonitorAKS,
            _ => throw new ArgumentException($"Unknown platform type: {platformType}", nameof(platformType))
        };
    }
}
