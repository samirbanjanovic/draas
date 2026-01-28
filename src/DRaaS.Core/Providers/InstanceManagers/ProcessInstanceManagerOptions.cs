namespace DRaaS.Core.Providers.InstanceManagers;

/// <summary>
/// Configuration options for ProcessInstanceManager.
/// </summary>
public class ProcessInstanceManagerOptions
{
    /// <summary>
    /// Path to the drasi-server executable.
    /// </summary>
    public string ExecutablePath { get; set; } = "drasi-server";

    /// <summary>
    /// Path to the YAML configuration file template for drasi-server instances.
    /// Can include placeholders like {InstanceId}, {Port}, {Host}.
    /// </summary>
    public string ConfigurationTemplatePath { get; set; } = string.Empty;

    /// <summary>
    /// Directory where instance-specific configuration files will be written.
    /// </summary>
    public string InstanceConfigDirectory { get; set; } = "./drasi-configs";

    /// <summary>
    /// Default log level for drasi-server instances.
    /// </summary>
    public string DefaultLogLevel { get; set; } = "info";

    /// <summary>
    /// Timeout in seconds to wait for graceful shutdown before force kill.
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Working directory for drasi-server processes.
    /// </summary>
    public string WorkingDirectory { get; set; } = "./drasi-runtime";
}
