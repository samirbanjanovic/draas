using System.Collections.ObjectModel;

namespace DRaaS.Core.Models;

public record PlatformInstanceManagerInfo
{
    public PlatformInstanceManagerInfo(List<string> supportedPlatforms)
    {
        SupportedPlatforms = new ReadOnlyCollection<string>(supportedPlatforms);
    }

    public required ReadOnlyCollection<string> SupportedPlatforms { get; set; }
}
