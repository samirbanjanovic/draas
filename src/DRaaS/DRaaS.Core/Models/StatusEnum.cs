using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.Core.Models;

public enum Status
{
    Registered = 0,
    Deregistered,
    Creating,
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Deleting,
    Deleted,
    Error,
    Unknown = 999
}
