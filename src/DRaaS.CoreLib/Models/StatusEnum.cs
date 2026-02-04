namespace DRaaS.CoreLib.Models;

public enum Status
{
    Unknown = 0,
    Registered,
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
}
