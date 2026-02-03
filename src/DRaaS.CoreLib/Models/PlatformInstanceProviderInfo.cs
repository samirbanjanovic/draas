using System.Collections.ObjectModel;

namespace DRaaS.Core.Models;

public record PlatformInstanceProviderInfo
{
    public PlatformInstanceProviderInfo(List<string> supportedPlatforms)
    {
        SupportedPlatforms = new ReadOnlyCollection<string>(supportedPlatforms);
    }

    public ReadOnlyCollection<string> SupportedPlatforms { get; init; }
}
