using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.CoreLib.Models;

public enum PlacementProviderRuntimeStatus
{
    Unknown = 0,
    Deploying,
    Deployed,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Deleted
}
