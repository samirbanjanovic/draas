using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.Core.Models;

public record DrasiInstanceState
{
    public required Status Status { get; init; } = Status.Unknown;
    public required DateTime TimeStamp { get; init; } = DateTime.Now;
    public required Dictionary<string, string> StateMetadata { get; init; } = [];
}
