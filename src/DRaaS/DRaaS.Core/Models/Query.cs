using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.Core.Models;

public record QuerySource
{
    public string? SourceId { get; init; }
}


public record Query
{
    public string? Id { get; init; }
    public string? QueryText { get; init; }
    public List<QuerySource>? Sources { get; init; }
}

