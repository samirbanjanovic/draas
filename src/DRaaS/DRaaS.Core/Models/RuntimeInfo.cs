using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.Core.Models;



public record RuntimeInfo
{
    public required string PlatformType { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public required Dictionary<string, string> RuntimeMetadata { get; init; } = [];    
}
